using System.Net;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class PlanningTests
{
    [Fact]
    public async Task AdminCanCreateLinkedRecordsFilterTotalsAndProtectUsedCategories()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        const string root = "/Admin/Planeamento/";
        foreach (var page in new[] { "Orcamento", "Fornecedores", "Categorias" })
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(root + page)).StatusCode);

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "planning@example.test", Email = "planning@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "TestPassword!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var token = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            var values = fields.ToList();
            var formPath = path.EndsWith("/Fornecedores/Guardar") ? root + "Fornecedores/Ficha"
                : path.EndsWith("/Orcamento/Guardar") ? root + "Orcamento/Linha" : null;
            if (formPath != null)
            {
                var html = await client.GetStringAsync(formPath);
                foreach (Match field in Regex.Matches(html, "name=\"__Invariant\"[^>]*value=\"([^\"]+)\""))
                    values.Add(new("__Invariant", field.Groups[1].Value));
            }
            return await client.PostAsync(path, new FormUrlEncodedContent(values));
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Admin/Login", new() { ["email"] = admin.Email, ["password"] = "TestPassword!2027" })).StatusCode);
        token = await HttpTests.AntiforgeryToken(client, root + "Categorias");
        var db = services.GetRequiredService<ApplicationDbContext>();
        async Task Save(string action, Dictionary<string, string> fields)
        {
            var response = await Post(root + action, fields);
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.Fail(action + ": " + response.StatusCode + " " + string.Join(" ", Regex.Matches(html, @"<li data-field=.*?</li>").Select(m => m.Value)));
            }
        }

        await Save("Categorias/Guardar", new() { ["Name"] = "Música ao vivo" });
        var duplicate = await Post(root + "Categorias/Guardar", new() { ["Name"] = "  MUSICA   ao VIVO  " });
        Assert.Contains("categoria já existe", WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));
        var music = (await db.PlanningCategories.SingleAsync()).Id.ToString();
        await Save("Categorias/Guardar", new() { ["Name"] = "Quinta" });
        var venue = (await db.PlanningCategories.SingleAsync(c => c.Name == "Quinta")).Id.ToString();

        await Save("Fornecedores/Guardar", new() { ["CategoryId"] = music, ["Name"] = "Trio", ["Price"] = "240.50" });
        var supplier = (await db.Suppliers.SingleAsync()).Id.ToString();
        var form = await client.GetStringAsync(root + "Orcamento/Linha?supplierId=" + supplier);
        Assert.Contains("Trio", form);
        await Save("Orcamento/Guardar", new() { ["CategoryId"] = music, ["SupplierId"] = supplier, ["Paid"] = "60.12", ["NextPayment"] = "2027-06-05" });
        var line = await db.BudgetLines.AsNoTracking().SingleAsync();
        Assert.Equal(240.50m, line.RealCost);
        Assert.Equal(60.12m, line.Paid);
        Assert.Equal(new DateOnly(2027, 6, 5), line.NextPayment);

        var invalid = await Post(root + "Orcamento/Guardar", new() { ["CategoryId"] = venue, ["SupplierId"] = supplier });
        Assert.Contains("fornecedor da categoria", await invalid.Content.ReadAsStringAsync());
        Assert.Equal(1, await db.BudgetLines.CountAsync());
        var overpaid = await Post(root + "Orcamento/Guardar", new() { ["CategoryId"] = music, ["SupplierId"] = supplier, ["Paid"] = "241" });
        Assert.Contains("não pode ultrapassar", WebUtility.HtmlDecode(await overpaid.Content.ReadAsStringAsync()));
        Assert.Equal(1, await db.BudgetLines.CountAsync());
        await Save("Orcamento/Guardar", new() { ["CategoryId"] = venue, ["RealCost"] = "1500", ["Paid"] = "500", ["Notes"] = "Reserva do espaço" });
        var filtered = WebUtility.HtmlDecode(await client.GetStringAsync(root + "Orcamento?categoryId=" + music));
        var dashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Dashboard/Index"));
        Assert.Contains(1180.38m.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")), dashboard);
        Assert.Contains("05/06/2027", dashboard);
        Assert.Contains($"href=\"{root}Orcamento/Linha/{line.Id}\"", dashboard);
        Assert.Contains("180,38", filtered);
        Assert.Contains("25,00", filtered);
        Assert.DoesNotContain("Reserva do espaço", filtered);
        Assert.Contains("Reserva do espaço", WebUtility.HtmlDecode(await client.GetStringAsync(root + "Orcamento")));
        Assert.DoesNotContain("Trio", await client.GetStringAsync(root + "Fornecedores?categoryId=" + venue));
        Assert.Contains("Ver linhas (1)", await client.GetStringAsync(root + "Fornecedores?categoryId=" + music));
        await Save("Categorias/Guardar", new() { ["Id"] = music, ["Name"] = "Animação musical" });
        Assert.Contains("Animação musical", WebUtility.HtmlDecode(await client.GetStringAsync(root + "Orcamento/Linha/" + line.Id)));
        Assert.Contains("Animação musical", WebUtility.HtmlDecode(await client.GetStringAsync(root + "Fornecedores/Ficha/" + supplier)));

        await Save("Fornecedores/Guardar", new() { ["Id"] = supplier, ["CategoryId"] = music, ["Name"] = "Trio", ["Price"] = "300" });
        Assert.Equal(240.50m, (await db.BudgetLines.AsNoTracking().SingleAsync(x => x.Id == line.Id)).RealCost);
        var change = await Post(root + "Fornecedores/Guardar", new() { ["Id"] = supplier, ["CategoryId"] = venue, ["Name"] = "Trio" });
        Assert.Contains("Desassocia primeiro", await change.Content.ReadAsStringAsync());
        await Save("Categorias/Eliminar/" + music, new());
        await Save("Fornecedores/Eliminar/" + supplier, new());
        Assert.Equal(2, await db.PlanningCategories.CountAsync());
        Assert.Equal(1, await db.Suppliers.CountAsync());
        await Save("Orcamento/Eliminar/" + line.Id, new());
        await Save("Fornecedores/Eliminar/" + supplier, new());
        await Save("Categorias/Eliminar/" + music, new());
        Assert.Equal(1, await db.PlanningCategories.CountAsync());
    }
}

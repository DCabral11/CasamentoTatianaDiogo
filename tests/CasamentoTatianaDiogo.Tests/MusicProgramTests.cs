using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class MusicProgramTests
{
    [Fact]
    public async Task AdminCanAdaptTheSeededProgramWithoutLosingUnassignedMoments()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        const string root = "/Admin/Musicas";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(root)).StatusCode);
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var admin = new ApplicationUser { UserName = "music@example.test", Email = "music@example.test", EmailConfirmed = true };
        await roles.CreateAsync(new IdentityRole("Admin"));
        Assert.True((await users.CreateAsync(admin, "MusicTest!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var token = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        Task<HttpResponseMessage> Post(string path, params (string Key, string Value)[] fields) =>
            client.PostAsync(path, new FormUrlEncodedContent(fields.Append(("__RequestVerificationToken", token))
                .Select(f => new KeyValuePair<string,string>(f.Item1, f.Item2))));
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Admin/Login", ("email", admin.Email), ("password", "MusicTest!2027"))).StatusCode);
        token = await HttpTests.AntiforgeryToken(client, root + "/Seccao");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(root + "/Seccao/1/Eliminar", new FormUrlEncodedContent([]))).StatusCode);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var mass = await db.MusicSections.SingleAsync(s => s.Name == "Cancioneiro Missa");
        var psalm = await db.MusicMoments.AsNoTracking().SingleAsync(m => m.Name == "Salmo");
        Assert.Equal(14, await db.MusicMoments.CountAsync(m => m.SectionId == mass.Id));
        Assert.Null(psalm.Song);
        var initial = WebUtility.HtmlDecode(await client.GetStringAsync(root));
        Assert.Contains("Por definir", initial);
        Assert.Contains("Músicas Quinta", initial);
        Assert.Contains("Sabes Bem", initial);
        async Task Save(string path, params (string Key, string Value)[] fields) =>
            Assert.Equal(HttpStatusCode.Redirect, (await Post(root + path, fields)).StatusCode);
        await Save("/Seccao/Guardar", ("Name", "Cocktail"), ("DisplayOrder", "1"));
        var section = await db.MusicSections.SingleAsync(s => s.Name == "Cocktail");
        var id = section.Id.ToString();
        await Save("/Seccao/Guardar", ("Id", id), ("Name", "Receção"), ("DisplayOrder", "1"));
        await Save("/Momento/Guardar", ("SectionId", id), ("Name", "Entrada"), ("DisplayOrder", "5"));
        var entry = await db.MusicMoments.AsNoTracking().SingleAsync(m => m.SectionId == section.Id);
        Assert.Null(entry.Song);
        await Save("/Momento/Guardar", ("Id", psalm.Id.ToString()), ("SectionId", id), ("Name", "Primeira dança"), ("Song", "Canção escolhida"), ("DisplayOrder", "2"));
        var page = WebUtility.HtmlDecode(await client.GetStringAsync(root));
        Assert.True(page.IndexOf("Receção", StringComparison.Ordinal) < page.IndexOf("Cancioneiro Missa", StringComparison.Ordinal));
        var firstGroup = page.Split("Cancioneiro Missa")[0];
        Assert.Contains("Canção escolhida", firstGroup);
        Assert.True(firstGroup.IndexOf("Primeira dança", StringComparison.Ordinal) < firstGroup.IndexOf("Entrada", StringComparison.Ordinal));
        var invalid = await Post(root + "/Momento/Guardar", ("SectionId", "9999"), ("Name", "Inválido"), ("DisplayOrder", "1"));
        Assert.Contains("Escolhe uma secção existente.", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        await Save("/Seccao/" + id + "/Eliminar");
        Assert.True(await db.MusicSections.AnyAsync(s => s.Id == section.Id));
        await Save("/Momento/" + psalm.Id + "/Eliminar");
        await Save("/Momento/" + entry.Id + "/Eliminar");
        await Save("/Seccao/" + id + "/Eliminar");
        Assert.False(await db.MusicSections.AnyAsync(s => s.Id == section.Id));
    }
}

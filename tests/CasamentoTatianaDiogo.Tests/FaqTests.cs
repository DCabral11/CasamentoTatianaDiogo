using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class FaqTests
{
    [Fact]
    public async Task AdminCanConfigurePublishOrderPreviewAndDisableTheModule()
    {
        using var app = new HttpTests.TestApplication();
        using var admin = app.Client();
        using var visitor = app.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/Faq")).StatusCode);
        await Login(app, admin);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.FaqPageSettings.AsNoTracking().SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await Settings(admin, settings.Version, true)).StatusCode);
        var empty = WebUtility.HtmlDecode(await visitor.GetStringAsync("/Faq"));
        Assert.Contains("Dúvidas do casamento", empty);
        Assert.Contains("Conteúdo em preparação", empty);
        Assert.Contains("Primeiro parágrafo.", empty);
        Assert.Contains("Segundo parágrafo.", empty);
        Assert.Contains("Ajuda do casamento", WebUtility.HtmlDecode(await visitor.GetStringAsync("/")));

        await Save(admin, new FaqItem { Question = "Pergunta privada", Answer = "Resposta apenas para revisão", DisplayOrder = 30 });
        Assert.DoesNotContain("Pergunta privada", await visitor.GetStringAsync("/Faq?preview=true"));
        Assert.Contains("Pergunta privada", await admin.GetStringAsync("/Admin/Faq/Preview"));
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Admin/Faq/Preview")).StatusCode);
        await Save(admin, new FaqItem { Question = "Última pergunta", Answer = "Linha um.\n\nLinha dois. <script>alert('x')</script>", DisplayOrder = 20, IsPublished = true });
        await Save(admin, new FaqItem { Question = "Primeira pergunta", Answer = "Primeira resposta", DisplayOrder = 10, IsPublished = true });
        var response = await visitor.GetAsync("/Faq");
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.True(html.IndexOf("Primeira pergunta", StringComparison.Ordinal) < html.IndexOf("Última pergunta", StringComparison.Ordinal));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", raw);
        Assert.DoesNotContain("<script>alert", raw);
        Assert.Contains("<details", raw);
        Assert.Contains("faq-text", raw);
        var published = await db.FaqItems.AsNoTracking().SingleAsync(x => x.Question == "Primeira pergunta");
        published.IsPublished = false;
        await Save(admin, published);
        Assert.DoesNotContain("Primeira pergunta", await visitor.GetStringAsync("/Faq"));

        settings = await db.FaqPageSettings.AsNoTracking().SingleAsync();
        await Settings(admin, settings.Version, false);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/Faq")).StatusCode);
        Assert.DoesNotContain("Ajuda do casamento", WebUtility.HtmlDecode(await visitor.GetStringAsync("/")));
        Assert.Equal(3, await db.FaqItems.CountAsync());
        Assert.Contains("Primeira pergunta", await admin.GetStringAsync("/Admin/Faq/Preview"));
    }

    [Fact]
    public async Task ValidationPreservesInputAndStaleEditsAndDeletesAreRejected()
    {
        using var app = new HttpTests.TestApplication();
        using var admin = app.Client();
        await Login(app, admin);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invalid = await Save(admin, new FaqItem { Question = "", Answer = "Texto por guardar", IsPublished = true });
        Assert.Contains("Texto por guardar", await invalid.Content.ReadAsStringAsync());
        Assert.Empty(await db.FaqItems.ToListAsync());
        await Save(admin, new FaqItem { Question = "Pergunta original", Answer = "Resposta original" });
        var original = await db.FaqItems.AsNoTracking().SingleAsync();
        original.Answer = "Resposta mais recente";
        Assert.Equal(HttpStatusCode.Redirect, (await Save(admin, original)).StatusCode);
        original.Answer = "Resposta antiga por guardar";
        var stale = await Save(admin, original);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("Resposta antiga por guardar", await stale.Content.ReadAsStringAsync());
        await Post(admin, "Delete/" + original.Id, new() { ["version"] = original.Version.ToString() });
        var current = await db.FaqItems.AsNoTracking().SingleAsync();
        Assert.Equal("Resposta mais recente", current.Answer);
        await Post(admin, "Delete/" + current.Id, new() { ["version"] = current.Version.ToString() });
        Assert.Empty(await db.FaqItems.ToListAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Admin/Faq/Edit/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Save(admin, new FaqItem { Id = 99999, Question = "Pergunta", Answer = "Resposta" })).StatusCode);
    }

    [Fact]
    public async Task SettingsValidateRequiredFieldsAndProtectAgainstConcurrentUpdates()
    {
        using var app = new HttpTests.TestApplication();
        using var admin = app.Client();
        await Login(app, admin);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.FaqPageSettings.AsNoTracking().SingleAsync();
        await Settings(admin, original.Version, true);
        Assert.Equal(HttpStatusCode.Conflict, (await Settings(admin, original.Version, false)).StatusCode);
        var current = await db.FaqPageSettings.AsNoTracking().SingleAsync();
        Assert.True(current.IsEnabled);
        var invalid = await Post(admin, "Settings", new()
        {
            ["Settings.Version"] = current.Version.ToString(), ["Settings.Title"] = "",
            ["Settings.Introduction"] = "Manter esta introdução", ["Settings.NavigationLabel"] = "Menu", ["Settings.EmptyMessage"] = "Sem conteúdo"
        });
        Assert.Contains("Manter esta introdu", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        Assert.Equal("Dúvidas do casamento", await db.FaqPageSettings.AsNoTracking().Select(x => x.Title).SingleAsync());
    }

    [Fact]
    public async Task AdminEndpointsRequireRoleAndCsrfAndFaqSupportsTheMusicShell()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        foreach (var action in new[] { "Index", "Create", "Edit/1", "Preview" })
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin/Faq/" + action)).StatusCode);
        await Login(app, client);
        foreach (var action in new[] { "Save", "Settings", "Delete/1" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/Faq/" + action, new FormUrlEncodedContent([]))).StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/Admin/Faq/" + action)).StatusCode);
        }
        Assert.Contains("/Admin/Faq/Index", await client.GetStringAsync("/Admin/Dashboard/Index"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.FaqPageSettings.SingleAsync()).IsEnabled = true;
        (await db.WeddingSettings.SingleAsync()).SpotifyTrackUrl = "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT";
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        Assert.Contains("id=\"guest-page\"", await client.GetStringAsync("/Faq"));
        Assert.DoesNotContain("id=\"guest-page\"", await client.GetStringAsync("/Faq?sitePage=1"));
    }

    private static async Task Login(HttpTests.TestApplication app, HttpClient client)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole("Admin"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "faq@example.test", Email = "faq@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "FaqTest!2027Password")).Succeeded);
        await users.AddToRoleAsync(user, "Admin");
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["email"] = user.Email, ["password"] = "FaqTest!2027Password", ["__RequestVerificationToken"] = csrf }));
    }
    private static Task<HttpResponseMessage> Save(HttpClient client, FaqItem item) => Post(client, "Save", new()
    {
        ["Id"] = item.Id.ToString(), ["Version"] = item.Version.ToString(), ["Question"] = item.Question,
        ["Answer"] = item.Answer, ["DisplayOrder"] = item.DisplayOrder.ToString(), ["IsPublished"] = item.IsPublished.ToString()
    });
    private static Task<HttpResponseMessage> Settings(HttpClient client, Guid version, bool enabled) => Post(client, "Settings", new()
    {
        ["Settings.Version"] = version.ToString(), ["Settings.IsEnabled"] = enabled.ToString(), ["Settings.Title"] = "Dúvidas do casamento",
        ["Settings.NavigationLabel"] = "Ajuda do casamento", ["Settings.Introduction"] = "Primeiro parágrafo.\n\nSegundo parágrafo.",
        ["Settings.EmptyMessage"] = "Conteúdo em preparação"
    });
    private static async Task<HttpResponseMessage> Post(HttpClient client, string action, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await HttpTests.AntiforgeryToken(client, "/Admin/Faq/Index");
        return await client.PostAsync("/Admin/Faq/" + action, new FormUrlEncodedContent(fields));
    }
}

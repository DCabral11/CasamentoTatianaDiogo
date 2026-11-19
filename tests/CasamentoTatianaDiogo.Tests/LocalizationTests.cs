using System.Globalization;
using System.Net;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("pt", "pt-PT", "Confirmar Presença", "Pesquisar")]
    [InlineData("en", "en-GB", "RSVP", "Search")]
    [InlineData("fr", "fr-FR", "Confirmer sa présence", "Rechercher")]
    public async Task PublicPagesAndValidationUseTheSelectedLanguage(string language, string culture, string heading, string search)
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Rsvp?culture=" + language));
        Assert.Contains("lang=\"" + culture + "\"", html);
        Assert.Contains(heading, html);
        Assert.Contains(search, html);
        Assert.Contains("data-language-form", html);
        var token = Token(html);
        var response = await client.PostAsync("/Rsvp/Search?culture=" + language, Form(new()
        {
            ["Query"] = "", ["__RequestVerificationToken"] = token
        }));
        var invalid = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(language switch { "en" => "Enter your name", "fr" => "Saisis ton nom", _ => "Escreve o teu nome" }, invalid);
        Assert.Contains("data-val-required", invalid);
        var returnUrl = Regex.Match(invalid, "name=\"returnUrl\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.DoesNotContain("_LanguageSelector", returnUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(returnUrl)).StatusCode);
    }

    [Fact]
    public async Task LanguageChoicePersistsAndRemovesOldQueryOverrides()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        var change = await Change(client, "fr", "/Information?culture=pt&sitePage=1&q=Ana#travel-heading");
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.Equal("/Information?sitePage=1&q=Ana#travel-heading", change.Headers.Location!.OriginalString);
        Assert.Contains("no-store", change.Headers.CacheControl!.ToString());
        Assert.Contains(change.Headers.GetValues("Set-Cookie"), x => x.StartsWith(".AspNetCore.Culture=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("lang=\"fr-FR\"", await client.GetStringAsync("/Information"));
        Assert.Contains("lang=\"fr-FR\"", await client.GetStringAsync("/Photos"));
        await Change(client, "en", "/");
        Assert.Contains("lang=\"en-GB\"", await client.GetStringAsync("/"));
        using var separate = app.Client();
        Assert.Contains("lang=\"pt-PT\"", await separate.GetStringAsync("/"));
    }

    [Theory]
    [InlineData("de", "/")]
    [InlineData("en", "https://example.com/")]
    [InlineData("fr", "//example.com/")]
    public async Task LanguageEndpointRejectsUnsupportedCulturesAndExternalRedirects(string language, string returnUrl)
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        var result = await Change(client, language, returnUrl);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.False(result.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Language/Change", Form(new() { ["language"] = "en", ["returnUrl"] = "/" }))).StatusCode);
    }

    [Fact]
    public async Task PresentationCacheDoesNotMixLanguagesAndCustomContentHasPortugueseFallback()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.WeddingSettings.SingleAsync();
            settings.CoupleStory = "A nossa história personalizada";
            settings.WeddingDateTime = new DateTime(2027, 6, 12, 15, 30, 0);
            db.SiteContents.AddRange(
                new() { Key = SiteTranslationService.Key("en", "WeddingSettings.CoupleStory"), Value = "Our custom story", Description = "Story" },
                new() { Key = SiteTranslationService.Key("fr", "WeddingSettings.CoupleStory"), Value = "Notre histoire personnalisée", Description = "Story" });
            var faq = await db.FaqPageSettings.SingleAsync();
            faq.IsEnabled = true;
            db.FaqItems.Add(new() { Question = "Pergunta personalizada", Answer = "Resposta original", IsPublished = true });
            await db.SaveChangesAsync();
        }
        foreach (var (lang, story, month) in new[] { ("en", "Our custom story", "June"), ("fr", "Notre histoire personnalisée", "juin"), ("pt", "A nossa história personalizada", "Junho"), ("en", "Our custom story", "June") })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync("/?culture=" + lang));
            Assert.Contains(story, html);
            Assert.Contains(month, html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Pergunta personalizada", WebUtility.HtmlDecode(await client.GetStringAsync("/Faq?culture=" + lang)));
        }
        using var verify = app.Services.CreateScope();
        Assert.Equal("A nossa história personalizada", (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().WeddingSettings.SingleAsync()).CoupleStory);
    }

    [Fact]
    public async Task TranslationEditorSavesOverridesAndDetectsStaleEdits()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Translations/Index?language=fr&culture=en"));
        Assert.Contains("Content translations", html);
        var values = Regex.Matches(html, "<textarea[^>]*name=\"([^\"]+)\"[^>]*>([\\s\\S]*?)</textarea>")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        var key = "Values[" + SiteTranslationService.Key("fr", "FaqSettings.Title") + "]";
        Assert.Contains(key, values.Keys);
        values[key] = "Questions sur notre mariage";
        values["Language"] = "fr";
        values["Version"] = Regex.Match(html, "name=\"Version\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        values["__RequestVerificationToken"] = Token(html);
        var save = await client.PostAsync("/Admin/Translations/Save", Form(values));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Translations/Index?language=fr"));
        Assert.Contains("Questions sur notre mariage", page);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Admin/Translations/Save", Form(values))).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Perguntas Frequentes", (await db.FaqPageSettings.SingleAsync()).Title);
    }

    [Fact]
    public async Task ChangingLanguageReusesTheVerifiedRsvpEditSession()
    {
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var guest = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", Family = new() { Name = "Silva" } };
            db.Guests.Add(guest);
            await db.SaveChangesAsync();
            id = guest.Id;
            var verification = scope.ServiceProvider.GetRequiredService<RsvpVerificationService>();
            var challenge = await verification.RequestCodeAsync(id, RsvpVerificationChannel.Email, "ana@example.test", "test");
            var access = await verification.VerifyCodeAsync(challenge, sender.Code);
            client.DefaultRequestHeaders.Add("Cookie", "Casamento.RsvpAccess=" + access.Token);
        }
        var original = WebUtility.HtmlDecode(await client.GetStringAsync("/Rsvp/Select/" + id));
        var editId = Regex.Match(original, "name=\"EditSessionId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(editId);
        var returnUrl = Regex.Match(original, "name=\"returnUrl\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Contains(editId, returnUrl);
        var change = await Change(client, "fr", returnUrl);
        var translated = WebUtility.HtmlDecode(await client.GetStringAsync(change.Headers.Location));
        Assert.Contains("Contact vérifié", translated);
        Assert.Contains("value=\"" + editId + "\"", translated);
        Assert.Contains("value=\"Attending\"", translated);
        using var check = app.Services.CreateScope();
        Assert.Single(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().RsvpEditSessions.ToListAsync());
    }

    [Fact]
    public void SatelliteResourcesAreCompleteAndFormatsAreValid()
    {
        var manager = new ResourceManager("CasamentoTatianaDiogo.Resources.UiResource", typeof(LocalizedText).Assembly);
        var original = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var originalKeys = original.Cast<System.Collections.DictionaryEntry>().Select(x => (string)x.Key).ToHashSet();
        Assert.True(originalKeys.Count > 900);
        foreach (var culture in new[] { "en", "fr" })
        {
            var resource = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, false)!;
            Assert.NotNull(resource);
            Assert.True(originalKeys.SetEquals(resource.Cast<System.Collections.DictionaryEntry>().Select(x => (string)x.Key)));
            foreach (System.Collections.DictionaryEntry entry in resource) CompositeFormat.Parse((string)entry.Value!);
        }
        Assert.All(ApplicationLanguages.Cultures, culture => Assert.Equal("€", culture.NumberFormat.CurrencySymbol));
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("en")]
    [InlineData("fr")]
    public async Task BudgetAmountsKeepTheirValueAndEuroCurrencyInEveryLanguage(string language)
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        await Change(client, language, "/Admin/Dashboard/Index");
        int categoryId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new PlanningCategory { Name = "Música", NormalizedName = "MUSICA" };
            db.PlanningCategories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }
        const string root = "/Admin/Planeamento/";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(root + "Fornecedores/Ficha"));
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Token(html)), new("CategoryId", categoryId.ToString()),
            new("Name", "Músicos"), new("Price", "240.50"), new("Status", Supplier.States[0])
        };
        foreach (Match field in Regex.Matches(html, "name=\"__Invariant\"[^>]*value=\"([^\"]+)\"")) fields.Add(new("__Invariant", field.Groups[1].Value));
        var save = await client.PostAsync(root + "Fornecedores/Guardar", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        using var check = app.Services.CreateScope();
        Assert.Equal(240.50m, (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Suppliers.SingleAsync()).Price);
        var list = WebUtility.HtmlDecode(await client.GetStringAsync(root + "Fornecedores"));
        Assert.Contains("€", list);
        Assert.DoesNotContain("£", list);
        Assert.Contains(language == "en" ? "240.50" : "240,50", list);
    }

    [Theory]
    [InlineData("en", "Create")]
    [InlineData("fr", "Créer")]
    public async Task TranslatedImportPreviewKeepsThePortugueseSpreadsheetContract(string language, string create)
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        await Change(client, language, "/Admin/ExcelImport/Families");
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add(ExcelImportService.Sheet("families"));
        var columns = ExcelImportService.Columns("families");
        for (var i = 0; i < columns.Length; i++) sheet.Cell(1, i + 1).Value = columns[i];
        sheet.Cell(2, Array.IndexOf(columns, "Nome") + 1).Value = "Família Silva";
        sheet.Cell(2, Array.IndexOf(columns, "Referência") + 1).Value = "F01";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(await HttpTests.AntiforgeryToken(client, "/Admin/ExcelImport/Families")), "__RequestVerificationToken");
        body.Add(new ByteArrayContent(stream.ToArray()), "File", "families.xlsx");
        var upload = await client.PostAsync("/Admin/ExcelImport/ImportFamilies", body);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var preview = WebUtility.HtmlDecode(await client.GetStringAsync(upload.Headers.Location));
        Assert.Contains("Família Silva", preview);
        Assert.Contains(create, preview);
        Assert.Contains("<strong class=\"d-block fs-2\">1</strong>", preview);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var draft = await db.AdminImportDrafts.SingleAsync();
        var confirm = await client.PostAsync("/Admin/ExcelImport/Confirm", Form(new()
        {
            ["id"] = draft.Id.ToString(), ["revision"] = draft.Revision.ToString(), ["__RequestVerificationToken"] = Token(preview)
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal("Família Silva", (await db.Families.SingleAsync()).Name);
    }

    [Fact]
    public async Task RecoveryPageCanRenderInFrenchWhenDatabaseSchemaIsUnavailable()
    {
        using var app = new HttpTests.TestApplication(initializeSchema: false);
        using var client = app.Client();
        var response = await client.GetAsync("/?culture=fr");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("lang=\"fr-FR\"", html);
        Assert.Contains("Un problème est survenu", html);
    }

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);
    private static async Task<HttpResponseMessage> Change(HttpClient client, string language, string returnUrl) => await client.PostAsync("/Language/Change", Form(new()
    {
        ["language"] = language, ["returnUrl"] = returnUrl, ["__RequestVerificationToken"] = await HttpTests.AntiforgeryToken(client, "/Rsvp")
    }));
}

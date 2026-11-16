using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class ContactFormatTests
{
    [Fact]
    public async Task AdminCanConfigureFormatsAndSearchTheFormattedGuestContact()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settingsId = await db.WeddingSettings.Select(x => x.Id).SingleAsync();
        var family = new Family { Name = "Costa", GroupCode = "C01" };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var token = await HttpTests.AntiforgeryToken(client, "/Admin/Settings/Index");
        var fields = new Dictionary<string,string>
        {
            ["Id"] = settingsId.ToString(), ["PhoneRegion"] = "GB", ["ExtensionLabel"] = "ramal",
            ["PhoneDisplay"] = "national", ["EmailDomainDisplay"] = "unicode",
            ["DefaultEmailDomain"] = "bücher.de", ["__RequestVerificationToken"] = token
        };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(fields))).StatusCode);
        fields["PhoneRegion"] = "ZZ";
        var invalid = await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(fields));
        Assert.Contains("Escolhe um país da lista.", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        Assert.Equal("GB", await db.WeddingSettings.Select(x => x.PhoneRegion).SingleAsync());
        var guest = new Dictionary<string,string>
        {
            ["FirstName"] = "Inês", ["LastName"] = "Costa", ["DisplayName"] = "Inês Costa", ["FamilyId"] = family.Id.ToString(),
            ["Email"] = "Teste+RSVP", ["Phone"] = "020 7946 0018 x07", ["__RequestVerificationToken"] = token
        };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Admin/Guests/Save", new FormUrlEncodedContent(guest))).StatusCode);
        var saved = await db.Guests.AsNoTracking().SingleAsync();
        Assert.Equal("+442079460018;ext=07", saved.Phone);
        var filtered = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Guests/Index?familyId={family.Id}&q=ines%20ramal&status=Pending"));
        Assert.Contains(">Inês Costa</td>", filtered);
        Assert.Contains("Teste+RSVP@bücher.de", filtered);
        Assert.Contains("020 7946 0018 ramal 07", filtered);
        var empty = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Guests/Index?familyId={family.Id}&q=ines&status=Attending"));
        Assert.DoesNotContain(">Inês Costa</td>", empty);
    }

    [Theory]
    [InlineData("PT", "912 345 678 ramal 004", "+351912345678;ext=004")]
    [InlineData("GB", "(020) 7946 0018 ext. 12", "+442079460018;ext=12")]
    [InlineData("PT", "0044 20 7946 0018 x12", "+442079460018;ext=12")]
    [InlineData("PT", "+39 02 12345678", "+390212345678")]
    public void NationalAndForeignNumbersKeepTheirCountryAndExtension(string region, string input, string expected)
    {
        var settings = new WeddingSettings { PhoneRegion = region, ExtensionLabel = "ramal" };
        var parsed = ContactFormats.Phone(input, settings)!;
        Assert.Equal(expected, parsed.Stored);
        Assert.Equal("tel:" + expected, parsed.Link);
        Assert.Equal(parsed, ContactFormats.Phone(parsed.Stored, settings));
        if (expected.Contains(';')) Assert.Contains(" ramal ", parsed.Display);
        Assert.Null(ContactFormats.Phone("912345678 ext. abc", settings));
    }

    [Fact]
    public void InternationalEmailAndAliasesRoundTripWithoutChangingTheMailbox()
    {
        var settings = new WeddingSettings { DefaultEmailDomain = "BÜCHER.de" };
        var email = ContactFormats.Email(" Ana+Festa ", settings, completeDomain: true)!;
        Assert.Equal("Ana+Festa@xn--bcher-kva.de", email.Stored);
        Assert.Equal("Ana+Festa@bücher.de", email.Display);
        Assert.Equal("mailto:Ana%2BFesta@xn--bcher-kva.de", email.Link);
        settings.EmailDomainDisplay = "ascii";
        Assert.Equal(email.Stored, ContactFormats.Email(email.Stored, settings)!.Display);
        Assert.Null(ContactFormats.Email("Ana", settings));
        Assert.Null(ContactFormats.Email("Ana@bad domain.pt", settings));
        Assert.Null(ContactFormats.Email("A\nB@example.org", settings));
        Assert.Equal(RsvpContact.Normalize(RsvpVerificationChannel.Email, email.Display),
            RsvpContact.Normalize(RsvpVerificationChannel.Email, email.Stored));
    }

    [Fact]
    public async Task ConfiguredFormatsReachPublicLinksAndTheGuestImportPreview()
    {
        using var app = new HttpTests.TestApplication();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.WeddingSettings.SingleAsync();
        settings.PhoneRegion = "GB";
        settings.PhoneDisplay = "national";
        settings.ExtensionLabel = "ramal";
        settings.DefaultEmailDomain = "bücher.de";
        settings.BridePhone = "020 7946 0018 ext. 012";
        settings.BrideEmail = "Ana+Teste@xn--bcher-kva.de";
        db.Families.Add(new() { Name = "Costa", GroupCode = "F01" });
        await db.SaveChangesAsync();
        using var client = app.Client();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Information"));
        Assert.Contains("tel:+442079460018;ext=012", html);
        Assert.Contains("020 7946 0018 ramal 012", html);
        Assert.Contains("Ana+Teste@bücher.de", html);
        var importer = scope.ServiceProvider.GetRequiredService<ExcelImportService>();
        var rows = new List<ImportInputRow> { new() { Number = 2, Values = new()
        {
            ["Nome"] = "Ana", ["Apelido"] = "Costa", ["Referência da família"] = "F01",
            ["Telefone"] = settings.BridePhone, ["E-mail"] = "Ana+Teste"
        } } };
        var draft = await importer.CreateDraftAsync("guests", "test", rows);
        var preview = ExcelImportService.Preview(draft).Plan.Single();
        Assert.Empty(preview.Errors);
        Assert.Equal("+442079460018;ext=012", preview.After["Telefone"]);
        Assert.Equal(settings.BrideEmail, preview.After["E-mail"]);
        Assert.True(await importer.ConfirmAsync(draft));
        Assert.Equal(preview.After["Telefone"], (await db.Guests.SingleAsync()).Phone);
    }
}

using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class RsvpVerificationHttpTests
{
    [Fact]
    public async Task BrowserFlowRequiresEmailCodeBeforeReadingAndSavingAndLocksTheChosenContact()
    {
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        var (guest, other) = await Seed(app);
        int selectedRelative;
        int unselectedRelative;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var familyId = (await db.Guests.FindAsync(guest))!.FamilyId;
            var first = new Guest { FamilyId = familyId, FirstName = "Bruno", LastName = "Silva", DisplayName = "Bruno Silva" };
            var second = new Guest { FamilyId = familyId, FirstName = "Carlos", LastName = "Silva", DisplayName = "Carlos Silva" };
            db.AddRange(first, second);
            await db.SaveChangesAsync();
            unselectedRelative = first.Id;
            selectedRelative = second.Id;
        }
        var denied = await client.GetAsync($"/Rsvp/Select/{guest}");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Rsvp/Contact", denied.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Rsvp/SendCode", Form(new() { ["GuestId"] = guest.ToString() }))).StatusCode);
        var contactHtml = await client.GetStringAsync($"/Rsvp/Contact/{guest}");
        Assert.Contains("type=\"email\"", contactHtml);
        Assert.DoesNotContain("SMS", contactHtml);
        Assert.DoesNotContain("name=\"Channel\"", contactHtml);
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        var send = await client.PostAsync("/Rsvp/SendCode", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Contact"] = "ana@example.test", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, send.StatusCode);
        Assert.Single(sender.Messages);
        var cookies = send.Headers.GetValues("Set-Cookie");
        Assert.Contains(cookies, x => x.StartsWith("Casamento.RsvpChallenge=") && x.Contains("secure", StringComparison.OrdinalIgnoreCase) && x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var verifyHtml = await client.GetStringAsync("/Rsvp/Verify");
        Assert.DoesNotContain("value=\"" + sender.Code + "\"", verifyHtml);
        Assert.DoesNotContain(sender.Messages.Single().Contact, verifyHtml);
        csrf = await HttpTests.AntiforgeryToken(client, "/Rsvp/Verify");
        var verify = await client.PostAsync("/Rsvp/Verify", Form(new()
        {
            ["Code"] = sender.Code, ["GuestId"] = other.ToString(), ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, verify.StatusCode);
        // Client-supplied IDs cannot move a verified session to another invitation.
        Assert.EndsWith("/" + guest, verify.Headers.Location!.OriginalString);
        var selection = await client.GetAsync($"/Rsvp/Select/{guest}");
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        Assert.Contains("no-store", selection.Headers.CacheControl!.ToString());
        var selectionHtml = WebUtility.HtmlDecode(await selection.Content.ReadAsStringAsync());
        Assert.Contains("Contacto verificado", selectionHtml);
        Assert.Contains("name=\"FamilyResponses.Index\"", selectionHtml);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/Rsvp/Select/{other}")).StatusCode);
        csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Select/{guest}");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Rsvp/Submit", Form(new() { ["GuestId"] = guest.ToString(), ["Status"] = "Attending" }))).StatusCode);
        var forged = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["GuestId"] = other.ToString(), ["Status"] = "Attending", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, forged.StatusCode);
        var save = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Status"] = "Attending", ["Message"] = "Até breve!", ["__RequestVerificationToken"] = csrf,
            ["FamilyResponses.Index"] = "1", ["FamilyResponses[1].GuestId"] = selectedRelative.ToString(), ["FamilyResponses[1].Status"] = "Attending"
        }));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/Rsvp?submitted=True", save.Headers.Location!.OriginalString);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await db.RsvpResponses.CountAsync());
            Assert.False(await db.RsvpResponses.AnyAsync(x => x.GuestId == unselectedRelative));
            Assert.Equal("Até breve!", (await db.RsvpResponses.SingleAsync(x => x.GuestId == guest)).Message);
            Assert.Single(db.RsvpInvitationContacts);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/Rsvp/Select/{guest}")).StatusCode);
        csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        var change = await client.PostAsync("/Rsvp/SendCode", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Contact"] = "different@example.test", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Contains("Não é possível usar este contacto", WebUtility.HtmlDecode(await change.Content.ReadAsStringAsync()));
        Assert.Single(sender.Messages);
    }

    [Fact]
    public async Task ExistingResponsesCanBeUpdatedOnlyAfterVerifyingTheOriginalContact()
    {
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        var (guest, _) = await Seed(app);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var person = (await db.Guests.FindAsync(guest))!;
            db.RsvpInvitationContacts.Add(new() { FamilyId = person.FamilyId, Channel = RsvpVerificationChannel.Email, NormalizedContact = "ana@example.test", ConfirmedAt = DateTime.UtcNow });
            db.RsvpResponses.Add(new() { GuestId = guest, Status = RsvpStatus.Attending, Message = "Mensagem anterior", SubmittedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        await client.PostAsync("/Rsvp/SendCode", Form(new() { ["GuestId"] = guest.ToString(), ["Contact"] = "ANA@EXAMPLE.TEST", ["__RequestVerificationToken"] = csrf }));
        csrf = await HttpTests.AntiforgeryToken(client, "/Rsvp/Verify");
        var wrong = await client.PostAsync("/Rsvp/Verify", Form(new() { ["Code"] = "wrong", ["__RequestVerificationToken"] = csrf }));
        Assert.Contains("O código é inválido", WebUtility.HtmlDecode(await wrong.Content.ReadAsStringAsync()));
        var verified = await client.PostAsync("/Rsvp/Verify", Form(new() { ["Code"] = sender.Code, ["__RequestVerificationToken"] = csrf }));
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        var html = await client.GetStringAsync($"/Rsvp/Select/{guest}");
        Assert.Contains("Mensagem anterior", html);
        csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Select/{guest}");
        var invalid = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Message"] = "Manter este texto", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Manter este texto", await invalid.Content.ReadAsStringAsync());
        var saved = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Status"] = "NotAttending", ["ConfirmOverwrite"] = "true", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var final = app.Services.CreateScope();
        var finalDb = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(RsvpStatus.NotAttending, (await finalDb.RsvpResponses.SingleAsync()).Status);
        Assert.Equal("ana@example.test", (await finalDb.RsvpInvitationContacts.SingleAsync()).NormalizedContact);
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);
    private static async Task<(int Guest, int Other)> Seed(HttpTests.TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var guest = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", Family = new() { Name = "Silva" } };
        var other = new Guest { FirstName = "Eva", LastName = "Costa", DisplayName = "Eva Costa", Family = new() { Name = "Costa" } };
        db.AddRange(guest, other);
        await db.SaveChangesAsync();
        return (guest.Id, other.Id);
    }
}

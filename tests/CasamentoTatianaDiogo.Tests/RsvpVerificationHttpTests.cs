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
        var time = new MutableTime();
        using var app = new HttpTests.TestApplication(verificationSender: sender, time: time);
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
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var sentHtml = await send.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Code\"", sentHtml);
        Assert.Contains("action=\"/Rsvp/Verify\"", sentHtml);
        Assert.DoesNotContain("data-send-code", sentHtml);
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
        var familyField = $"FamilyResponses[{selectedRelative}]";
        var editSessionId = await EditId(client, guest);
        var incomplete = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["EditSessionId"] = editSessionId,
            ["GuestId"] = guest.ToString(), ["Status"] = "Attending", ["Message"] = "Até breve!", ["__RequestVerificationToken"] = csrf,
            ["FamilyResponses.Index"] = selectedRelative.ToString(), [familyField + ".GuestId"] = selectedRelative.ToString(),
            [familyField + ".Message"] = "Vamos juntos", [familyField + ".Status"] = ""
        }));
        Assert.Equal(HttpStatusCode.OK, incomplete.StatusCode);
        var redisplayed = WebUtility.HtmlDecode(await incomplete.Content.ReadAsStringAsync());
        Assert.Contains("Escolhe se este familiar vai estar presente.", redisplayed);
        Assert.Contains("Até breve!", redisplayed);
        Assert.Contains("Vamos juntos", redisplayed);
        Assert.Matches($"id=\"family-guest-toggle-{selectedRelative}\"[^>]*checked", redisplayed);
        Assert.DoesNotMatch($"id=\"family-guest-toggle-{unselectedRelative}\"[^>]*checked", redisplayed);
        var save = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["EditSessionId"] = editSessionId,
            ["GuestId"] = guest.ToString(), ["Status"] = "Attending", ["Message"] = "Até breve!", ["__RequestVerificationToken"] = csrf,
            ["FamilyResponses.Index"] = selectedRelative.ToString(), [familyField + ".GuestId"] = selectedRelative.ToString(),
            [familyField + ".Message"] = "Vamos juntos", [familyField + ".Status"] = "Attending"
        }));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/Rsvp/Receipt", save.Headers.Location!.OriginalString);
        Assert.Contains(save.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Casamento.RsvpReceipt=") &&
            x.Contains("secure", StringComparison.OrdinalIgnoreCase) && x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var receiptResponse = await client.GetAsync("/Rsvp/Receipt");
        Assert.Contains("no-store", receiptResponse.Headers.CacheControl!.ToString());
        var receipt = WebUtility.HtmlDecode(await receiptResponse.Content.ReadAsStringAsync());
        Assert.Contains("O teu recibo RSVP", receipt);
        Assert.Contains("Até breve!", receipt);
        Assert.Contains("Vamos juntos", receipt);
        Assert.Contains("Carlos Silva", receipt);
        Assert.DoesNotContain("Bruno Silva", receipt);
        Assert.DoesNotContain("Eva Costa", receipt);
        Assert.DoesNotContain("ana@example.test", receipt);
        Assert.Contains("data-print-receipt", receipt);
        using (var anonymous = app.Client())
        {
            var deniedReceipt = await anonymous.GetAsync($"/Rsvp/Receipt?id={guest}");
            Assert.Equal(HttpStatusCode.Gone, deniedReceipt.StatusCode);
            Assert.DoesNotContain("Até breve!", await deniedReceipt.Content.ReadAsStringAsync());
        }
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await db.RsvpResponses.CountAsync());
            Assert.False(await db.RsvpResponses.AnyAsync(x => x.GuestId == unselectedRelative));
            Assert.Equal("Vamos juntos", (await db.RsvpResponses.SingleAsync(x => x.GuestId == selectedRelative)).Message);
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

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.RsvpResponses.SingleAsync(x => x.GuestId == guest)).Message = "Alteração posterior";
            (await db.WeddingSettings.SingleAsync()).RsvpDeadline = new DateOnly(2020, 1, 1);
            await db.SaveChangesAsync();
        }
        var afterDeadline = WebUtility.HtmlDecode(await client.GetStringAsync("/Rsvp/Receipt"));
        Assert.Contains("Até breve!", afterDeadline);
        Assert.DoesNotContain("Alteração posterior", afterDeadline);
        Assert.Contains("Confirmações de presença encerradas", WebUtility.HtmlDecode(await client.GetStringAsync($"/Rsvp/Contact/{guest}")));
        time.Advance(TimeSpan.FromHours(24));
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync("/Rsvp/Receipt")).StatusCode);
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
        var editSessionId = await EditId(client, guest);
        csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Select/{guest}");
        var invalid = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["EditSessionId"] = editSessionId,
            ["GuestId"] = guest.ToString(), ["Message"] = "Manter este texto", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Manter este texto", await invalid.Content.ReadAsStringAsync());
        var saved = await client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["EditSessionId"] = editSessionId,
            ["GuestId"] = guest.ToString(), ["Status"] = "NotAttending", ["ConfirmOverwrite"] = "true", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var final = app.Services.CreateScope();
        var finalDb = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(RsvpStatus.NotAttending, (await finalDb.RsvpResponses.SingleAsync()).Status);
        Assert.Equal("ana@example.test", (await finalDb.RsvpInvitationContacts.SingleAsync()).NormalizedContact);
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    [Fact]
    public async Task CodeFieldAppearsImmediatelyInsideMusicPageAndResumesUntilExpiry()
    {
        var sender = new RecordingVerificationSender();
        var time = new MutableTime();
        using var app = new HttpTests.TestApplication(verificationSender: sender, time: time);
        using var client = app.Client();
        var (guest, other) = await Seed(app);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.WeddingSettings.SingleAsync()).SpotifyTrackUrl = "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT";
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "iframe");
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        Task<HttpResponseMessage> Send() => client.PostAsync("/Rsvp/SendCode", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Contact"] = "ana@example.test", ["__RequestVerificationToken"] = csrf
        }));
        var sent = await Send();
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var html = WebUtility.HtmlDecode(await sent.Content.ReadAsStringAsync());
        Assert.Contains("Código enviado.", html);
        Assert.Contains("name=\"Code\"", html);
        Assert.Contains("Verificar e continuar", html);
        Assert.DoesNotContain("id=\"guest-page\"", html);
        Assert.DoesNotContain("ana@example.test", html);
        Assert.DoesNotContain("value=\"" + sender.Code + "\"", html);
        Assert.Contains("name=\"Code\"", await client.GetStringAsync($"/Rsvp/Contact/{guest}"));
        Assert.Contains("data-send-code", await client.GetStringAsync($"/Rsvp/Contact/{guest}?changeContact=true"));
        Assert.DoesNotContain("name=\"Code\"", await client.GetStringAsync($"/Rsvp/Contact/{other}"));
        var repeated = await Send();
        Assert.Contains("name=\"Code\"", await repeated.Content.ReadAsStringAsync());
        Assert.Single(sender.Messages);
        Assert.Equal("/Rsvp/Verify", (await client.GetAsync("/Rsvp/SendCode")).Headers.Location!.OriginalString);
        time.Advance(TimeSpan.FromMinutes(11));
        Assert.DoesNotContain("name=\"Code\"", await client.GetStringAsync($"/Rsvp/Contact/{guest}"));
    }

    [Fact]
    public async Task DisplayedChallengeSurvivesAnotherTabAndIncorrectCode()
    {
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        var (guest, other) = await Seed(app);
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        async Task<string> Send(int id, string email)
        {
            var response = await client.PostAsync("/Rsvp/SendCode", Form(new()
            {
                ["GuestId"] = id.ToString(), ["Contact"] = email, ["__RequestVerificationToken"] = csrf
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }
        var firstHtml = await Send(guest, "ana@example.test");
        var firstCode = sender.Code;
        var challenge = System.Text.RegularExpressions.Regex.Match(firstHtml,
            "name=\"ChallengeToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(challenge);
        await Send(other, "eva@example.test");
        Task<HttpResponseMessage> Verify(string token, string code) => client.PostAsync("/Rsvp/Verify", Form(new()
        {
            ["ChallengeToken"] = token, ["Code"] = code, ["GuestId"] = other.ToString(), ["__RequestVerificationToken"] = csrf
        }));
        // Invalid posted tokens must not silently verify the invitation in the cookie.
        var forged = await Verify("invalid", sender.Code);
        Assert.Equal("/Rsvp", forged.Headers.Location!.OriginalString);
        var wrong = await Verify(challenge, "wrong");
        var wrongHtml = WebUtility.HtmlDecode(await wrong.Content.ReadAsStringAsync());
        Assert.Contains("O código é inválido", wrongHtml);
        Assert.Contains(challenge, wrongHtml);
        var verified = await Verify(challenge, firstCode);
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.EndsWith("/" + guest, verified.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Rsvp/Select/{guest}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/Rsvp/Select/{other}")).StatusCode);
    }

    [Fact]
    public async Task DeliveryFailurePreservesEmailAndDoesNotClaimCodeWasSent()
    {
        var sender = new RecordingVerificationSender { Fail = true };
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        var (guest, _) = await Seed(app);
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
        var response = await client.PostAsync("/Rsvp/SendCode", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["Contact"] = "ana@example.test", ["__RequestVerificationToken"] = csrf
        }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-send-code", html);
        Assert.Contains("value=\"ana@example.test\"", html);
        Assert.DoesNotContain("name=\"Code\"", html);
        Assert.DoesNotContain("Código enviado.", html);
    }

    [Fact]
    public async Task ConflictShowsEnteredValuesAndCannotBeBypassedByOverwriteCheckbox()
    {
        var sender = new RecordingVerificationSender();
        var time = new MutableTime();
        using var app = new HttpTests.TestApplication(verificationSender: sender, time: time);
        using var first = app.Client();
        using var second = app.Client();
        var (guest, _) = await Seed(app);
        async Task<(string Csrf, string Edit)> Verify(HttpClient client)
        {
            var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{guest}");
            await client.PostAsync("/Rsvp/SendCode", Form(new() { ["GuestId"] = guest.ToString(), ["Contact"] = "ana@example.test", ["__RequestVerificationToken"] = csrf }));
            csrf = await HttpTests.AntiforgeryToken(client, "/Rsvp/Verify");
            await client.PostAsync("/Rsvp/Verify", Form(new() { ["Code"] = sender.Code, ["__RequestVerificationToken"] = csrf }));
            return (csrf, await EditId(client, guest));
        }
        var a = await Verify(first);
        time.Advance(TimeSpan.FromMinutes(1));
        var b = await Verify(second);
        Task<HttpResponseMessage> Save(HttpClient client, string csrf, string edit, string message) => client.PostAsync("/Rsvp/Submit", Form(new()
        {
            ["GuestId"] = guest.ToString(), ["EditSessionId"] = edit, ["Status"] = "Attending",
            ["Message"] = message, ["ConfirmOverwrite"] = "true", ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsync("/Rsvp/EditStatus", Form(new()))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Save(first, a.Csrf, a.Edit, "Resposta mais recente")).StatusCode);
        var conflict = await Save(second, b.Csrf, b.Edit, "O meu texto por guardar");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var html = WebUtility.HtmlDecode(await conflict.Content.ReadAsStringAsync());
        Assert.Contains("O meu texto por guardar", html);
        Assert.Contains("Rever resposta mais recente", html);
        Assert.Contains($"name=\"EditSessionId\" value=\"{b.Edit}\"", html);
        Assert.Matches("data-rsvp-submit[^>]*disabled", html);
        Assert.Equal(HttpStatusCode.Conflict, (await Save(second, b.Csrf, b.Edit, "Tentar outra vez")).StatusCode);
        var refreshed = await EditId(second, guest);
        Assert.Equal(HttpStatusCode.Redirect, (await Save(second, b.Csrf, refreshed, "Resposta revista")).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal("Resposta revista", await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RsvpResponses.Select(x => x.Message).SingleAsync());
    }
    private static async Task<string> EditId(HttpClient client, int guest) => System.Text.RegularExpressions.Regex.Match(
        await client.GetStringAsync($"/Rsvp/Select/{guest}"), "name=\"EditSessionId\" value=\"([^\"]+)\"").Groups[1].Value;
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

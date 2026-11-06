using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class RsvpDeadlineTests
{
    [Fact]
    public async Task DeadlineIncludesTheWholeLisbonDayAndBlocksEveryGuestEntryPoint()
    {
        // Lisbon is UTC+1 in June.
        var time = new DeadlineTime(new DateTimeOffset(2027, 6, 5, 22, 59, 59, TimeSpan.Zero));
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender, time: time);
        using var client = app.Client();
        int guestId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.WeddingSettings.SingleAsync()).RsvpDeadline = new DateOnly(2027, 6, 5);
            var guest = new Guest { DisplayName = "Ana Silva", FirstName = "Ana", Family = new() { Name = "Silva" } };
            db.Guests.Add(guest);
            await db.SaveChangesAsync();
            guestId = guest.Id;
        }
        Assert.Contains("guest-search", await client.GetStringAsync("/Rsvp"));
        var token = await HttpTests.AntiforgeryToken(client, "/Rsvp/Contact/" + guestId);
        async Task<HttpResponseMessage> Post(string action, params (string Key, string Value)[] fields) =>
            await client.PostAsync("/Rsvp/" + action, new FormUrlEncodedContent(
                fields.Append(("__RequestVerificationToken", token)).Select(pair =>
                    new KeyValuePair<string, string>(pair.Item1, pair.Item2))));
        Assert.Equal(HttpStatusCode.Redirect, (await Post("SendCode", ("GuestId", guestId.ToString()), ("Contact", "ana@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post("Verify", ("Code", sender.Code))).StatusCode);
        Assert.Contains("guest-status", await client.GetStringAsync("/Rsvp/Select/" + guestId));

        time.Advance(TimeSpan.FromSeconds(1));
        foreach (var route in new[] { "/Rsvp", "/Rsvp/Index", "/Rsvp/Contact/" + guestId, "/Rsvp/Verify", "/Rsvp/Select/" + guestId })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Confirmações de presença encerradas", html);
            Assert.Contains("05/06/2027", html);
            Assert.DoesNotContain("guest-status", html);
        }
        foreach (var action in new[] { "Search", "SendCode", "Verify", "Submit", "EditStatus" })
        {
            var response = await Post(action, ("GuestId", guestId.ToString()), ("Status", "Attending"),
                ("Contact", "ana@example.test"), ("Query", "Ana"), ("Code", sender.Code));
            Assert.Contains("Confirmações de presença encerradas", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
        using var finalScope = app.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await finalDb.RsvpResponses.ToListAsync());
        Assert.Empty(await finalDb.RsvpInvitationContacts.ToListAsync());
        Assert.Single(sender.Messages);

        // Removing the deadline reopens the public module on the next request.
        (await finalDb.WeddingSettings.SingleAsync()).RsvpDeadline = null;
        await finalDb.SaveChangesAsync();
        Assert.Contains("guest-search", await client.GetStringAsync("/Rsvp"));
    }

    [Fact]
    public async Task AdministratorCanConfigureAndRemoveDeadlineWhileRsvpIsClosed()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "deadline@example.test", Email = "deadline@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "DeadlineTest!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var token = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["email"] = admin.Email, ["password"] = "DeadlineTest!2027", ["__RequestVerificationToken"] = token
        }));
        token = await HttpTests.AntiforgeryToken(client, "/Admin/Settings/Index");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settingsId = await db.WeddingSettings.Select(s => s.Id).SingleAsync();
        foreach (var date in new[] { "2020-06-05", "" })
        {
            var response = await client.PostAsync("/Admin/Settings/Index", new FormUrlEncodedContent(new Dictionary<string,string>
            {
                ["Id"] = settingsId.ToString(), ["RsvpDeadline"] = date, ["__Invariant"] = "RsvpDeadline",
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(date == "" ? null : new DateOnly(2020, 6, 5),
                await db.WeddingSettings.AsNoTracking().Select(s => s.RsvpDeadline).SingleAsync());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/Rsvp/Index")).StatusCode);
        }
    }

    private sealed class DeadlineTime(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan interval) => current += interval;
    }
}

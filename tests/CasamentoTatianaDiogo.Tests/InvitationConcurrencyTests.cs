using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class InvitationConcurrencyTests
{
    [Fact]
    public async Task AdminCannotSaveAStaleInvitationOrDeleteAResponseFromAStaleList()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole("Admin"));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "concurrency@example.test", Email = "concurrency@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "Concurrency!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["email"] = admin.Email, ["password"] = "Concurrency!2027", ["__RequestVerificationToken"] = csrf }));
        var db = services.GetRequiredService<ApplicationDbContext>();
        var family = new Family { Name = "Original", GroupCode = "F01", RsvpVersion = Guid.NewGuid() };
        var guest = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", Family = family, CurrentStatus = RsvpStatus.Attending };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        db.RsvpResponses.Add(new() { GuestId = guest.Id, Status = RsvpStatus.Attending });
        await db.SaveChangesAsync();
        var version = family.RsvpVersion;
        var html = await client.GetStringAsync($"/Admin/InvitationGroups/Edit/{family.Id}");
        Assert.Contains(version.ToString(), html);
        csrf = await HttpTests.AntiforgeryToken(client, $"/Admin/InvitationGroups/Edit/{family.Id}");
        Task<HttpResponseMessage> Save(string name) => client.PostAsync("/Admin/InvitationGroups/Save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = family.Id.ToString(), ["Name"] = name, ["GroupCode"] = "F01", ["AllowGroupRsvp"] = "true",
            ["RsvpVersion"] = version.ToString(), ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, (await Save("Mais recente")).StatusCode);
        var conflict = await Save("Nome antigo por guardar");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("Nome antigo por guardar", await conflict.Content.ReadAsStringAsync());
        Assert.Equal("Mais recente", await db.Families.AsNoTracking().Select(x => x.Name).SingleAsync());
        Task<HttpResponseMessage> Delete(Guid v) => client.PostAsync($"/Admin/Rsvp/Delete/{guest.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["rsvpVersion"] = v.ToString(), ["__RequestVerificationToken"] = csrf }));
        await Delete(version);
        Assert.Single(await db.RsvpResponses.ToListAsync());
        var currentVersion = await db.Families.AsNoTracking().Select(x => x.RsvpVersion).SingleAsync();
        var list = await client.GetStringAsync("/Admin/Rsvp/Index");
        Assert.Contains(currentVersion.ToString(), list);
        await Delete(currentVersion);
        Assert.Empty(await db.RsvpResponses.ToListAsync());
        Assert.NotEqual(currentVersion, await db.Families.AsNoTracking().Select(x => x.RsvpVersion).SingleAsync());
    }
}

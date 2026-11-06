using System.Net;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ClosedXML.Excel;

namespace CasamentoTatianaDiogo.Tests;

public class GuestCompanionTests
{
    [Fact]
    public async Task AdminManagesCompanionsInGuestsAndRejectsInvalidAssociations()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        var (mainId, familyId) = await Seed(app);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin/Guests/Create")).StatusCode);
        await Login(app, client);
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Guests/Create");
        async Task<HttpResponseMessage> Save(Dictionary<string,string> fields)
        {
            fields["__RequestVerificationToken"] = csrf;
            return await client.PostAsync("/Admin/Guests/Save", new FormUrlEncodedContent(fields));
        }
        Dictionary<string,string> Person() => new()
        {
            ["FirstName"] = "Eva", ["LastName"] = "Costa", ["DisplayName"] = "Eva Costa",
            ["FamilyId"] = familyId.ToString(), ["IsPlusOne"] = "true", ["MainGuestId"] = mainId.ToString()
        };
        var invalid = Person(); invalid["AllowPlusOne"] = "true";
        Assert.Contains("Um Plus-One não pode levar", WebUtility.HtmlDecode(await (await Save(invalid)).Content.ReadAsStringAsync()));
        invalid = Person(); invalid.Remove("MainGuestId");
        Assert.Equal(HttpStatusCode.OK, (await Save(invalid)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Guests.ToListAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Save(Person())).StatusCode);
        var companion = await db.Guests.AsNoTracking().SingleAsync(x => x.IsPlusOne);
        Assert.False(companion.AllowPlusOne);
        Assert.Equal(mainId, companion.MainGuestId);
        Assert.Equal(companion.Id, (await db.PlusOnes.SingleAsync()).CompanionGuestId);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Guests/Index"));
        Assert.Contains("Plus-One de", html);
        Assert.Contains("Ana Silva", html);
        var dashboard = await client.GetStringAsync("/Admin/Dashboard/Index");
        Assert.DoesNotContain("/Admin/PlusOnes", dashboard);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Admin/PlusOnes/Create")).StatusCode);
        invalid = Person(); invalid["Id"] = companion.Id.ToString(); invalid["MainGuestId"] = companion.Id.ToString();
        Assert.Equal(HttpStatusCode.OK, (await Save(invalid)).StatusCode);
        invalid = Person(); invalid["Id"] = mainId.ToString(); invalid["MainGuestId"] = companion.Id.ToString();
        Assert.Equal(HttpStatusCode.OK, (await Save(invalid)).StatusCode);
        invalid = Person(); invalid["Id"] = mainId.ToString(); invalid["IsPlusOne"] = "false"; invalid.Remove("MainGuestId");
        Assert.Contains("tem acompanhantes associados", WebUtility.HtmlDecode(await (await Save(invalid)).Content.ReadAsStringAsync()));
        invalid = Person(); invalid["Id"] = companion.Id.ToString(); invalid["DisplayName"] = "Eva revista";
        Assert.Equal(HttpStatusCode.Redirect, (await Save(invalid)).StatusCode);
        Assert.Equal("Eva revista", await db.PlusOnes.AsNoTracking().Select(x => x.PlusOneDisplayName).SingleAsync());
        var edit = await client.GetStringAsync($"/Admin/Guests/Edit/{companion.Id}");
        Assert.Contains("name=\"IsPlusOne\"", edit);
        Assert.Contains("name=\"MainGuestId\"", edit);
        Assert.DoesNotContain($"value=\"{companion.Id}\">Eva revista", edit);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/Guests/Save", new FormUrlEncodedContent(Person()))).StatusCode);
    }

    [Fact]
    public async Task LegacyConversionPreservesResponseAndRunsOnlyOnce()
    {
        using var app = new HttpTests.TestApplication();
        var (mainId, familyId) = await Seed(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = new PlusOne { MainGuestId = mainId, PlusOneFirstName = "Eva", PlusOneLastName = "Costa", PlusOneDisplayName = "Eva Costa" };
        db.PlusOnes.Add(old); await db.SaveChangesAsync();
        var response = new RsvpResponse { GuestId = mainId, Status = RsvpStatus.Attending, PlusOneId = old.Id, PlusOneAttending = true, PlusOneMessage = "Mensagem preservada" };
        db.RsvpResponses.Add(response); await db.SaveChangesAsync();
        await GuestCompanions.UpgradeExistingAsync(db);
        await GuestCompanions.UpgradeExistingAsync(db);
        Assert.Equal(2, await db.Guests.CountAsync());
        var companion = await db.Guests.SingleAsync(x => x.IsPlusOne);
        Assert.Equal(familyId, companion.FamilyId);
        Assert.Equal(RsvpStatus.Attending, companion.CurrentStatus);
        Assert.Equal(companion.Id, (await db.PlusOnes.SingleAsync()).CompanionGuestId);
        Assert.Equal(old.Id, response.PlusOneId);
        Assert.Equal("Mensagem preservada", response.PlusOneMessage);
        var errors = await GuestCompanions.ValidateAsync(db, new Guest { Id = companion.Id, FamilyId = familyId, IsPlusOne = false });
        Assert.Contains(errors, x => x.Message.Contains("Já existe uma resposta RSVP"));
    }

    [Fact]
    public async Task RsvpUsesLinkedGuestOnlyAsCompanionAndUpdatesItsStatus()
    {
        var sender = new RecordingVerificationSender();
        using var app = new HttpTests.TestApplication(verificationSender: sender);
        using var client = app.Client();
        var (mainId, familyId) = await Seed(app);
        int companionId;
        int linkId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var companion = new Guest { FirstName = "Eva", LastName = "Costa", DisplayName = "Eva Costa", FamilyId = familyId, IsPlusOne = true, MainGuestId = mainId };
            db.Guests.Add(companion); await db.SaveChangesAsync();
            await GuestCompanions.SyncAsync(db, companion); await db.SaveChangesAsync();
            companionId = companion.Id; linkId = await db.PlusOnes.Select(x => x.Id).SingleAsync();
            var rsvp = scope.ServiceProvider.GetRequiredService<IRsvpService>();
            Assert.Empty(await rsvp.SearchGuestsAsync("Eva"));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Rsvp/Contact/{companionId}")).StatusCode);
        var csrf = await HttpTests.AntiforgeryToken(client, $"/Rsvp/Contact/{mainId}");
        async Task<HttpResponseMessage> Post(string action, Dictionary<string,string> fields)
        {
            fields["__RequestVerificationToken"] = csrf;
            return await client.PostAsync("/Rsvp/" + action, new FormUrlEncodedContent(fields));
        }
        await Post("SendCode", new() { ["GuestId"] = mainId.ToString(), ["Contact"] = "ana@example.test" });
        await Post("Verify", new() { ["Code"] = sender.Code });
        var select = await client.GetStringAsync($"/Rsvp/Select/{mainId}");
        Assert.Contains("Eva Costa", select);
        Assert.DoesNotContain($"family-guest-toggle-{companionId}", select);
        var editId = Regex.Match(select, "name=\"EditSessionId\" value=\"([^\"]+)\"").Groups[1].Value;
        var forged = await Post("Submit", new()
        {
            ["GuestId"] = mainId.ToString(), ["EditSessionId"] = editId, ["Status"] = "Attending",
            ["FamilyResponses[0].GuestId"] = companionId.ToString(), ["FamilyResponses[0].Status"] = "Attending"
        });
        Assert.Contains("Só podes responder", WebUtility.HtmlDecode(await forged.Content.ReadAsStringAsync()));
        var result = await Post("Submit", new()
        {
            ["GuestId"] = mainId.ToString(), ["EditSessionId"] = editId, ["Status"] = "Attending",
            ["PlusOneId"] = linkId.ToString(), ["PlusOneAttending"] = "true", ["PlusOneMessage"] = "Olá!"
        });
        Assert.Equal("/Rsvp/Receipt", result.Headers.Location!.OriginalString);
        var receipt = WebUtility.HtmlDecode(await client.GetStringAsync("/Rsvp/Receipt"));
        Assert.Contains("Eva Costa", receipt); Assert.Contains("Olá!", receipt);
        using var final = app.Services.CreateScope();
        var finalDb = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(RsvpStatus.Attending, (await finalDb.Guests.FindAsync(companionId))!.CurrentStatus);
        Assert.Single(await finalDb.RsvpResponses.ToListAsync());
        await Login(app, client);
        using var workbook = new XLWorkbook(new MemoryStream(await client.GetByteArrayAsync("/Admin/Rsvp/ExportExcel")));
        Assert.Equal(2, workbook.Worksheet("Resumo").Cell(12, 2).GetValue<int>());
        Assert.Equal(1, workbook.Worksheet("Resumo").Cell(11, 2).GetValue<int>());
        var version = (await finalDb.Families.FindAsync(familyId))!.RsvpVersion;
        var adminCsrf = await HttpTests.AntiforgeryToken(client, "/Admin/Rsvp/Index");
        await client.PostAsync($"/Admin/Rsvp/Delete/{mainId}", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["rsvpVersion"] = version.ToString(), ["__RequestVerificationToken"] = adminCsrf
        }));
        Assert.Equal(RsvpStatus.Pending, await finalDb.Guests.AsNoTracking().Where(x => x.Id == companionId).Select(x => x.CurrentStatus).SingleAsync());
    }

    [Fact]
    public async Task DatabaseRejectsBothRolesEvenWithoutFormValidation()
    {
        using var app = new HttpTests.TestApplication();
        var (mainId, familyId) = await Seed(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Guests.Add(new Guest { FirstName = "Eva", LastName = "Costa", DisplayName = "Eva Costa",
            FamilyId = familyId, MainGuestId = mainId, IsPlusOne = true, AllowPlusOne = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.Guests.ToListAsync());
    }

    [Fact]
    public async Task ImportCannotGiveCompanionPermissionToBringSomeone()
    {
        using var app = new HttpTests.TestApplication();
        var (mainId, familyId) = await Seed(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var companion = new Guest { FirstName = "Eva", LastName = "Costa", DisplayName = "Eva Costa", FamilyId = familyId, IsPlusOne = true, MainGuestId = mainId };
        db.Guests.Add(companion); await db.SaveChangesAsync();
        await GuestCompanions.SyncAsync(db, companion); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<ExcelImportService>();
        var rows = new List<ImportInputRow> { new() { Number = 2, Values = new()
        {
            ["ID"] = companion.Id.ToString(), ["Nome"] = "Eva", ["Apelido"] = "Costa", ["Referência da família"] = "S01", ["Permitir acompanhante"] = "Sim"
        } } };
        var plan = await service.BuildPlanAsync("guests", rows);
        Assert.Contains(plan.Single().Errors, x => x.Contains("Um Plus-One não pode"));
        rows[0].Values["Permitir acompanhante"] = "Não";
        rows[0].Values["Nome apresentado"] = "Eva revista";
        var draft = await service.CreateDraftAsync("guests", "test-admin", rows);
        Assert.True(await service.ConfirmAsync(draft));
        Assert.Equal("Eva revista", await db.PlusOnes.Select(x => x.PlusOneDisplayName).SingleAsync());
        Assert.True((await db.Guests.FindAsync(companion.Id))!.IsPlusOne);
    }

    private static async Task<(int Main, int Family)> Seed(HttpTests.TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var main = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", AllowPlusOne = true, Family = new() { Name = "Silva", GroupCode = "S01", AllowGroupRsvp = true } };
        db.Guests.Add(main); await db.SaveChangesAsync();
        return (main.Id, main.FamilyId);
    }

    private static async Task Login(HttpTests.TestApplication app, HttpClient client)
    {
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword!2027")).Succeeded);
        await users.AddToRoleAsync(user, "Admin");
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["email"] = user.Email, ["password"] = "TestPassword!2027", ["__RequestVerificationToken"] = csrf
        }));
    }
}

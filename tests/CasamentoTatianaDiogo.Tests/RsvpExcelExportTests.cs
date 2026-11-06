using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class RsvpExcelExportTests
{
    [Fact]
    public async Task ExportIncludesStoredHistoryCompanionsTypedDatesAndFamilyFilter()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin/Rsvp/ExportExcel")).StatusCode);
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "export@example.test", Email = "export@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "ExportTest!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = admin.Email, ["password"] = "ExportTest!2027", ["__RequestVerificationToken"] = csrf
        }));
        using (var empty = new XLWorkbook(new MemoryStream(await client.GetByteArrayAsync("/Admin/Rsvp/ExportExcel"))))
            Assert.Equal(4, empty.Worksheets.Count);

        var db = services.GetRequiredService<ApplicationDbContext>();
        var guest = new Guest
        {
            FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", Email = "ana@example.test",
            Phone = "+351912345678", CurrentStatus = RsvpStatus.Attending, Notes = "Nota privada",
            Family = new() { Name = "Silva", GroupCode = "S01", Notes = "Família da noiva" }
        };
        var companion = new PlusOne { MainGuest = guest, PlusOneFirstName = "Rui", PlusOneLastName = "Silva", PlusOneDisplayName = "Rui Silva" };
        var submitted = new DateTime(2027, 6, 5, 11, 30, 0, DateTimeKind.Utc);
        db.AddRange(guest, companion, new Guest { DisplayName = "Outra família", Family = new() { Name = "Outra" } });
        await db.SaveChangesAsync();
        db.RsvpInvitationContacts.Add(new() { FamilyId = guest.FamilyId, NormalizedContact = "convite@example.test", ConfirmedAt = submitted });
        db.RsvpResponses.AddRange(
            new() { GuestId = guest.Id, SubmittedAt = submitted.AddDays(-1), Status = RsvpStatus.NotAttending, Message = "Primeiro registo" },
            new() { GuestId = guest.Id, SubmittedAt = submitted, UpdatedAt = submitted.AddHours(1),
                Status = RsvpStatus.Attending, DietaryRestrictions = "Sem glúten", Message = "=1+1", MusicRequest = "Canção",
                PlusOneId = companion.Id, PlusOneAttending = true, PlusOneMessage = "Até breve",
                PlusOneDietaryRestrictions = "Vegetariano", PlusOneMusicRequest = "Outra canção",
                SubmittedFromIp = "192.0.2.1", UserAgent = "Test browser" });
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/Admin/Rsvp/ExportExcel?familyId=" + guest.FamilyId);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var people = workbook.Worksheet("Convidados");
        var history = workbook.Worksheet("Respostas");
        var companions = workbook.Worksheet("Acompanhantes");
        Assert.Equal(2, people.LastRowUsed()!.RowNumber());
        Assert.Equal(3, history.LastRowUsed()!.RowNumber());
        Assert.Equal("convite@example.test", people.Cell(2, 16).GetString());
        Assert.Equal("Nota privada", people.Cell(2, 14).GetString());
        Assert.Equal("+351912345678", people.Cell(2, 11).GetString());
        Assert.Equal(12, history.Cell(2, 13).GetDateTime().Hour);
        Assert.Equal(11, history.Cell(2, 14).GetDateTime().Hour);
        Assert.Equal("Sim", history.Cell(2, 7).GetString());
        Assert.Equal("Não", history.Cell(3, 7).GetString());
        Assert.Equal("=1+1", history.Cell(2, 18).GetString());
        Assert.False(history.Cell(2, 18).HasFormula);
        Assert.Equal("192.0.2.1", history.Cell(2, 28).GetString());
        Assert.Equal("Vegetariano", companions.Cell(2, 12).GetString());
        Assert.Equal("Até breve", companions.Cell(2, 13).GetString());
        Assert.Equal(2, workbook.Worksheet("Resumo").Cell(12, 2).GetValue<int>());
        Assert.True(history.Tables.Single().ShowAutoFilter);
    }
}

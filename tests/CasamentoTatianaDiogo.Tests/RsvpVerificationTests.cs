using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class RsvpVerificationTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
    private readonly RecordingVerificationSender sender = new();
    private readonly MutableTime time = new();
    private readonly int guestId;
    private readonly int relativeId;
    private readonly int otherGuestId;

    public RsvpVerificationTests()
    {
        connection.Open();
        using var db = Db();
        db.Database.EnsureCreated();
        var family = new Family { Name = "Família A" };
        var other = new Family { Name = "Família B" };
        var guest = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", Family = family };
        var relative = new Guest { FirstName = "Rui", LastName = "Silva", DisplayName = "Rui Silva", Family = family };
        var outsider = new Guest { FirstName = "Eva", LastName = "Costa", DisplayName = "Eva Costa", Family = other };
        db.AddRange(guest, relative, outsider);
        db.SaveChanges();
        guestId = guest.Id; relativeId = relative.Id; otherGuestId = outsider.Id;
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    private RsvpVerificationService Verification(ApplicationDbContext db) => new(db, sender, protection, time, NullLogger<RsvpVerificationService>.Instance);
    private RsvpService Rsvp(ApplicationDbContext db) => new(db, new QuietNotifications(), new TestWebEnvironment(), new AppMessageService(), Verification(db), time);
    private async Task<CasamentoTatianaDiogo.Common.Errors.AppResult> Submit(ApplicationDbContext db, RsvpSubmitViewModel model, string? token, string? ip, string? agent)
    {
        model.EditSessionId = (await Rsvp(db).GetSelectionAsync(model.GuestId, token))?.Input.EditSessionId ?? Guid.Empty;
        return await Rsvp(db).SubmitAsync(model, token, ip, agent);
    }
    private async Task<string> Request(int id, RsvpVerificationChannel channel, string contact, string ip = "127.0.0.1")
    {
        using var db = Db();
        return await Verification(db).RequestCodeAsync(id, channel, contact, ip);
    }
    private async Task<string> Verified(int id, RsvpVerificationChannel channel, string contact)
    {
        var token = await Request(id, channel, contact);
        using var db = Db();
        return (await Verification(db).VerifyCodeAsync(token, sender.Code)).Token;
    }
    private async Task SaveFirst(RsvpVerificationChannel channel = RsvpVerificationChannel.Email, string contact = "ana@example.test")
    {
        var token = await Verified(guestId, channel, contact);
        using var db = Db();
        var result = await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.Attending }, token, null, null);
        Assert.True(result.Succeeded, result.Message);
    }

    [Theory]
    [InlineData(RsvpVerificationChannel.Email, " Ana@Example.TEST ", "ana@example.test")]
    [InlineData(RsvpVerificationChannel.Email, "912 345 678", null)]
    [InlineData(RsvpVerificationChannel.Email, "not-an-email", null)]
    [InlineData(RsvpVerificationChannel.Email, "Ana <ana@example.test>", null)]
    [InlineData((RsvpVerificationChannel)1, "+351912345678", null)]
    public void ContactsAreNormalizedBeforeUniquenessChecks(RsvpVerificationChannel channel, string value, string? expected) =>
        Assert.Equal(expected, RsvpContact.Normalize(channel, value));

    [Fact]
    public async Task UnverifiedRequestsCannotReadOrWriteResponses()
    {
        using var db = Db();
        Assert.Null(await Rsvp(db).GetSelectionAsync(guestId, null));
        Assert.False((await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.Attending }, null, null, null)).Succeeded);
        Assert.Empty(db.RsvpResponses);
        Assert.Empty(db.RsvpInvitationContacts);
    }

    [Theory]
    [InlineData(RsvpVerificationChannel.Email, "ANA@EXAMPLE.TEST")]
    public async Task EmailConfirmsOneFamilyAndRequiresVerificationForUpdates(RsvpVerificationChannel channel, string contact)
    {
        var token = await Verified(guestId, channel, contact);
        using (var db = Db())
        {
            Assert.NotNull(await Rsvp(db).GetSelectionAsync(relativeId, token));
            Assert.Null(await Rsvp(db).GetSelectionAsync(otherGuestId, token));
            Assert.True((await Submit(db, new()
            {
                GuestId = guestId, Status = RsvpStatus.Attending, DietaryRestrictions = "Sem glúten",
                FamilyResponses = [new() { GuestId = relativeId, Status = RsvpStatus.Attending }]
            }, token, null, null)).Succeeded);
            Assert.Equal(2, await db.RsvpResponses.CountAsync());
            var binding = await db.RsvpInvitationContacts.SingleAsync();
            Assert.Equal(RsvpContact.Normalize(channel, contact), binding.NormalizedContact);
            Assert.Equal(channel, binding.Channel);
        }
        using (var db = Db()) Assert.Null(await Verification(db).GetAccessAsync(token, guestId));
        time.Advance(TimeSpan.FromMinutes(1));
        var updateToken = await Verified(guestId, channel, contact);
        using (var db = Db())
        {
            var selection = await Rsvp(db).GetSelectionAsync(guestId, updateToken);
            Assert.Equal("Sem glúten", selection!.Input.DietaryRestrictions);
            Assert.Equal(RsvpStatus.Attending, selection.Input.Status);
            Assert.True((await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.NotAttending, ConfirmOverwrite = true }, updateToken, null, null)).Succeeded);
        }
        using (var db = Db())
        {
            Assert.Single(db.RsvpInvitationContacts);
            Assert.Equal(RsvpStatus.NotAttending, (await db.Guests.FindAsync(guestId))!.CurrentStatus);
            Assert.Null((await db.RsvpResponses.SingleAsync(x => x.GuestId == guestId)).DietaryRestrictions);
        }
    }

    [Theory]
    [InlineData(RsvpVerificationChannel.Email, "ana@example.test", " ANA@EXAMPLE.TEST ")]
    public async Task AContactCannotConfirmAnotherInvitation(RsvpVerificationChannel channel, string first, string duplicate)
    {
        await SaveFirst(channel, first);
        time.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(otherGuestId, channel, duplicate));
        using var db = Db();
        Assert.Single(db.RsvpInvitationContacts);
    }

    [Fact]
    public async Task AnInvitationCannotSwitchContactOrChannel()
    {
        await SaveFirst();
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(guestId, RsvpVerificationChannel.Email, "new@example.test"));
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(relativeId, (RsvpVerificationChannel)1, "912345678"));
    }

    [Fact]
    public async Task OldNonEmailChallengesAndSessionsCannotAuthorizeResponses()
    {
        var token = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using (var db = Db())
        {
            await db.RsvpVerificationChallenges.ExecuteUpdateAsync(x => x.SetProperty(c => c.Channel, (RsvpVerificationChannel)1));
            Assert.Null(await Verification(db).GetCodeFormAsync(token));
            await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(token, sender.Code));
            await db.RsvpVerificationChallenges.ExecuteUpdateAsync(x => x.SetProperty(c => c.Channel, RsvpVerificationChannel.Email));
            var access = (await Verification(db).VerifyCodeAsync(token, sender.Code)).Token;
            await db.RsvpVerificationChallenges.ExecuteUpdateAsync(x => x.SetProperty(c => c.Channel, (RsvpVerificationChannel)1));
            Assert.Null(await Verification(db).GetAccessAsync(access, guestId));
        }
    }

    [Fact]
    public async Task OldNonEmailBindingsArePreservedAndRequireSupportInsteadOfAllowingTakeover()
    {
        using var db = Db();
        var familyId = (await db.Guests.FindAsync(guestId))!.FamilyId;
        db.RsvpInvitationContacts.Add(new() { FamilyId = familyId, Channel = (RsvpVerificationChannel)1, NormalizedContact = "+351912345678" });
        await db.SaveChangesAsync();
        var form = await Verification(db).GetContactFormAsync(guestId);
        Assert.True(form!.RequiresSupport);
        Assert.False(form.EmailAvailable);
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(guestId, RsvpVerificationChannel.Email, "new@example.test"));
        Assert.Single(db.RsvpInvitationContacts);
    }

    [Fact]
    public async Task FailedAttemptsArePersistedAcrossInstancesAndTheCodeIsSingleUse()
    {
        var token = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        var code = sender.Code;
        using (var db = Db()) Assert.NotEqual(code, (await db.RsvpVerificationChallenges.SingleAsync()).CodeHash);
        for (var i = 0; i < 5; i++)
        {
            using var db = Db();
            await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(token, "invalid"));
        }
        using (var db = Db()) await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(token, code));
        time.Advance(TimeSpan.FromMinutes(1));
        var replacement = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using (var db = Db()) await Verification(db).VerifyCodeAsync(replacement, sender.Code);
        using (var db = Db())
        {
            await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(replacement, sender.Code));
            Assert.Equal("", (await db.RsvpVerificationChallenges.OrderByDescending(x => x.CreatedAt).FirstAsync()).CodeHash);
        }
    }

    [Fact]
    public async Task CodesAndVerifiedSessionsExpire()
    {
        var token = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        time.Advance(TimeSpan.FromMinutes(11));
        using (var db = Db()) await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(token, sender.Code));
        var access = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        time.Advance(TimeSpan.FromMinutes(31));
        using (var db = Db()) Assert.Null(await Verification(db).GetAccessAsync(access, guestId));
    }

    [Fact]
    public async Task ResendingInvalidatesThePreviousCodeAndEnforcesCooldownAndHourlyLimits()
    {
        var first = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        var oldCode = sender.Code;
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(guestId, RsvpVerificationChannel.Email, "ana@example.test"));
        for (var i = 0; i < 4; i++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        }
        using (var db = Db()) await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(first, oldCode));
        time.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(guestId, RsvpVerificationChannel.Email, "ana@example.test"));
        Assert.Equal(5, sender.Messages.Count);
        time.Advance(TimeSpan.FromHours(1));
        await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
    }

    [Fact]
    public async Task FailedDeliveryDoesNotAuthorizeOrBindTheContact()
    {
        sender.Fail = true;
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Request(guestId, RsvpVerificationChannel.Email, "ana@example.test"));
        using var db = Db();
        var challenge = await db.RsvpVerificationChallenges.SingleAsync();
        Assert.True(challenge.ExpiresAt <= time.GetUtcNow().UtcDateTime);
        Assert.Null(challenge.VerifiedAt);
        Assert.Empty(db.RsvpInvitationContacts);
    }

    [Fact]
    public async Task ForgedTokensAndChallengeTokensCannotAuthorizeSubmissions()
    {
        var pending = await Request(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using var db = Db();
        Assert.Null(await Verification(db).GetAccessAsync(pending, guestId));
        Assert.Null(await Verification(db).GetAccessAsync("forged", guestId));
        var access = (await Verification(db).VerifyCodeAsync(pending, sender.Code)).Token;
        await Assert.ThrowsAsync<RsvpVerificationException>(() => Verification(db).VerifyCodeAsync(access, sender.Code));
    }

    [Fact]
    public async Task AVerifiedGuestCannotSubmitResponsesForAnotherFamily()
    {
        var token = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using var db = Db();
        Assert.False((await Submit(db, new()
        {
            GuestId = guestId, Status = RsvpStatus.Attending,
            FamilyResponses = [new() { GuestId = otherGuestId, Status = RsvpStatus.Attending }]
        }, token, null, null)).Succeeded);
        Assert.Empty(db.RsvpResponses);
        Assert.Empty(db.RsvpInvitationContacts);
    }

    [Fact]
    public async Task TwoAlreadyVerifiedContactsCannotClaimTheSameInvitation()
    {
        var first = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        var second = await Verified(relativeId, RsvpVerificationChannel.Email, "rui@example.test");
        using (var db = Db()) Assert.True((await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.Attending }, first, null, null)).Succeeded);
        using (var db = Db())
        {
            Assert.Null(await Verification(db).GetAccessAsync(second, relativeId));
            Assert.False((await Submit(db, new() { GuestId = relativeId, Status = RsvpStatus.Attending }, second, null, null)).Succeeded);
            Assert.Single(db.RsvpResponses);
        }
    }

    [Fact]
    public async Task AContactVerifiedForTwoInvitationsCanOnlySaveOne()
    {
        var first = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        time.Advance(TimeSpan.FromMinutes(1));
        var second = await Verified(otherGuestId, RsvpVerificationChannel.Email, "ana@example.test");
        using (var db = Db()) Assert.True((await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.Attending }, first, null, null)).Succeeded);
        using (var db = Db())
        {
            Assert.False((await Submit(db, new() { GuestId = otherGuestId, Status = RsvpStatus.Attending }, second, null, null)).Succeeded);
            Assert.Single(db.RsvpInvitationContacts);
            Assert.Single(db.RsvpResponses);
        }
    }

    [Fact]
    public async Task DatabaseConstraintsRejectBothKindsOfConflictingClaim()
    {
        using var db = Db();
        var family = (await db.Guests.FindAsync(guestId))!.FamilyId;
        var otherFamily = (await db.Guests.FindAsync(otherGuestId))!.FamilyId;
        db.RsvpInvitationContacts.Add(new() { FamilyId = family, Channel = RsvpVerificationChannel.Email, NormalizedContact = "ana@example.test" });
        await db.SaveChangesAsync();
        using (var competing = Db())
        {
            competing.RsvpInvitationContacts.Add(new() { FamilyId = otherFamily, Channel = RsvpVerificationChannel.Email, NormalizedContact = "ana@example.test" });
            Assert.True(RsvpVerificationService.IsUniqueViolation(await Assert.ThrowsAsync<DbUpdateException>(() => competing.SaveChangesAsync())));
        }
        using (var competing = Db())
        {
            competing.RsvpInvitationContacts.Add(new() { FamilyId = family, Channel = RsvpVerificationChannel.Email, NormalizedContact = "other@example.test" });
            Assert.True(RsvpVerificationService.IsUniqueViolation(await Assert.ThrowsAsync<DbUpdateException>(() => competing.SaveChangesAsync())));
        }
    }

    [Fact]
    public async Task InvalidResponseDoesNotClaimAContact()
    {
        var token = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using var db = Db();
        Assert.False((await Submit(db, new() { GuestId = guestId, Status = RsvpStatus.Pending }, token, null, null)).Succeeded);
        Assert.Empty(db.RsvpInvitationContacts);
    }

    public void Dispose() => connection.Dispose();

    [Fact]
    public async Task TwoEditorsAreWarnedAndStaleFamilyResponsesRequireAnExplicitReload()
    {
        var first = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        time.Advance(TimeSpan.FromMinutes(1));
        var second = await Verified(relativeId, RsvpVerificationChannel.Email, "ana@example.test");
        Guid firstEdit, secondEdit;
        using (var db = Db()) firstEdit = (await Rsvp(db).GetSelectionAsync(guestId, first))!.Input.EditSessionId;
        using (var db = Db())
        {
            var selection = (await Rsvp(db).GetSelectionAsync(relativeId, second))!;
            secondEdit = selection.Input.EditSessionId;
            Assert.True(selection.HasConcurrentEditor);
            Assert.True((await Rsvp(db).HeartbeatAsync(guestId, firstEdit, first))!.OtherEditor);
        }
        time.Advance(TimeSpan.FromMinutes(3));
        using (var db = Db()) Assert.False((await Rsvp(db).HeartbeatAsync(guestId, firstEdit, first))!.OtherEditor);
        using (var db = Db()) Assert.True((await Rsvp(db).SubmitAsync(new()
        {
            GuestId = guestId, EditSessionId = firstEdit, Status = RsvpStatus.Attending,
            FamilyResponses = [new() { GuestId = relativeId, Status = RsvpStatus.Attending, Message = "Resposta recente" }]
        }, first, null, null)).Succeeded);
        using (var db = Db())
        {
            Assert.True((await Rsvp(db).HeartbeatAsync(relativeId, secondEdit, second))!.HasChanged);
            var stale = new RsvpSubmitViewModel { GuestId = relativeId, EditSessionId = secondEdit,
                Status = RsvpStatus.NotAttending, ConfirmOverwrite = true, Message = "Resposta antiga" };
            var result = await Rsvp(db).SubmitAsync(stale, second, null, null);
            Assert.Equal(CasamentoTatianaDiogo.Common.Errors.ErrorCode.RsvpConcurrencyConflict, result.Error!.Code);
            Assert.Equal("Resposta recente", (await db.RsvpResponses.AsNoTracking().SingleAsync(x => x.GuestId == relativeId)).Message);
            var unchangedForm = (await Rsvp(db).GetSelectionAsync(relativeId, second, secondEdit))!;
            Assert.True(unchangedForm.HasConcurrencyConflict);
            var refreshed = (await Rsvp(db).GetSelectionAsync(relativeId, second))!;
            Assert.False(refreshed.HasConcurrencyConflict);
            Assert.NotEqual(unchangedForm.DraftScope, refreshed.DraftScope);
            stale.EditSessionId = refreshed.Input.EditSessionId;
            stale.Message = "Alteração revista";
            Assert.True((await Rsvp(db).SubmitAsync(stale, second, null, null)).Succeeded);
        }
    }

    [Fact]
    public async Task AWriterWinningBetweenReadAndSaveRollsBackTheWholeLosingBatch()
    {
        var losingToken = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        time.Advance(TimeSpan.FromMinutes(1));
        var winningToken = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        Guid losingEdit, winningEdit;
        using (var db = Db())
        {
            losingEdit = (await Rsvp(db).GetSelectionAsync(guestId, losingToken))!.Input.EditSessionId;
            winningEdit = (await Rsvp(db).GetSelectionAsync(guestId, winningToken))!.Input.EditSessionId;
        }
        var interceptor = new BeforeResponseSave(async () =>
        {
            using var winner = Db();
            Assert.True((await Rsvp(winner).SubmitAsync(new() { GuestId = guestId, EditSessionId = winningEdit,
                Status = RsvpStatus.Attending, Message = "Vencedor" }, winningToken, null, null)).Succeeded);
        });
        using (var loser = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).AddInterceptors(interceptor).Options))
        {
            var result = await Rsvp(loser).SubmitAsync(new() { GuestId = guestId, EditSessionId = losingEdit,
                Status = RsvpStatus.NotAttending, ConfirmOverwrite = true,
                FamilyResponses = [new() { GuestId = relativeId, Status = RsvpStatus.NotAttending }]
            }, losingToken, null, null);
            Assert.Equal(CasamentoTatianaDiogo.Common.Errors.ErrorCode.RsvpConcurrencyConflict, result.Error!.Code);
        }
        using var final = Db();
        Assert.Equal("Vencedor", (await final.RsvpResponses.SingleAsync()).Message);
        Assert.Equal(RsvpStatus.Pending, (await final.Guests.SingleAsync(x => x.Id == relativeId)).CurrentStatus);
        Assert.Single(await final.RsvpVerificationChallenges.Where(x => x.ReceiptJson != null).ToListAsync());
        Assert.NotNull(await Verification(final).GetAccessAsync(losingToken, guestId));
    }

    [Fact]
    public async Task EditingSessionsCannotBeForgedOrMovedToAnotherGuestOrVerification()
    {
        var token = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using var db = Db();
        var id = (await Rsvp(db).GetSelectionAsync(guestId, token))!.Input.EditSessionId;
        Assert.Null(await Rsvp(db).HeartbeatAsync(relativeId, id, token));
        Assert.Null(await Rsvp(db).HeartbeatAsync(guestId, id, "forged"));
        foreach (var edit in new[] { Guid.Empty, Guid.NewGuid(), id })
            Assert.False((await Rsvp(db).SubmitAsync(new() { GuestId = relativeId, EditSessionId = edit,
                Status = RsvpStatus.Attending }, token, null, null)).Succeeded);
        Assert.Empty(await db.RsvpResponses.ToListAsync());
    }

    private sealed class BeforeResponseSave(Func<Task> before) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData data,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<RsvpResponse>().Any(x => x.State is EntityState.Added or EntityState.Modified)) await before();
            return result;
        }
    }

    [Fact]
    public async Task ReceiptCapturesOnlySavedPeopleAndCompanionAndCannotAuthorizeAnEdit()
    {
        var token = await Verified(guestId, RsvpVerificationChannel.Email, "ana@example.test");
        using var db = Db();
        var verification = Verification(db);
        Assert.Null(await verification.CreateReceiptTokenAsync(token));
        var plusOne = new PlusOne { MainGuestId = guestId, PlusOneFirstName = "Pedro", PlusOneDisplayName = "Pedro Santos" };
        db.PlusOnes.Add(plusOne);
        await db.SaveChangesAsync();
        var result = await Submit(db, new()
        {
            GuestId = guestId, Status = RsvpStatus.Attending, Message = "Obrigado!\nAté breve.",
            DietaryRestrictions = "Sem glúten", MusicRequest = "A nossa música",
            PlusOneAttending = true, PlusOneId = plusOne.Id, PlusOneDietaryRestrictions = "Vegetariano",
            PlusOneMessage = "Olá", PlusOneMusicRequest = "Outra música",
            FamilyResponses = [new() { GuestId = relativeId, Status = RsvpStatus.NotAttending, Message = "Não consigo ir" }]
        }, token, "127.0.0.1", "Private user agent");
        Assert.True(result.Succeeded, result.Message);
        var receiptToken = await verification.CreateReceiptTokenAsync(token);
        Assert.NotNull(receiptToken);
        Assert.Null(await verification.GetReceiptAsync(token));
        Assert.Null(await verification.GetReceiptAsync(receiptToken + "tampered"));
        Assert.Null(await verification.GetAccessAsync(receiptToken, guestId));
        Assert.Null(await verification.GetAccessAsync(token, guestId));
        var receipt = (await verification.GetReceiptAsync(receiptToken))!;
        Assert.Equal(2, receipt.People.Count);
        Assert.Equal(2, receipt.Attending);
        Assert.Equal(1, receipt.NotAttending);
        Assert.Equal(time.GetUtcNow().UtcDateTime, receipt.SavedAtUtc);
        var person = receipt.People.Single(x => x.Name == "Ana Silva");
        Assert.Equal("Obrigado!\nAté breve.", person.Message);
        Assert.Equal("Sem glúten", person.DietaryRestrictions);
        Assert.Equal("A nossa música", person.MusicRequest);
        Assert.Equal("Pedro Santos", person.Companion!.Name);
        Assert.Equal("Vegetariano", person.Companion.DietaryRestrictions);
        Assert.Equal("Olá", person.Companion.Message);
        Assert.Equal("Outra música", person.Companion.MusicRequest);
        Assert.DoesNotContain("Private user agent", (await db.RsvpVerificationChallenges.SingleAsync()).ReceiptJson!);
        time.Advance(TimeSpan.FromHours(24));
        Assert.Null(await verification.GetReceiptAsync(receiptToken));
    }

    private class QuietNotifications : IRsvpEmailNotificationService
    {
        public Task SendAsync(IReadOnlyCollection<RsvpEmailDetail> responses) => Task.CompletedTask;
    }
    private class TestWebEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal class MutableTime : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}

internal class RecordingVerificationSender : IRsvpVerificationSender
{
    public List<(RsvpVerificationChannel Channel, string Contact, string Code)> Messages { get; } = [];
    public string Code => Messages.Last().Code;
    public bool Fail { get; set; }
    public bool IsAvailable(RsvpVerificationChannel channel) => channel == RsvpVerificationChannel.Email;
    public Task SendAsync(RsvpVerificationChannel channel, string contact, string code, CancellationToken cancellationToken = default)
    {
        Messages.Add((channel, contact, code));
        if (Fail) throw new InvalidOperationException("Delivery failed.");
        return Task.CompletedTask;
    }
}

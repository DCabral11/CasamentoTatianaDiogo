using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public static class GuestCompanions
{
    public static async Task<List<(string Field, string Message)>> ValidateAsync(ApplicationDbContext db, Guest candidate)
    {
        var errors = new List<(string, string)>();
        if (candidate.IsPlusOne && candidate.AllowPlusOne)
            errors.Add((nameof(Guest.AllowPlusOne), "Um Plus-One não pode levar outro acompanhante."));
        if (candidate.IsPlusOne)
        {
            var main = await db.Guests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == candidate.MainGuestId);
            if (main == null || main.Id == candidate.Id || main.IsPlusOne || !main.AllowPlusOne)
                errors.Add((nameof(Guest.MainGuestId), "Seleciona um convidado que possa levar acompanhante e que não seja Plus-One."));
            else if (candidate.FamilyId != main.FamilyId)
                errors.Add((nameof(Guest.FamilyId), "O Plus-One deve pertencer à mesma família/convite do convidado que acompanha."));
        }
        else if (candidate.MainGuestId.HasValue)
            errors.Add((nameof(Guest.MainGuestId), "Marca «É Plus-One» para associar o convidado que acompanha."));

        var original = await db.Guests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == candidate.Id);
        if (original != null)
        {
            var hasCompanions = await db.PlusOnes.AnyAsync(x => x.MainGuestId == candidate.Id && !x.IsArchived);
            if (hasCompanions && (candidate.IsPlusOne || !candidate.AllowPlusOne || candidate.FamilyId != original.FamilyId))
                errors.Add((nameof(Guest.AllowPlusOne), "Este convidado tem acompanhantes associados. Revê essas associações antes de alterar a permissão, a família ou de o marcar como Plus-One."));
            if (candidate.IsPlusOne != original.IsPlusOne || candidate.MainGuestId != original.MainGuestId || candidate.FamilyId != original.FamilyId)
            {
                var hasResponse = await db.RsvpResponses.AnyAsync(x => x.GuestId == candidate.Id ||
                    (original.IsPlusOne && x.GuestId == original.MainGuestId) ||
                    (x.PlusOne != null && x.PlusOne.CompanionGuestId == candidate.Id));
                if (hasResponse)
                    errors.Add((nameof(Guest.MainGuestId), "Já existe uma resposta RSVP para este convidado. Remove primeiro essa resposta na gestão de RSVP antes de alterar a associação ou a família."));
            }
        }
        return errors;
    }

    // Called within the same transaction as the guest edit/import.
    public static async Task SyncAsync(ApplicationDbContext db, Guest guest)
    {
        var link = await db.PlusOnes.SingleOrDefaultAsync(x => x.CompanionGuestId == guest.Id);
        if (!guest.IsPlusOne)
        {
            if (link != null) { link.IsArchived = true; link.CompanionGuestId = null; }
            return;
        }
        if (link == null)
        {
            link = new PlusOne { CompanionGuestId = guest.Id };
            db.PlusOnes.Add(link);
        }
        link.MainGuestId = guest.MainGuestId!.Value;
        link.PlusOneFirstName = guest.FirstName;
        link.PlusOneLastName = guest.LastName;
        link.PlusOneDisplayName = guest.DisplayName;
        link.IsArchived = false;
    }

    public static async Task UpgradeExistingAsync(ApplicationDbContext db)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var old = await db.PlusOnes.Include(x => x.MainGuest)
                .Where(x => x.CompanionGuestId == null && !x.IsArchived).ToListAsync();
            foreach (var link in old)
            {
                var main = link.MainGuest!;
                main.AllowPlusOne = true;
                var latest = await db.RsvpResponses.Where(x => x.GuestId == main.Id)
                    .OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
                var guest = new Guest
                {
                    FirstName = link.PlusOneFirstName, LastName = link.PlusOneLastName,
                    DisplayName = link.PlusOneDisplayName, FamilyId = main.FamilyId,
                    IsPlusOne = true, MainGuestId = main.Id,
                    CurrentStatus = latest == null ? RsvpStatus.Pending :
                        latest.Status == RsvpStatus.Attending && latest.PlusOneAttending && latest.PlusOneId == link.Id
                            ? RsvpStatus.Attending : RsvpStatus.NotAttending
                };
                link.CompanionGuest = guest;
                db.Guests.Add(guest);
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }
}

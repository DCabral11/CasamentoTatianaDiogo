using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpService(ApplicationDbContext db, IRsvpEmailNotificationService emailNotifications) : IRsvpService
    {
        public async Task<List<Guest>> SearchGuestsAsync(string query)
        {
            query = (query ?? string.Empty).Trim();

            if (query.Length < 2)
                return [];

            return await db.Guests.Include(g => g.Family).Where(g => g.FirstName.Contains(query) || g.LastName.Contains(query) || g.DisplayName.Contains(query)).OrderBy(g => g.DisplayName).Take(20).ToListAsync();
        }

        public async Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId)
        {
            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == guestId);

            if (guest == null) 
                return null;

            var related = await db.Guests.Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id).OrderBy(g => g.DisplayName).ToListAsync();
            var hasExisting = await db.RsvpResponses.AnyAsync(r => r.GuestId == guestId);

            return new RsvpSelectionViewModel
            {
                Guest = guest,
                RelatedGuests = related,
                PlusOnes = guest.PlusOnes.ToList(),
                HasExistingResponse = hasExisting
            };
        }

        public async Task<(bool ok, string message)> SubmitAsync(RsvpSubmitViewModel model, string? ip, string? userAgent)
        {
            if (model.Status is null)
                return (false, "Por favor, selecione uma opção de presença.");

            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == model.GuestId);

            if (guest == null)
                return (false, "Convidado não encontrado.");

            var guestsToRespond = new List<Guest> { guest };

            var responsesByGuestId = model.FamilyResponses
                .GroupBy(response => response.GuestId)
                .ToDictionary(group => group.Key, group => group.Last());

            if (model.ApplyToGroup && guest.Family?.AllowGroupRsvp == true)
            {
                var familyGuests = await db.Guests
                    .Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id)
                    .ToListAsync();

                guestsToRespond.AddRange(familyGuests.Where(g =>
                    responsesByGuestId.TryGetValue(g.Id, out var response) && response.Status is not null));
            }

            if (await db.RsvpResponses.AnyAsync(r => guestsToRespond.Select(g => g.Id).Contains(r.GuestId)) && !model.ConfirmOverwrite)
                return (false, "Já existe uma resposta registrada. Por favor, confirme que deseja substituir a resposta existente.");

            var plusOne = model.PlusOneId.HasValue
                ? guest.PlusOnes.FirstOrDefault(p => p.Id == model.PlusOneId.Value)
                : null;

            if (model.PlusOneAttending && plusOne == null)
                return (false, "O acompanhante selecionado não é válido.");

            var emailDetails = new List<RsvpEmailDetail>();

            foreach (var responseGuest in guestsToRespond)
            {
                var response = await db.RsvpResponses.OrderByDescending(r => r.SubmittedAt).FirstOrDefaultAsync(r => r.GuestId == responseGuest.Id);

                if (response == null)
                {
                    response = new RsvpResponse
                    {
                        GuestId = responseGuest.Id,
                        SubmittedAt = DateTime.UtcNow
                    };

                    db.RsvpResponses.Add(response);
                }

                var familyResponse = responsesByGuestId.GetValueOrDefault(responseGuest.Id);
                response.Status = responseGuest.Id == guest.Id ? model.Status.Value : familyResponse!.Status!.Value;
                response.DietaryRestrictions = responseGuest.Id == guest.Id ? model.DietaryRestrictions : familyResponse?.DietaryRestrictions;
                response.Message = responseGuest.Id == guest.Id ? model.Message : familyResponse?.Message;
                response.MusicRequest = responseGuest.Id == guest.Id ? model.MusicRequest : familyResponse?.MusicRequest;
                response.PlusOneAttending = responseGuest.Id == guest.Id && model.PlusOneAttending;
                response.PlusOneId = responseGuest.Id == guest.Id && model.PlusOneAttending ? plusOne?.Id : null;
                response.UpdatedAt = DateTime.UtcNow;
                response.SubmittedFromIp = ip;
                response.UserAgent = userAgent;

                responseGuest.CurrentStatus = response.Status;

                emailDetails.Add(new RsvpEmailDetail(
                    responseGuest.DisplayName,
                    response.Status == CasamentoTatianaDiogo.Models.Enums.RsvpStatus.Attending ? "Confirmada" : "Não confirmada",
                    response.DietaryRestrictions,
                    response.Message,
                    response.MusicRequest,
                    response.PlusOneAttending ? plusOne?.PlusOneDisplayName : null));
            }

            await db.SaveChangesAsync();
            await emailNotifications.SendAsync(emailDetails);

            return (true, "A resposta foi guardada com sucesso. Agradecemos por confirmar sua presença!");
        }
    }
}

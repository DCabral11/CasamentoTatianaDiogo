using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpService(ApplicationDbContext db) : IRsvpService
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

            if (await db.RsvpResponses.AnyAsync(r => r.GuestId == guest.Id) && !model.ConfirmOverwrite)
                return (false, "Já existe uma resposta registrada para este convidado. Por favor, confirme que deseja substituir a resposta existente.");

            if (model.PlusOneAttending && guest.AllowPlusOne && model.PlusOneId == null)
                return (false, "Por favor, selecione o convidado acompanhante.");

            var guestIds = new List<int> { guest.Id };

            if (model.ApplyToGroup && guest.Family?.AllowGroupRsvp == true)
                guestIds = await db.Guests.Where(g => g.FamilyId == guest.FamilyId).Select(g => g.Id).ToListAsync();

            foreach (var guestId in guestIds)
            {
                var response = await db.RsvpResponses.OrderByDescending(r => r.SubmittedAt).FirstOrDefaultAsync(r => r.GuestId == guestId);

                if (response == null)
                {
                    response = new RsvpResponse
                    {
                        GuestId = guestId,
                        SubmittedAt = DateTime.UtcNow
                    };

                    db.RsvpResponses.Add(response);
                }

                response.Status = model.Status.Value;
                response.Message = model.Message;
                response.MusicRequest = model.MusicRequest;
                response.PlusOneAttending = guestId == guest.Id && model.PlusOneAttending;
                response.PlusOneId = guestId == guest.Id ? model.PlusOneId : null;
                response.UpdatedAt = DateTime.UtcNow;
                response.SubmittedFromIp = ip;
                response.UserAgent = userAgent;

                var g = await db.Guests.FindAsync(guestId);

                if (g != null)
                    g.CurrentStatus = model.Status.Value;
            }

            await db.SaveChangesAsync();

            return (true, "A resposta foi guardada com sucesso. Agradecemos por confirmar sua presença!");
        }
    }
}

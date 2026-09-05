using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpService(ApplicationDbContext db, IRsvpEmailNotificationService emailNotifications, IWebHostEnvironment environment) : IRsvpService
    {
        public async Task<List<Guest>> SearchGuestsAsync(string query)
        {
            query = (query ?? string.Empty).Trim();

            if (query.Length < 2)
                return [];

            var guests = await db.Guests.Include(g => g.Family).Where(g => g.FirstName.Contains(query) || g.LastName.Contains(query) || g.DisplayName.Contains(query)).OrderBy(g => g.DisplayName).Take(20).ToListAsync();
            PopulateProfileImagePaths(guests);
            return guests;
        }

        public async Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId)
        {
            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == guestId);

            if (guest == null) 
                return null;

            var related = await db.Guests.Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id).OrderBy(g => g.DisplayName).ToListAsync();
            var hasExisting = await db.RsvpResponses.AnyAsync(r => r.GuestId == guestId);
            PopulateProfileImagePaths([guest, .. related]);

            return new RsvpSelectionViewModel
            {
                Guest = guest,
                RelatedGuests = related,
                PlusOnes = guest.PlusOnes.ToList(),
                HasExistingResponse = hasExisting
            };
        }

        private void PopulateProfileImagePaths(IEnumerable<Guest> guests)
        {
            var imagesDirectory = Path.Combine(environment.WebRootPath, "images", "guests");
            if (!Directory.Exists(imagesDirectory))
                return;

            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            foreach (var guest in guests)
            {
                var selectedFileName = Path.GetFileName(guest.AvatarFileName ?? string.Empty);
                var fileName = !string.IsNullOrWhiteSpace(selectedFileName) &&
                               extensions.Contains(Path.GetExtension(selectedFileName), StringComparer.OrdinalIgnoreCase) &&
                               File.Exists(Path.Combine(imagesDirectory, selectedFileName))
                    ? selectedFileName
                    : extensions
                        .SelectMany(extension => new[] { $"guest-{guest.Id}{extension}", $"{guest.Id}{extension}" })
                        .FirstOrDefault(name => File.Exists(Path.Combine(imagesDirectory, name)));

                if (fileName != null)
                    guest.ProfileImagePath = $"/images/guests/{Uri.EscapeDataString(fileName)}";
            }
        }

        public async Task<(bool ok, string message)> SubmitAsync(RsvpSubmitViewModel model, string? ip, string? userAgent)
        {
            if (model.Status is null)
                return (false, "Escolhe se vais estar presente antes de continuares.");

            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == model.GuestId);

            if (guest == null)
                return (false, "Não encontrámos este convidado. Volta à pesquisa e tenta novamente.");

            if (model.Status == CasamentoTatianaDiogo.Models.Enums.RsvpStatus.NotAttending)
            {
                model.DietaryRestrictions = null;
                model.MusicRequest = null;
                model.PlusOneAttending = false;
                model.PlusOneId = null;
                model.PlusOneDietaryRestrictions = null;
                model.PlusOneMessage = null;
                model.PlusOneMusicRequest = null;
            }

            var guestsToRespond = new List<Guest> { guest };

            var responsesByGuestId = model.FamilyResponses
                .GroupBy(response => response.GuestId)
                .ToDictionary(group => group.Key, group => group.Last());

            if (guest.Family?.AllowGroupRsvp == true)
            {
                var familyGuests = await db.Guests
                    .Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id)
                    .ToListAsync();

                guestsToRespond.AddRange(familyGuests.Where(g =>
                    responsesByGuestId.TryGetValue(g.Id, out var response) && response.Status is not null));
            }

            if (await db.RsvpResponses.AnyAsync(r => guestsToRespond.Select(g => g.Id).Contains(r.GuestId)) && !model.ConfirmOverwrite)
                return (false, "Já existe uma resposta para um dos convidados selecionados. Assinala a opção de atualização para a substituir.");

            var plusOne = model.PlusOneId.HasValue
                ? guest.PlusOnes.FirstOrDefault(p => p.Id == model.PlusOneId.Value)
                : null;

            if (model.PlusOneAttending && plusOne == null)
                return (false, "Não foi possível associar esse acompanhante. Atualiza a página e tenta novamente.");

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
                response.PlusOneDietaryRestrictions = response.PlusOneAttending ? model.PlusOneDietaryRestrictions : null;
                response.PlusOneMessage = response.PlusOneAttending ? model.PlusOneMessage : null;
                response.PlusOneMusicRequest = response.PlusOneAttending ? model.PlusOneMusicRequest : null;
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
                    response.PlusOneAttending ? plusOne?.PlusOneDisplayName : null,
                    response.PlusOneDietaryRestrictions,
                    response.PlusOneMessage,
                    response.PlusOneMusicRequest));
            }

            await db.SaveChangesAsync();
            await emailNotifications.SendAsync(emailDetails);

            return (true, "A resposta foi guardada. Obrigado por confirmares a tua presença!");
        }
    }
}

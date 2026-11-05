using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Common.Errors;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Enums = CasamentoTatianaDiogo.Models.Enums;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpService(ApplicationDbContext db, IRsvpEmailNotificationService emailNotifications, IWebHostEnvironment environment, IAppMessageService messages, RsvpVerificationService verification, TimeProvider time) : IRsvpService
    {
        public async Task<List<Guest>> SearchGuestsAsync(string query)
        {
            query = (query ?? string.Empty).Trim();

            if (query.Length < 2)
                return [];

            var normalizedQuery = query.NormalizeForSearch();
            var guests = (await db.Guests
                .AsNoTracking()
                .Include(g => g.Family)
                .OrderBy(g => g.DisplayName)
                .ToListAsync())
                .Where(guest => guest.FirstName.NormalizeForSearch().Contains(normalizedQuery) ||
                                guest.LastName.NormalizeForSearch().Contains(normalizedQuery) ||
                                guest.DisplayName.NormalizeForSearch().Contains(normalizedQuery))
                .Take(20)
                .ToList();
            PopulateProfileImagePaths(guests);
            return guests;
        }

        public async Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId, string? verificationToken)
        {
            var access = await verification.GetAccessAsync(verificationToken, guestId);
            if (access == null) return null;
            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == guestId);

            if (guest == null) 
                return null;

            var related = await db.Guests.Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id).OrderBy(g => g.DisplayName).ToListAsync();
            var hasExisting = await db.RsvpResponses.AnyAsync(r => r.GuestId == guestId);
            var guestIds = related.Select(x => x.Id).Append(guestId).ToList();
            var responses = await db.RsvpResponses.AsNoTracking().Where(x => guestIds.Contains(x.GuestId))
                .OrderByDescending(x => x.SubmittedAt).ToListAsync();
            var current = responses.FirstOrDefault(x => x.GuestId == guestId);
            PopulateProfileImagePaths([guest, .. related]);

            return new RsvpSelectionViewModel
            {
                Guest = guest,
                RelatedGuests = related,
                PlusOnes = guest.PlusOnes.ToList(),
                HasExistingResponse = hasExisting,
                VerifiedContact = RsvpContact.Mask(access.Channel, access.Contact),
                DraftScope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"rsvp:{access.FamilyId}:{guestId}:{access.Contact}"))),
                Input = new RsvpSubmitViewModel
                {
                    GuestId = guestId, Status = current?.Status, DietaryRestrictions = current?.DietaryRestrictions,
                    Message = current?.Message, MusicRequest = current?.MusicRequest,
                    PlusOneId = current?.PlusOneId, PlusOneAttending = current?.PlusOneAttending == true,
                    PlusOneDietaryRestrictions = current?.PlusOneDietaryRestrictions,
                    PlusOneMessage = current?.PlusOneMessage, PlusOneMusicRequest = current?.PlusOneMusicRequest,
                    FamilyResponses = related.Select(person =>
                    {
                        var response = responses.FirstOrDefault(x => x.GuestId == person.Id);
                        return new FamilyGuestRsvpInput
                        {
                            GuestId = person.Id, Status = response?.Status, DietaryRestrictions = response?.DietaryRestrictions,
                            Message = response?.Message, MusicRequest = response?.MusicRequest
                        };
                    }).ToList()
                }
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

        public async Task<AppResult> SubmitAsync(RsvpSubmitViewModel model, string? verificationToken, string? ip, string? userAgent)
        {
            var access = await verification.GetAccessAsync(verificationToken, model.GuestId);
            if (access == null)
                return AppResult.Failure(ErrorCode.AccessDenied, "Verifica o teu contacto antes de confirmar ou alterar a resposta.");
            if (model.Status is not (Enums.RsvpStatus.Attending or Enums.RsvpStatus.NotAttending))
                return AppResult.Failure(ErrorCode.RsvpStatusRequired, messages.Get(ErrorCode.RsvpStatusRequired), nameof(model.Status));

            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes).FirstOrDefaultAsync(g => g.Id == model.GuestId);

            if (guest == null)
                return AppResult.Failure(ErrorCode.RsvpGuestNotFound, messages.Get(ErrorCode.RsvpGuestNotFound));

            var guestsToRespond = new List<Guest> { guest };

            if (model.FamilyResponses.Any(x => x.Status.HasValue &&
                x.Status is not (Enums.RsvpStatus.Attending or Enums.RsvpStatus.NotAttending)))
                return AppResult.Failure(ErrorCode.InvalidRequest, "Escolhe uma resposta de presença válida.");
            var requestedIds = model.FamilyResponses.Where(x => x.Status.HasValue).Select(x => x.GuestId).Distinct().ToList();
            if (requestedIds.Count > 0 && (guest.Family?.AllowGroupRsvp != true ||
                await db.Guests.CountAsync(x => requestedIds.Contains(x.Id) && x.FamilyId == access.FamilyId && x.Id != guest.Id) != requestedIds.Count))
                return AppResult.Failure(ErrorCode.AccessDenied, "Só podes responder pelos convidados autorizados deste convite.");

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
                return AppResult.Failure(ErrorCode.RsvpExistingResponse, messages.Get(ErrorCode.RsvpExistingResponse), nameof(model.ConfirmOverwrite));

            var attending = model.Status == Enums.RsvpStatus.Attending;
            var bringingPlusOne = attending && model.PlusOneAttending;
            var plusOne = bringingPlusOne && model.PlusOneId.HasValue
                ? guest.PlusOnes.FirstOrDefault(p => p.Id == model.PlusOneId.Value)
                : null;

            if (bringingPlusOne && plusOne == null)
                return AppResult.Failure(ErrorCode.RsvpInvalidPlusOne, messages.Get(ErrorCode.RsvpInvalidPlusOne));

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
                response.DietaryRestrictions = responseGuest.Id == guest.Id ? (attending ? model.DietaryRestrictions : null) : familyResponse?.DietaryRestrictions;
                response.Message = responseGuest.Id == guest.Id ? model.Message : familyResponse?.Message;
                response.MusicRequest = responseGuest.Id == guest.Id ? (attending ? model.MusicRequest : null) : familyResponse?.MusicRequest;
                response.PlusOneAttending = responseGuest.Id == guest.Id && bringingPlusOne;
                response.PlusOneId = response.PlusOneAttending ? plusOne?.Id : null;
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

            var binding = await db.RsvpInvitationContacts.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == access.FamilyId);
            if (binding != null && (binding.Channel != access.Channel || binding.NormalizedContact != access.Contact))
                return AppResult.Failure(ErrorCode.AccessDenied, RsvpVerificationService.ContactUnavailable);
            var bindingAdded = binding == null;
            if (bindingAdded)
                db.RsvpInvitationContacts.Add(new RsvpInvitationContact
                {
                    FamilyId = access.FamilyId, Channel = access.Channel,
                    NormalizedContact = access.Contact, ConfirmedAt = time.GetUtcNow().UtcDateTime
                });
            var challenge = await db.RsvpVerificationChallenges.FindAsync(access.ChallengeId);
            challenge!.SessionExpiresAt = time.GetUtcNow().UtcDateTime;
            try
            {
                // The contact claim and all responses are committed in one SaveChanges transaction.
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (bindingAdded && RsvpVerificationService.IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                return AppResult.Failure(ErrorCode.AccessDenied, RsvpVerificationService.ContactUnavailable);
            }
            await emailNotifications.SendAsync(emailDetails);

            return AppResult.Success();
        }
    }
}

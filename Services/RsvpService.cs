using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Common.Errors;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Enums = CasamentoTatianaDiogo.Models.Enums;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpService(ApplicationDbContext db, IRsvpEmailNotificationService emailNotifications, IWebHostEnvironment environment, IAppMessageService messages, RsvpVerificationService verification, TimeProvider time) : IRsvpService
    {
        public const string ConcurrentChange = "Este convite foi alterado entretanto ou este formulário já não é válido. A tua resposta não foi guardada. Revê a resposta mais recente antes de voltar a enviar.";
        private AppResult Conflict() => AppResult.Failure(ErrorCode.RsvpConcurrencyConflict, ConcurrentChange);

        public async Task<RsvpEditStatus?> HeartbeatAsync(int guestId, Guid editSessionId, string? verificationToken)
        {
            var access = await verification.GetAccessAsync(verificationToken, guestId);
            if (access == null) return null;
            var edit = await db.RsvpEditSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == editSessionId &&
                x.ChallengeId == access.ChallengeId && x.GuestId == guestId && x.FamilyId == access.FamilyId);
            if (edit == null) return null;
            var now = time.GetUtcNow().UtcDateTime;
            await db.RsvpEditSessions.Where(x => x.Id == edit.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now));
            var version = await db.Families.Where(x => x.Id == access.FamilyId).Select(x => x.RsvpVersion).SingleAsync();
            return new(await OtherEditorAsync(edit.Id, access.FamilyId), version != edit.ExpectedVersion);
        }

        private async Task<bool> OtherEditorAsync(Guid id, int familyId)
        {
            var now = time.GetUtcNow().UtcDateTime;
            return await db.RsvpEditSessions.AnyAsync(x => x.Id != id && x.FamilyId == familyId && x.LastSeenAt > now.AddMinutes(-2) &&
                db.RsvpVerificationChallenges.Any(c => c.Id == x.ChallengeId && c.SessionExpiresAt > now));
        }

        public async Task<List<Guest>> SearchGuestsAsync(string query)
        {
            query = (query ?? string.Empty).Trim();

            if (query.Length < 2)
                return [];

            var normalizedQuery = query.NormalizeForSearch();
            var guests = (await db.Guests
                .AsNoTracking()
                .Where(g => !g.IsPlusOne)
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

        public async Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId, string? verificationToken, Guid? editSessionId = null)
        {
            var access = await verification.GetAccessAsync(verificationToken, guestId);
            if (access == null) return null;
            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes.Where(p => !p.IsArchived)).ThenInclude(p => p.CompanionGuest).FirstOrDefaultAsync(g => g.Id == guestId);

            if (guest == null) 
                return null;

            RsvpEditSession? edit;
            if (editSessionId.HasValue)
                edit = await db.RsvpEditSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == editSessionId &&
                    x.ChallengeId == access.ChallengeId && x.GuestId == guestId && x.FamilyId == access.FamilyId);
            else
            {
                edit = new RsvpEditSession { ChallengeId = access.ChallengeId, GuestId = guestId, FamilyId = access.FamilyId,
                    ExpectedVersion = guest.Family!.RsvpVersion, LastSeenAt = time.GetUtcNow().UtcDateTime };
                db.RsvpEditSessions.Add(edit);
                await db.SaveChangesAsync();
            }

            var related = await db.Guests.Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id && !g.IsPlusOne).OrderBy(g => g.DisplayName).ToListAsync();
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
                PlusOnes = guest.AllowPlusOne && !guest.IsPlusOne ? guest.PlusOnes.ToList() : [],
                HasExistingResponse = hasExisting,
                HasConcurrentEditor = await OtherEditorAsync(edit?.Id ?? Guid.Empty, access.FamilyId),
                HasConcurrencyConflict = edit == null || edit.ExpectedVersion != guest.Family!.RsvpVersion,
                VerifiedContact = RsvpContact.Mask(access.Channel, access.Contact),
                DraftScope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"rsvp:{access.FamilyId}:{guestId}:{access.Contact}:{edit?.ExpectedVersion}"))),
                Input = new RsvpSubmitViewModel
                {
                    EditSessionId = edit?.Id ?? Guid.Empty,
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

            var guest = await db.Guests.Include(g => g.Family).Include(g => g.PlusOnes.Where(p => !p.IsArchived)).ThenInclude(p => p.CompanionGuest).FirstOrDefaultAsync(g => g.Id == model.GuestId);

            if (guest == null)
                return AppResult.Failure(ErrorCode.RsvpGuestNotFound, messages.Get(ErrorCode.RsvpGuestNotFound));

            var edit = await db.RsvpEditSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == model.EditSessionId &&
                x.ChallengeId == access.ChallengeId && x.GuestId == model.GuestId && x.FamilyId == access.FamilyId);
            if (edit == null || edit.ExpectedVersion != guest.Family!.RsvpVersion) return Conflict();

            var guestsToRespond = new List<Guest> { guest };

            if (model.FamilyResponses.Any(x => x.Status.HasValue &&
                x.Status is not (Enums.RsvpStatus.Attending or Enums.RsvpStatus.NotAttending)))
                return AppResult.Failure(ErrorCode.InvalidRequest, "Escolhe uma resposta de presença válida.");
            var requestedIds = model.FamilyResponses.Where(x => x.Status.HasValue).Select(x => x.GuestId).Distinct().ToList();
            if (requestedIds.Count > 0 && (guest.Family?.AllowGroupRsvp != true ||
                await db.Guests.CountAsync(x => requestedIds.Contains(x.Id) && x.FamilyId == access.FamilyId && x.Id != guest.Id && !x.IsPlusOne) != requestedIds.Count))
                return AppResult.Failure(ErrorCode.AccessDenied, "Só podes responder pelos convidados autorizados deste convite.");

            var responsesByGuestId = model.FamilyResponses
                .GroupBy(response => response.GuestId)
                .ToDictionary(group => group.Key, group => group.Last());

            if (guest.Family?.AllowGroupRsvp == true)
            {
                var familyGuests = await db.Guests.Include(g => g.PlusOnes.Where(p => !p.IsArchived)).ThenInclude(p => p.CompanionGuest)
                    .Where(g => g.FamilyId == guest.FamilyId && g.Id != guest.Id && !g.IsPlusOne)
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

            if (bringingPlusOne && (plusOne == null || !guest.AllowPlusOne || guest.IsPlusOne))
                return AppResult.Failure(ErrorCode.RsvpInvalidPlusOne, messages.Get(ErrorCode.RsvpInvalidPlusOne));

            var emailDetails = new List<RsvpEmailDetail>();
            var receiptPeople = new List<RsvpReceiptPerson>();
            var savedAt = time.GetUtcNow().UtcDateTime;

            foreach (var responseGuest in guestsToRespond)
            {
                var response = await db.RsvpResponses.OrderByDescending(r => r.SubmittedAt).FirstOrDefaultAsync(r => r.GuestId == responseGuest.Id);

                if (response == null)
                {
                    response = new RsvpResponse
                    {
                        GuestId = responseGuest.Id,
                        SubmittedAt = savedAt
                    };

                    db.RsvpResponses.Add(response);
                }

                var familyResponse = responsesByGuestId.GetValueOrDefault(responseGuest.Id);
                response.Status = responseGuest.Id == guest.Id ? model.Status.Value : familyResponse!.Status!.Value;
                response.DietaryRestrictions = responseGuest.Id == guest.Id ? (attending ? model.DietaryRestrictions : null) : familyResponse?.DietaryRestrictions;
                response.Message = responseGuest.Id == guest.Id ? model.Message : familyResponse?.Message;
                response.MusicRequest = responseGuest.Id == guest.Id ? (attending ? model.MusicRequest : null) : familyResponse?.MusicRequest;
                if (responseGuest.Id == guest.Id)
                {
                    response.PlusOneAttending = bringingPlusOne;
                    response.PlusOneId = bringingPlusOne ? plusOne?.Id : null;
                    response.PlusOneDietaryRestrictions = bringingPlusOne ? model.PlusOneDietaryRestrictions : null;
                    response.PlusOneMessage = bringingPlusOne ? model.PlusOneMessage : null;
                    response.PlusOneMusicRequest = bringingPlusOne ? model.PlusOneMusicRequest : null;
                }
                else if (response.Status != Enums.RsvpStatus.Attending)
                {
                    response.PlusOneAttending = false;
                    response.PlusOneId = null;
                    response.PlusOneDietaryRestrictions = null;
                    response.PlusOneMessage = null;
                    response.PlusOneMusicRequest = null;
                }
                var selectedCompanion = responseGuest.PlusOnes.FirstOrDefault(p => p.Id == response.PlusOneId);
                foreach (var companion in responseGuest.PlusOnes.Where(p => p.CompanionGuest != null))
                    companion.CompanionGuest!.CurrentStatus = response.PlusOneAttending && companion.Id == response.PlusOneId
                        ? Enums.RsvpStatus.Attending : Enums.RsvpStatus.NotAttending;
                response.UpdatedAt = savedAt;
                response.SubmittedFromIp = ip;
                response.UserAgent = userAgent;

                responseGuest.CurrentStatus = response.Status;

                receiptPeople.Add(new(responseGuest.DisplayName, response.Status, response.DietaryRestrictions,
                    response.Message, response.MusicRequest,
                    response.PlusOneAttending ? new RsvpReceiptPerson(selectedCompanion!.PlusOneDisplayName,
                        Enums.RsvpStatus.Attending, response.PlusOneDietaryRestrictions,
                        response.PlusOneMessage, response.PlusOneMusicRequest) : null));

                emailDetails.Add(new RsvpEmailDetail(
                    responseGuest.DisplayName,
                    response.Status == CasamentoTatianaDiogo.Models.Enums.RsvpStatus.Attending ? "Confirmada" : "Não confirmada",
                    response.DietaryRestrictions,
                    response.Message,
                    response.MusicRequest,
                    response.PlusOneAttending ? selectedCompanion?.PlusOneDisplayName : null,
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
            challenge!.SessionExpiresAt = savedAt;
            // Commit an immutable snapshot with the response, never reconstruct it from later edits.
            challenge.ReceiptJson = JsonSerializer.Serialize(new RsvpReceiptViewModel(
                $"RSVP-{challenge.Id:N}", model.GuestId, savedAt,
                RsvpContact.Mask(access.Channel, access.Contact), receiptPeople));
            challenge.ReceiptExpiresAt = savedAt.AddHours(24);
            // The family version update and all answers commit atomically. A concurrent winner
            // makes EF roll back the entire batch, including first responses and contact claims.
            guest.Family!.RsvpVersion = Guid.NewGuid();
            try
            {
                // The contact claim and all responses are committed in one SaveChanges transaction.
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return Conflict();
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

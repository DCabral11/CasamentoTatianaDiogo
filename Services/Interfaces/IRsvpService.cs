using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;

namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public record RsvpEmailDetail(
        string GuestName,
        string Status,
        string? DietaryRestrictions,
        string? Message,
        string? MusicRequest,
        string? PlusOneName,
        string? PlusOneDietaryRestrictions,
        string? PlusOneMessage,
        string? PlusOneMusicRequest);

    public interface IRsvpService
    {
        Task<List<Guest>> SearchGuestsAsync(string query);

        Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId, string? verificationToken, Guid? editSessionId = null);
        Task<RsvpEditStatus?> HeartbeatAsync(int guestId, Guid editSessionId, string? verificationToken);

        Task<CasamentoTatianaDiogo.Common.Errors.AppResult> SubmitAsync(RsvpSubmitViewModel model, string? verificationToken, string? ip, string? userAgent);
    }

    public record RsvpEditStatus(bool OtherEditor, bool HasChanged);

    public interface IRsvpEmailNotificationService
    {
        Task SendAsync(IReadOnlyCollection<RsvpEmailDetail> responses);
    }
}

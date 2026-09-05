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

        Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId);

        Task<(bool ok, string message)> SubmitAsync(RsvpSubmitViewModel model, string? ip, string? userAgent);
    }

    public interface IRsvpEmailNotificationService
    {
        Task SendAsync(IReadOnlyCollection<RsvpEmailDetail> responses);
    }
}

using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;

namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public interface IRsvpService
    {
        Task<List<Guest>> SearchGuestsAsync(string query);

        Task<RsvpSelectionViewModel?> GetSelectionAsync(int guestId);

        Task<(bool ok, string message)> SubmitAsync(RsvpSubmitViewModel model, string? ip, string? userAgent);
    }
}

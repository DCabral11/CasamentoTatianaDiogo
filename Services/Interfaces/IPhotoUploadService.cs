using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;

namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public interface IPhotoUploadService
    {
        Task<(bool open, string message, PhotoUploadSettings settings)> GetWindowAsync();

        Task<(bool ok, string message)> UploadAsync(PhotoUploadViewModel vm, string? ip, CancellationToken ct);
    }
}

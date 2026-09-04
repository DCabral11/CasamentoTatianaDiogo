using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public interface IWeddingSettingsService
    {
        Task<WeddingSettings> GetAsync();

        Task<PhotoUploadSettings> GetPhotoSettingsAsync();
    }
}

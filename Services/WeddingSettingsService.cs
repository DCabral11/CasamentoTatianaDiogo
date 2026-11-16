using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class WeddingSettingsService(ApplicationDbContext db) : IWeddingSettingsService
    {
        private Task<WeddingSettings>? currentSettings;

        public Task<WeddingSettings> GetAsync() => currentSettings ??= LoadSettingsAsync();

        private async Task<WeddingSettings> LoadSettingsAsync() => await db.WeddingSettings.AsNoTracking().FirstOrDefaultAsync() ?? new WeddingSettings();

        private Task<PhotoUploadSettings>? currentPhotoSettings;
        public Task<PhotoUploadSettings> GetPhotoSettingsAsync() => currentPhotoSettings ??= ReadPhotoSettingsAsync();
        private async Task<PhotoUploadSettings> ReadPhotoSettingsAsync() => await db.PhotoUploadSettings.AsNoTracking().FirstOrDefaultAsync() ?? new PhotoUploadSettings();
    }
}

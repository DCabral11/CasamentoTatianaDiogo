using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class WeddingSettingsService(ApplicationDbContext db, PublicContentCache cache) : IWeddingSettingsService
    {
        private Task<WeddingSettings>? currentSettings;

        public Task<WeddingSettings> GetAsync() => currentSettings ??= LoadSettingsAsync();

        private Task<WeddingSettings>? presentation;
        public Task<WeddingSettings> GetPresentationAsync() => currentSettings ??
            (presentation ??= cache.GetAsync("wedding-presentation", LoadSettingsAsync));

        private async Task<WeddingSettings> LoadSettingsAsync() => await db.WeddingSettings.AsNoTracking().FirstOrDefaultAsync() ?? new WeddingSettings();

        private Task<PhotoUploadSettings>? currentPhotoSettings;
        public Task<PhotoUploadSettings> GetPhotoSettingsAsync() => currentPhotoSettings ??= ReadPhotoSettingsAsync();
        private async Task<PhotoUploadSettings> ReadPhotoSettingsAsync() => await db.PhotoUploadSettings.AsNoTracking().FirstOrDefaultAsync() ?? new PhotoUploadSettings();
    }
}

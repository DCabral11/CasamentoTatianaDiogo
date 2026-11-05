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

        private async Task<WeddingSettings> LoadSettingsAsync() => await db.WeddingSettings.FirstOrDefaultAsync() ?? new WeddingSettings();

        public async Task<PhotoUploadSettings> GetPhotoSettingsAsync() => await db.PhotoUploadSettings.FirstOrDefaultAsync() ?? new PhotoUploadSettings();
    }
}

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class WeddingSettingsService(ApplicationDbContext db, PublicContentCache cache, SiteTranslationService translations) : IWeddingSettingsService
    {
        private Task<WeddingSettings>? currentSettings;

        public Task<WeddingSettings> GetAsync() => currentSettings ??= LoadSettingsAsync();

        private Task<WeddingSettings>? presentation;
        public Task<WeddingSettings> GetPresentationAsync() => presentation ??= PresentationAsync();
        private async Task<WeddingSettings> PresentationAsync()
        {
            var value = (await (currentSettings ?? cache.GetAsync("wedding-presentation", LoadSettingsAsync))).CopyForPresentation();
            value.CoupleStory = await translations.TranslateAsync("WeddingSettings.CoupleStory", value.CoupleStory);
            value.HomeHeroTitle = await translations.TranslateAsync("WeddingSettings.HomeHeroTitle", value.HomeHeroTitle);
            value.HomeHeroSubtitle = await translations.TranslateAsync("WeddingSettings.HomeHeroSubtitle", value.HomeHeroSubtitle);
            return value;
        }

        private async Task<WeddingSettings> LoadSettingsAsync() => await db.WeddingSettings.AsNoTracking().FirstOrDefaultAsync() ?? new WeddingSettings();

        private Task<PhotoUploadSettings>? currentPhotoSettings;
        public Task<PhotoUploadSettings> GetPhotoSettingsAsync() => currentPhotoSettings ??= ReadPhotoSettingsAsync();
        private async Task<PhotoUploadSettings> ReadPhotoSettingsAsync() => await db.PhotoUploadSettings.AsNoTracking().FirstOrDefaultAsync() ?? new PhotoUploadSettings();
    }
}

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class WeddingSettingsService(ApplicationDbContext db) : IWeddingSettingsService
    {
        public async Task<WeddingSettings> GetAsync() => await db.WeddingSettings.FirstOrDefaultAsync() ?? new WeddingSettings();

        public async Task<PhotoUploadSettings> GetPhotoSettingsAsync() => await db.PhotoUploadSettings.FirstOrDefaultAsync() ?? new PhotoUploadSettings();
    }
}

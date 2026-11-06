using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public class FaqService(ApplicationDbContext db)
{
    private Task<FaqPageSettings>? settings;
    // Shared only within a request, so publication changes appear on the next request.
    public Task<FaqPageSettings> GetSettingsAsync() => settings ??= ReadSettingsAsync();
    private async Task<FaqPageSettings> ReadSettingsAsync() =>
        await db.FaqPageSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1) ?? new();
    public async Task<FaqPageViewModel> GetPageAsync(bool preview = false) => new(await GetSettingsAsync(),
        await db.FaqItems.AsNoTracking().Where(x => preview || x.IsPublished)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(), preview);
}

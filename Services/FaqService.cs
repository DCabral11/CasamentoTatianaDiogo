using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public class FaqService(ApplicationDbContext db, PublicContentCache cache)
{
    private Task<FaqPageSettings>? settings;
    public Task<FaqPageSettings> GetSettingsAsync() => settings ??= cache.GetAsync("faq-settings", ReadSettingsAsync);
    private async Task<FaqPageSettings> ReadSettingsAsync() =>
        await db.FaqPageSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1) ?? new();
    public async Task<FaqPageViewModel> GetPageAsync(bool preview = false) => new(
        preview ? await ReadSettingsAsync() : await GetSettingsAsync(),
        preview ? await ReadItemsAsync(true) : await cache.GetAsync("faq-published", () => ReadItemsAsync(false)), preview);

    private async Task<IReadOnlyList<FaqItem>> ReadItemsAsync(bool preview) =>
        await db.FaqItems.AsNoTracking().Where(x => preview || x.IsPublished)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync();
}

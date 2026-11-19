using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public class FaqService(ApplicationDbContext db, PublicContentCache cache, SiteTranslationService translations)
{
    private Task<FaqPageSettings>? settings;
    public Task<FaqPageSettings> GetSettingsAsync() => settings ??= LocalizeSettingsAsync(cache.GetAsync("faq-settings", ReadSettingsAsync));
    private async Task<FaqPageSettings> ReadSettingsAsync() =>
        await db.FaqPageSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1) ?? new();
    public async Task<FaqPageViewModel> GetPageAsync(bool preview = false, bool localize = true)
    {
        var pageSettings = preview ? await ReadSettingsAsync() : await cache.GetAsync("faq-settings", ReadSettingsAsync);
        var items = preview ? await ReadItemsAsync(true) : await cache.GetAsync("faq-published", () => ReadItemsAsync(false));
        if (!localize) return new(pageSettings, items, preview);
        var localized = new List<FaqItem>();
        foreach (var item in items) localized.Add(new FaqItem
        {
            Id = item.Id, DisplayOrder = item.DisplayOrder, IsPublished = item.IsPublished, Version = item.Version,
            Question = await translations.TranslateAsync($"FaqItem.{item.Id}.Question", item.Question),
            Answer = await translations.TranslateAsync($"FaqItem.{item.Id}.Answer", item.Answer)
        });
        return new(await LocalizeSettingsAsync(Task.FromResult(pageSettings)), localized, preview);
    }

    private async Task<FaqPageSettings> LocalizeSettingsAsync(Task<FaqPageSettings> task)
    {
        var value = await task;
        return new()
        {
            Id = value.Id, IsEnabled = value.IsEnabled, Version = value.Version,
            Title = await translations.TranslateAsync("FaqSettings.Title", value.Title),
            NavigationLabel = await translations.TranslateAsync("FaqSettings.NavigationLabel", value.NavigationLabel),
            Introduction = await translations.TranslateAsync("FaqSettings.Introduction", value.Introduction),
            EmptyMessage = await translations.TranslateAsync("FaqSettings.EmptyMessage", value.EmptyMessage)
        };
    }

    private async Task<IReadOnlyList<FaqItem>> ReadItemsAsync(bool preview) =>
        await db.FaqItems.AsNoTracking().Where(x => preview || x.IsPublished)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync();
}

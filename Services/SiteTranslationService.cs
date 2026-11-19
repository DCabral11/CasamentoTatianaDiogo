using System.Security.Cryptography;
using System.Text;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

// Cache originals only; translated presentation objects belong to a single request.
public sealed class SiteTranslationService(ApplicationDbContext db, PublicContentCache cache)
{
    public const string Prefix = "I18n.";
    private Task<Dictionary<string, string>>? texts;
    public Task<Dictionary<string, string>> OriginalsAsync() => texts ??= cache.GetAsync("site-text", async () =>
    {
        var result = await db.SiteContents.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value);
        if (result.GetValueOrDefault("NavPhotos") == "Galeria") result["NavPhotos"] = "Enviar fotografias";
        return result;
    });

    public static string Key(string language, string source) => Prefix + language + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    public async Task<string> TranslateAsync(string source, string? original)
    {
        if (string.IsNullOrEmpty(original)) return original ?? "";
        if (Language == "pt") return original;
        return (await OriginalsAsync()).GetValueOrDefault(Key(Language, source)) ?? T(original);
    }

    public async Task<IReadOnlyList<TranslationSource>> SourcesAsync()
    {
        var result = new Dictionary<string, TranslationSource>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) result[key] = new(key, value);
        }
        foreach (var row in await db.SiteContents.AsNoTracking().Where(x => !x.Key.StartsWith(Prefix)).ToListAsync()) Add("SiteContent." + row.Key, row.Value);
        foreach (var topic in WeddingTravel.Topics) result.TryAdd("SiteContent." + topic.Key, new("SiteContent." + topic.Key, topic.DefaultText));
        foreach (var field in WeddingPageMessages.Pages.SelectMany(x => x.Fields))
            if (field.DefaultText.Length > 0) result.TryAdd("SiteContent." + field.Key, new("SiteContent." + field.Key, field.DefaultText));
        var settings = await db.WeddingSettings.AsNoTracking().FirstOrDefaultAsync() ?? new();
        Add("WeddingSettings.CoupleStory", settings.CoupleStory); Add("WeddingSettings.HomeHeroTitle", settings.HomeHeroTitle); Add("WeddingSettings.HomeHeroSubtitle", settings.HomeHeroSubtitle);
        var faq = await db.FaqPageSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1) ?? new();
        Add("FaqSettings.Title", faq.Title); Add("FaqSettings.NavigationLabel", faq.NavigationLabel); Add("FaqSettings.Introduction", faq.Introduction); Add("FaqSettings.EmptyMessage", faq.EmptyMessage);
        foreach (var item in await db.FaqItems.AsNoTracking().ToListAsync())
        {
            Add($"FaqItem.{item.Id}.Question", item.Question); Add($"FaqItem.{item.Id}.Answer", item.Answer);
        }
        foreach (var item in await db.TimelineEvents.AsNoTracking().ToListAsync())
        {
            Add($"TimelineEvent.{item.Id}.Title", item.Title); Add($"TimelineEvent.{item.Id}.Description", item.Description);
        }
        return result.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
    }
}

public sealed record TranslationSource(string Key, string Original);

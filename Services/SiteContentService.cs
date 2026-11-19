using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class SiteContentService(SiteTranslationService translations) : ISiteContentService
    {
        private Task<IReadOnlyDictionary<string, string>>? current;
        public Task<IReadOnlyDictionary<string, string>> GetAsync() => current ??= ReadAsync();

        private async Task<IReadOnlyDictionary<string, string>> ReadAsync()
        {
            var labels = new Dictionary<string, string>();
            foreach (var (key, value) in (await translations.OriginalsAsync()).Where(x => !x.Key.StartsWith(SiteTranslationService.Prefix)))
                labels[key] = await translations.TranslateAsync("SiteContent." + key, value);
            foreach (var field in ViewModels.WeddingPageMessages.Pages.SelectMany(x => x.Fields))
                if (!labels.ContainsKey(field.Key)) labels[field.Key] = await translations.TranslateAsync("SiteContent." + field.Key, field.DefaultText);
            foreach (var topic in ViewModels.WeddingTravel.Topics)
                if (!labels.ContainsKey(topic.Key)) labels[topic.Key] = await translations.TranslateAsync("SiteContent." + topic.Key, topic.DefaultText);
            return labels;
        }
    }
}

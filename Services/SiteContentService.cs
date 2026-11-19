using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class SiteContentService(ApplicationDbContext db, PublicContentCache cache) : ISiteContentService
    {
        private Task<IReadOnlyDictionary<string, string>>? current;
        public Task<IReadOnlyDictionary<string, string>> GetAsync() => current ??= cache.GetAsync("site-text", ReadAsync);

        private async Task<IReadOnlyDictionary<string, string>> ReadAsync()
        {
            var labels = await db.SiteContents.AsNoTracking().Select(x => new { x.Key, x.Value })
                .ToDictionaryAsync(content => content.Key, content => content.Value);
            // Existing sites may still have the original seeded navigation label.
            if (labels.GetValueOrDefault("NavPhotos") == "Galeria")
                labels["NavPhotos"] = "Enviar fotografias";
            return labels;
        }
    }
}

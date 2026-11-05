using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class SiteContentService(ApplicationDbContext db) : ISiteContentService
    {
        public async Task<IReadOnlyDictionary<string, string>> GetAsync()
        {
            var labels = await db.SiteContents.AsNoTracking().ToDictionaryAsync(content => content.Key, content => content.Value);
            // Existing sites may still have the original seeded navigation label.
            if (labels.GetValueOrDefault("NavPhotos") == "Galeria")
                labels["NavPhotos"] = "Enviar fotografias";
            return labels;
        }
    }
}

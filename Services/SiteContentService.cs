using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services
{
    public class SiteContentService(ApplicationDbContext db) : ISiteContentService
    {
        public async Task<IReadOnlyDictionary<string, string>> GetAsync() =>
            await db.SiteContents.AsNoTracking().ToDictionaryAsync(content => content.Key, content => content.Value);
    }
}

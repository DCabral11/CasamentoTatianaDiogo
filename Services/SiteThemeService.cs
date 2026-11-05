using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace CasamentoTatianaDiogo.Services;

public sealed class SiteThemeService(IWeddingSettingsService settings, IMemoryCache cache)
{
    public SiteTheme LastKnown => cache.Get<SiteTheme>(typeof(SiteTheme)) ?? SiteTheme.Default;

    public async Task<SiteTheme> GetAsync()
    {
        var theme = new SiteTheme(await settings.GetAsync());
        cache.Set(typeof(SiteTheme), theme);
        return theme;
    }
}

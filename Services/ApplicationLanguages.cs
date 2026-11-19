using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace CasamentoTatianaDiogo.Services;

public static class ApplicationLanguages
{
    public static readonly CultureInfo[] Cultures = [Create("pt-PT"), Create("en-GB"), Create("fr-FR")];
    public static string? Normalize(string? value) => value?.ToLowerInvariant() switch
    {
        "pt" or "pt-pt" => "pt-PT",
        "en" or "en-gb" => "en-GB",
        "fr" or "fr-fr" => "fr-FR",
        _ => null
    };

    private static CultureInfo Create(string name)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        culture.NumberFormat.CurrencySymbol = "€";
        return culture;
    }

    public static void Configure(RequestLocalizationOptions options)
    {
        options.DefaultRequestCulture = new RequestCulture(Cultures[0]);
        options.SupportedCultures = Cultures;
        options.SupportedUICultures = Cultures;
        options.ApplyCurrentCultureToResponseHeaders = true;
        options.RequestCultureProviders =
        [
            new CustomRequestCultureProvider(context =>
            {
                var culture = Normalize(context.Request.Query["culture"].FirstOrDefault());
                return Task.FromResult(culture == null ? null : new ProviderCultureResult(culture));
            }),
            new CookieRequestCultureProvider()
        ];
    }
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Localization;

namespace CasamentoTatianaDiogo.Services;

public static class LocalizedText
{
    private static readonly ResourceManager Resources = new("CasamentoTatianaDiogo.Resources.UiResource", typeof(LocalizedText).Assembly);
    private static readonly ConcurrentDictionary<string, string> BrowserCatalogs = new();

    public static string T(string? text, params object?[] arguments)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var value = Resources.GetString(ResourceKey(text), CultureInfo.CurrentUICulture) ?? text;
        return arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    public static string TF(FormattableString text) => T(text.Format, text.GetArguments());
    public static string ResourceKey(string text) => "Text" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    public static string DatePattern => Language == "pt" ? "d 'de' MMMM 'de' yyyy" : "d MMMM yyyy";
    public static string BrowserCatalog => BrowserCatalogs.GetOrAdd(Language, _ =>
    {
        using var stream = typeof(LocalizedText).Assembly.GetManifestResourceStream("CasamentoTatianaDiogo.Resources.BrowserMessages.json")!;
        var keys = JsonSerializer.Deserialize<string[]>(stream)!;
        return JsonSerializer.Serialize(keys.ToDictionary(key => key, key => T(key)));
    });
}

public sealed class UiTextLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] => new(name, LocalizedText.T(name));
    public LocalizedString this[string name, params object[] arguments] => new(name, LocalizedText.T(name, arguments));
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}


using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Models;
using PhoneNumbers;

namespace CasamentoTatianaDiogo.Services;

public sealed record PhoneContact(string Stored, string Display, string Link);
public sealed record EmailContact(string Stored, string Display, string Link);

public static class ContactFormats
{
    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();
    private static readonly IdnMapping Domains = new() { UseStd3AsciiRules = true };
    private static readonly Regex Telephone = new(
        @"^(?<number>\+?\(?[0-9][0-9 ()\-.]*?)(?:\s*(?:ext\.?|extensão|ramal|x|;ext=)\s*(?<ext>[0-9]{1,10}))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool SupportedRegion(string? value) => value != null && Phones.GetSupportedRegions().Contains(value);
    public static IEnumerable<(string Code, string Label)> Regions() => Phones.GetSupportedRegions().Select(code =>
    {
        string name;
        try { name = new RegionInfo(code).DisplayName; } catch (ArgumentException) { name = code; }
        return (Code: code, Label: $"{name} (+{Phones.GetCountryCodeForRegion(code)})");
    }).OrderBy(x => x.Label);

    public static PhoneContact? Phone(string? input, WeddingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 100) return null;
        var match = Telephone.Match(input.Trim());
        if (!match.Success) return null;
        var raw = match.Groups["number"].Value;
        if (raw.StartsWith("00", StringComparison.Ordinal)) raw = "+" + raw[2..];
        try
        {
            var number = Phones.Parse(raw, settings.PhoneRegion);
            if (!Phones.IsPossibleNumber(number)) return null;
            var canonical = Phones.Format(number, PhoneNumberFormat.E164);
            var format = settings.PhoneDisplay switch
            {
                "compact" => PhoneNumberFormat.E164,
                "national" when Phones.GetRegionCodeForNumber(number) == settings.PhoneRegion => PhoneNumberFormat.NATIONAL,
                _ => PhoneNumberFormat.INTERNATIONAL
            };
            var display = Phones.Format(number, format);
            var ext = match.Groups["ext"].Value;
            var stored = ext.Length == 0 ? canonical : $"{canonical};ext={ext}";
            return new(stored, ext.Length == 0 ? display : $"{display} {settings.ExtensionLabel} {ext}", "tel:" + stored);
        }
        catch (NumberParseException) { return null; }
    }

    public static string? Domain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;
        try
        {
            var ascii = Domains.GetAscii(domain.Trim()).ToLowerInvariant();
            return ascii.Length <= 253 && ascii.Contains('.') && !ascii.EndsWith('.') &&
                Uri.CheckHostName(ascii) == UriHostNameType.Dns ? ascii : null;
        }
        catch (ArgumentException) { return null; }
    }

    public static EmailContact? Email(string? input, WeddingSettings settings, int limit = 254, bool completeDomain = false)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Any(char.IsControl)) return null;
        var value = input.Trim();
        if (completeDomain && !value.Contains('@') && settings.DefaultEmailDomain != null)
            value += "@" + settings.DefaultEmailDomain;
        var at = value.LastIndexOf('@');
        if (at <= 0 || at != value.IndexOf('@') || value[(at + 1)..].Any(char.IsWhiteSpace)) return null;
        var local = value[..at];
        var domain = Domain(value[(at + 1)..]);
        if (domain == null || System.Text.Encoding.UTF8.GetByteCount(local) > 64) return null;
        var address = local + "@" + domain;
        if (System.Text.Encoding.UTF8.GetByteCount(address) > limit || !MailAddress.TryCreate(address, out var parsed) ||
            parsed.Address != address || local.Any(char.IsWhiteSpace)) return null;
        try
        {
            var displayDomain = settings.EmailDomainDisplay == "ascii" ? domain : Domains.GetUnicode(domain);
            return new(address, local + "@" + displayDomain, "mailto:" + Uri.EscapeDataString(local) + "@" + domain);
        }
        catch (ArgumentException) { return null; }
    }

    public static string? SavePhone(string? input, WeddingSettings settings, Action<string> error)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var phone = Phone(input, settings);
        if (phone == null) error(T("Indica um telefone válido, com o indicativo para números estrangeiros. Para uma extensão usa, por exemplo, «ext. 123»."));
        return phone?.Stored ?? input;
    }

    public static string? SaveEmail(string? input, WeddingSettings settings, int limit, Action<string> error)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var email = Email(input, settings, limit, completeDomain: true);
        if (email == null) error(T("Indica um email válido dentro do limite do campo."));
        return email?.Stored ?? input;
    }
}

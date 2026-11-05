using System.Drawing;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.ViewModels;

public sealed class SiteTheme
{
    public const string DefaultLogo = "/images/navbar.png";
    public static readonly SiteTheme Default = new(new WeddingSettings());
    public string Logo { get; }
    public string Mode { get; }
    public string Variables { get; }

    public SiteTheme(WeddingSettings settings)
    {
        var primary = Parse(settings.PrimaryColor, "#bd4742");
        var secondary = Parse(settings.SecondaryColor, "#fce8e4");
        var onPrimary = Foreground(primary);
        var onSecondary = Foreground(secondary);
        var accent = primary;
        // On a similarly coloured surface, retain the hue and adjust its lightness.
        var surface = onSecondary == Color.White ? Color.Black : Color.White;
        while (Contrast(accent, secondary) < 4.5 || Contrast(accent, surface) < 4.5)
        {
            var next = Mix(accent, onSecondary);
            accent = next.ToArgb() == accent.ToArgb() ? onSecondary : next;
        }

        Mode = onSecondary == Color.White ? "dark" : "light";
        Logo = NormalizeLogo(settings.LogoPath) ?? DefaultLogo;
        Variables = $"--primary:{Hex(primary)};--primary-rgb:{Rgb(primary)};--secondary:{Hex(secondary)};" +
            $"--on-primary:{Hex(onPrimary)};--accent-ink:{Hex(accent)};--accent-rgb:{Rgb(accent)};" +
            $"--primary-hover:{Hex(Mix(primary, onPrimary == Color.White ? Color.Black : Color.White))}";
        // The original palette pairs its pink accents with the cream page background in site.css.
        if (Hex(secondary) != "#FCE8E4")
            Variables += $";--canvas:{Hex(secondary)}";
    }

    public static string? NormalizeLogo(string? path)
    {
        path = path?.Trim();
        if (string.IsNullOrEmpty(path) || path.Any(char.IsControl) || path.Contains('\\') || path.StartsWith("//"))
            return null;
        if (path.StartsWith('/')) return path;
        if (path.StartsWith("~/")) return "/" + path[2..].TrimStart('/');
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return uri.Scheme is "https" or "http" && uri.Host.Length > 0 ? uri.AbsoluteUri : null;
        return "/" + path;
    }

    private static Color Parse(string? value, string fallback)
    {
        var hex = value?.Trim() ?? "";
        if (!Regex.IsMatch(hex, "^#[a-fA-F0-9]{6}$")) hex = fallback;
        return Color.FromArgb(Convert.ToInt32(hex[1..], 16) | unchecked((int)0xff000000));
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static string Rgb(Color c) => $"{c.R},{c.G},{c.B}";
    private static Color Foreground(Color c) => Contrast(c, Color.White) > Contrast(c, Color.Black) ? Color.White : Color.Black;
    private static Color Mix(Color a, Color b) => Color.FromArgb((a.R * 3 + b.R) / 4, (a.G * 3 + b.G) / 4, (a.B * 3 + b.B) / 4);

    private static double Contrast(Color a, Color b)
    {
        var x = Luminance(a);
        var y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Linear(byte channel)
        {
            var n = channel / 255d;
            return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }
}

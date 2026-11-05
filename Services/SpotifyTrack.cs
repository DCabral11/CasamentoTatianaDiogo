using System.Text.RegularExpressions;

namespace CasamentoTatianaDiogo.Services;

public static class SpotifyTrack
{
    public static string? Normalize(string? link)
    {
        if (!Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "open.spotify.com" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0) return null;

        var match = Regex.Match(uri.AbsolutePath, @"^/(?:intl-[a-zA-Z-]+/)?(?:embed/)?track/([a-zA-Z0-9]{22})/?$");
        return match.Success ? $"https://open.spotify.com/track/{match.Groups[1].Value}" : null;
    }
}

using System.Globalization;
using System.Net.Mail;
using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.Services;

public static class RsvpContact
{
    public static string? Normalize(RsvpVerificationChannel channel, string? value)
    {
        value = (value ?? string.Empty).Trim();
        if (channel == RsvpVerificationChannel.Email)
        {
            if (value.Length > 254 || !MailAddress.TryCreate(value, out var address) ||
                !string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
                return null;
            var at = value.LastIndexOf('@');
            if (at < 1 || !value[(at + 1)..].Contains('.')) return null;
            try
            {
                var normalized = value[..at].ToLowerInvariant() + "@" + new IdnMapping().GetAscii(value[(at + 1)..]).ToLowerInvariant();
                return normalized.Length <= 254 ? normalized : null;
            }
            catch (ArgumentException) { return null; }
        }
        return null;
    }

    public static string Mask(RsvpVerificationChannel channel, string contact) =>
        channel == RsvpVerificationChannel.Email
            ? contact[..1] + "***" + contact[contact.IndexOf('@')..]
            : "***";
}

using System.Globalization;
using System.Text;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels;

namespace CasamentoTatianaDiogo.Services;

internal static class WeddingCalendar
{
    public static byte[] Create(WeddingSettings settings, DateTime startUtc, DateTime? endUtc)
    {
        var output = new StringBuilder();
        void Line(string text)
        {
            // Fold at 75 UTF-8 octets, including the continuation space.
            var length = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (length + rune.Utf8SequenceLength > 75)
                {
                    output.Append("\r\n ");
                    length = 1;
                }
                output.Append(rune.ToString());
                length += rune.Utf8SequenceLength;
            }
            output.Append("\r\n");
        }

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Casamento Tatiana Diogo//Calendario//PT");
        Line("BEGIN:VEVENT");
        Line("UID:d286bdb0-f5b7-4e63-9f46-987a15c2d10b");
        Line("DTSTAMP:" + Timestamp(settings.UpdatedAt));
        Line("DTSTART:" + Timestamp(startUtc));
        if (endUtc > startUtc) Line("DTEND:" + Timestamp(endUtc.Value));
        Line("SUMMARY:" + Escape($"Casamento de {settings.BrideName} e {settings.GroomName}"));
        var ceremony = WeddingTravel.Address(settings.CeremonyLocationName, settings.CeremonyAddress);
        var reception = WeddingTravel.Address(settings.ReceptionLocationName, settings.ReceptionAddress);
        if (ceremony.Length > 0) Line("LOCATION:" + Escape(ceremony));
        if (reception.Length > 0) Line("DESCRIPTION:" + Escape("Copo de água em " + reception));
        Line("END:VEVENT");
        Line("END:VCALENDAR");
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private static string Timestamp(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.Replace("\\", "\\\\").ReplaceLineEndings("\\n")
        .Replace(",", "\\,").Replace(";", "\\;");
}

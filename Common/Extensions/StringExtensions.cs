using System.Globalization;
using System.Text;

namespace CasamentoTatianaDiogo.Common.Extensions
{
    public static class StringExtensions
    {
        public static string NormalizeForSearch(this string? value) => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToUpperInvariant();
        public static string? NullIfWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

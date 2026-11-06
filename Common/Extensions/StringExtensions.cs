using System.Globalization;
using System.Text;

namespace CasamentoTatianaDiogo.Common.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Normalizes a string for search by removing diacritics and converting it to uppercase.
        /// </summary>
        /// <param name="value">The string to normalize.</param>
        /// <returns>The normalized string.</returns>
        public static string NormalizeForSearch(this string? value) => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToUpperInvariant();

        /// <summary>
        /// Returns null if the string is null or whitespace; otherwise, returns the trimmed string.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns>The trimmed string or null.</returns>
        public static string? NullIfWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

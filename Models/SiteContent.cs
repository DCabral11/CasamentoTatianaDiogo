using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class SiteContent
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string Description { get; set; } = string.Empty;

        public bool IsHtml { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class TimelineEvent
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public DateTime EventDateTime { get; set; }

        public int DisplayOrder { get; set; }

        [StringLength(50)]
        public string IconName { get; set; } = "bi-heart";

        public bool IsActive { get; set; } = true;
    }
}

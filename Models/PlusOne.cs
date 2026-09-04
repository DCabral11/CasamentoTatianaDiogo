using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class PlusOne
    {
        public int Id { get; set; }

        public int MainGuestId { get; set; }

        public Guest? MainGuest { get; set; }

        [Required, StringLength(100)]
        public string PlusOneFirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string PlusOneLastName { get; set; } = string.Empty;

        [Required, StringLength(220)]
        public string PlusOneDisplayName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? PlusOneDietaryRestrictions { get; set; }
    }
}

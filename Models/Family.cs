#region References

using System.ComponentModel.DataAnnotations;

#endregion

namespace CasamentoTatianaDiogo.Models
{
    public class Family
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? GroupCode { get; set; }

        public bool AllowGroupRsvp { get; set; } = true;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public ICollection<Guest> Guests { get; set; } = [];
    }
}

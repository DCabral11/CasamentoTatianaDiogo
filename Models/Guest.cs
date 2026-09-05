using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasamentoTatianaDiogo.Models
{
    public class Guest
    {
        public int Id { get; set; }

        public int FamilyId { get; set; }

        public Family? Family { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, StringLength(220)]
        public string DisplayName { get; set; } = string.Empty;

        [EmailAddress, StringLength(200)]
        public string? Email { get; set; }

        [Phone, StringLength(50)]
        public string? Phone { get; set; }

        public bool AllowPlusOne { get; set; }

        public bool IsChild { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public RsvpStatus CurrentStatus { get; set; } = RsvpStatus.Pending;

        public ICollection<RsvpResponse> RsvpResponses { get; set; } = [];

        public ICollection<PlusOne> PlusOnes { get; set; } = [];

        [NotMapped]
        public string? ProfileImagePath { get; set; }
    }
}

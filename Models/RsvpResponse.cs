using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class RsvpResponse
    {
        public int Id { get; set; }

        public int GuestId { get; set; }

        public Guest? Guest { get; set; }

        public RsvpStatus Status { get; set; } = RsvpStatus.Pending;

        [StringLength(1000)]
        public string? DietaryRestrictions { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }

        [StringLength(200)]
        public string? MusicRequest { get; set; }

        public bool PlusOneAttending { get; set; }

        public int? PlusOneId { get; set; }

        public PlusOne? PlusOne { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(64)]
        public string? SubmittedFromIp { get; set; }

        [StringLength(512)]
        public string? UserAgent { get; set; }
    }
}

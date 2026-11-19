#region References

using System.ComponentModel.DataAnnotations;

#endregion

namespace CasamentoTatianaDiogo.Models
{
    public enum RsvpVerificationChannel { Email = 0 }

    public class RsvpInvitationContact
    {
        public int FamilyId { get; set; }

        public RsvpVerificationChannel Channel { get; set; }

        [MaxLength(254)]
        public string NormalizedContact { get; set; } = string.Empty;

        public DateTime ConfirmedAt { get; set; }
    }

    public class RsvpVerificationChallenge
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int GuestId { get; set; }

        public int FamilyId { get; set; }

        public RsvpVerificationChannel Channel { get; set; }

        [MaxLength(254)] 
        public string NormalizedContact { get; set; } = string.Empty;

        [MaxLength(512)] 
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public int Attempts { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public DateTime? SessionExpiresAt { get; set; }

        public string? ReceiptJson { get; set; }

        public DateTime? ReceiptExpiresAt { get; set; }
    }

    public class RsvpVerificationLimit
    {
        [MaxLength(64)]
        public string Id { get; set; } = string.Empty;
        
        public DateTime WindowEndsAt { get; set; }
        
        public DateTime NextAllowedAt { get; set; }
        
        public int Count { get; set; }
    }
}
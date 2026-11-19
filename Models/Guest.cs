#region References

using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion

namespace CasamentoTatianaDiogo.Models
{
    public class Guest
    {
        public int Id { get; set; }

        #region Guest Informations

        public int FamilyId { get; set; }

        public Family? Family { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Notes { get; set; }

        #endregion

        #region Guest Contacts

        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        #endregion

        #region Guest Photos

        [StringLength(255)]
        public string? AvatarFileName { get; set; }

        [Range(1, 2.5)]
        public double AvatarScale { get; set; } = 1;

        [Range(0, 100)]
        public int AvatarPositionX { get; set; } = 50;

        [Range(0, 100)]
        public int AvatarPositionY { get; set; } = 50;

        [NotMapped]
        public string? ProfileImagePath { get; set; }

        #endregion

        #region Guest Plus-One Information

        public bool AllowPlusOne { get; set; }

        public bool IsPlusOne { get; set; }

        public int? MainGuestId { get; set; }
        
        public Guest? MainGuest { get; set; }

        public bool IsChild { get; set; }

        public ICollection<PlusOne> PlusOnes { get; set; } = [];

        #endregion

        #region Guest RSVP Information

        public RsvpStatus CurrentStatus { get; set; } = RsvpStatus.Pending;

        public ICollection<RsvpResponse> RsvpResponses { get; set; } = [];

        #endregion
    }
}
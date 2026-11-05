using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.ViewModels
{
    public record HomeViewModel(WeddingSettings Settings, IReadOnlyList<CarouselPhoto> CarouselImages);

    public record CarouselPhoto(string Url, string? SrcSet, string ThumbnailUrl);

    public record InformationViewModel(WeddingSettings Settings, IReadOnlyList<TimelineEvent> TimelineEvents);

    public class RsvpSearchViewModel
    {
        [Required(ErrorMessage = "Escreve o teu nome para começarmos a pesquisa.")]
        public string Query { get; set; } = string.Empty;

        public List<Guest> Results { get; set; } = [];

        public string? Message { get; set; }
    }

    public class RsvpSelectionViewModel
    {
        public Guest Guest { get; set; } = new();

        public List<Guest> RelatedGuests { get; set; } = [];

        public List<PlusOne> PlusOnes { get; set; } = [];

        public bool HasExistingResponse { get; set; }
        public string VerifiedContact { get; set; } = string.Empty;
        public string DraftScope { get; set; } = string.Empty;
        public RsvpSubmitViewModel Input { get; set; } = new();
    }

    public class RsvpSubmitViewModel
    {
        [Required]
        public int GuestId { get; set; }

        public bool ApplyToGroup { get; set; }

        [Required(ErrorMessage = "Escolhe se vais estar presente.")]
        public RsvpStatus? Status { get; set; }

        [StringLength(1000)]
        public string? DietaryRestrictions { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }

        [StringLength(200)]
        public string? MusicRequest { get; set; }

        public bool PlusOneAttending { get; set; }

        public int? PlusOneId { get; set; }

        [StringLength(1000)]
        public string? PlusOneDietaryRestrictions { get; set; }

        [StringLength(1000)]
        public string? PlusOneMessage { get; set; }

        [StringLength(200)]
        public string? PlusOneMusicRequest { get; set; }

        public bool ConfirmOverwrite { get; set; }

        public List<FamilyGuestRsvpInput> FamilyResponses { get; set; } = [];
    }

    public class FamilyGuestRsvpInput
    {
        public int GuestId { get; set; }

        [Required(ErrorMessage = "Escolhe se este familiar vai estar presente.")]
        public RsvpStatus? Status { get; set; }

        [StringLength(1000)]
        public string? DietaryRestrictions { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }

        [StringLength(200)]
        public string? MusicRequest { get; set; }
    }

    public class PhotoUploadViewModel
    {
        public bool IsOpen { get; set; }

        public string Message { get; set; } = string.Empty;

        public PhotoUploadSettings Settings { get; set; } = new();

        [StringLength(220)]
        public string? GuestName { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public List<IFormFile> Files { get; set; } = [];
    }
}

namespace CasamentoTatianaDiogo.ViewModels.Admin
{
    public class DashboardViewModel
    {
        public int Attending { get; set; }

        public int NotAttending { get; set; }

        public int Pending { get; set; }

        public int UploadedPhotos { get; set; }

        public int GuestCount { get; set; }
        public decimal BudgetBalance { get; set; }
        public int UnpricedLines { get; set; }
        public int? PaymentLineId { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public string? PaymentCategory { get; set; }
        public bool IsPaymentOverdue { get; set; }
        public int MissingSongs { get; set; }
    }
}

using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.ViewModels
{
    public record HomeViewModel(WeddingSettings Settings, IReadOnlyList<string> CarouselImages);

    public record InformationViewModel(WeddingSettings Settings, IReadOnlyList<TimelineEvent> TimelineEvents);

    public class RsvpSearchViewModel
    {
        [Required]
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
    }

    public class RsvpSubmitViewModel
    {
        [Required]
        public int GuestId { get; set; }

        public bool ApplyToGroup { get; set; }

        [Required]
        public RsvpStatus? Status { get; set; }

        [StringLength(1000)]
        public string? DietaryRestrictions { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }

        [StringLength(200)]
        public string? MusicRequest { get; set; }

        public bool PlusOneAttending { get; set; }

        public int? PlusOneId { get; set; }

        public bool ConfirmOverwrite { get; set; }
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
    }
}

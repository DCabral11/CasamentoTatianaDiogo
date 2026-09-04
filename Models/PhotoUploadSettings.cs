using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class PhotoUploadSettings
    {
        public int Id { get; set; }

        public bool IsEnabled { get; set; }

        public DateTime? OpensAt { get; set; }

        public DateTime? ClosesAt { get; set; }

        [StringLength(200)]
        public string? GoogleDriveFolderId { get; set; }

        [Range(1, 100)]
        public int MaxFilesSizeMb { get; set; } = 15;

        [StringLength(200)]
        public string AllowedExtensionsCsv { get; set; } = ".jpg,.jpeg,.png,.heic";
    }
}

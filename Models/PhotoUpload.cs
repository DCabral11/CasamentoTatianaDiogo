using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class PhotoUpload
    {
        public int Id { get; set; }

        [Required, StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string GoogleDriveFileId { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? GoogleDriveWebViewLink { get; set; }

        [Required, StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [StringLength(220)]
        public string? GuestName { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(64)]
        public string? UploadIp { get; set; }
    }
}

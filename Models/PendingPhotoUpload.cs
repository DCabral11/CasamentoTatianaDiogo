using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models;

public class PendingPhotoUpload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, StringLength(4000)] public string UploadUri { get; set; } = "";
    [Required, StringLength(200)] public string DriveFileId { get; set; } = "";
    [Required, StringLength(255)] public string OriginalFileName { get; set; } = "";
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    [ConcurrencyCheck] public long UploadedBytes { get; set; }
    [StringLength(220)] public string? GuestName { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [StringLength(64)] public string? UploadIp { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? PhotoUploadId { get; set; }
    public PhotoUpload? PhotoUpload { get; set; }
}

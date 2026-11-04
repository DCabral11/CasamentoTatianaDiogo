using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.ViewModels;

public class BeginPhotoUploadRequest
{
    [Required, StringLength(255)] public string FileName { get; set; } = "";
    [Required, StringLength(100)] public string ContentType { get; set; } = "";
    [Range(1, 100 * 1024 * 1024)] public long SizeBytes { get; set; }
    [StringLength(220)] public string? GuestName { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public record PhotoUploadProgress(long UploadedBytes, bool Completed);
public record PhotoUploadTicket(string Token, int ChunkSize);

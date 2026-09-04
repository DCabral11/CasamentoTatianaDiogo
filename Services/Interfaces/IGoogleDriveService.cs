namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public record DriveUploadResult(string FileId, string? WebViewLink);

    public interface IGoogleDriveService
    {
        Task<DriveUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string folderId, CancellationToken ct);
    }
}

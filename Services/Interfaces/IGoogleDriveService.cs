namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public record DriveUploadResult(string FileId, string? WebViewLink);
    public record DriveUploadSession(string UploadUri, string FileId);
    public record DriveChunkResult(long UploadedBytes, DriveUploadResult? CompletedFile = null);

    public interface IGoogleDriveService
    {
        Task<DriveUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string folderId, CancellationToken ct);
        Task<DriveUploadSession> BeginUploadAsync(string fileName, string contentType, long size, string folderId, CancellationToken ct);
        Task<DriveChunkResult> UploadChunkAsync(string uploadUri, string fileId, Stream stream, long offset, long length, long total, CancellationToken ct);
    }
}

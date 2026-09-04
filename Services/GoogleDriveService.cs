using CasamentoTatianaDiogo.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;

namespace CasamentoTatianaDiogo.Services
{
    public class GoogleDriveService(IConfiguration config, ILogger<GoogleDriveService> logger) : IGoogleDriveService
    {
        public async Task<DriveUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string folderId, CancellationToken ct)
        {
            var credentialsPath = config["GoogleDrive:CredentialsPath"];

            if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
                throw new InvalidOperationException("Google Drive credentials file path is not configured or the file does not exist.");

            GoogleCredential credential;
            await using (var fs = File.OpenRead(credentialsPath))
                credential = GoogleCredential.FromStream(fs).CreateScoped(DriveService.Scope.DriveFile);

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CasamentoTatianaDiogo"
            });
            
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };
            
            var request = service.Files.Create(metadata, stream, contentType);
            request.Fields = "id, webViewLink";

            var progress = await request.UploadAsync(ct);

            if (progress.Status != UploadStatus.Completed)
                throw progress.Exception ?? new Exception("File upload failed without an exception.");

            return new DriveUploadResult(request.ResponseBody.Id, request.ResponseBody.WebViewLink);
        }
    }
}

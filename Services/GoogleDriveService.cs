using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CasamentoTatianaDiogo.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;

namespace CasamentoTatianaDiogo.Services;

public class GoogleDriveService(IConfiguration config) : IGoogleDriveService
{
    private readonly Lazy<GoogleCredential> credential = new(() => LoadCredential(config));

    private static GoogleCredential LoadCredential(IConfiguration config)
    {
        var json = config["GoogleDrive:CredentialsJson"];
        var base64 = config["GoogleDrive:CredentialsBase64"];
        var path = config["GoogleDrive:CredentialsPath"];
        using var stream = !string.IsNullOrWhiteSpace(json) ? new MemoryStream(Encoding.UTF8.GetBytes(json))
            : !string.IsNullOrWhiteSpace(base64) ? new MemoryStream(Convert.FromBase64String(base64))
            : !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? (Stream)File.OpenRead(path)
            : throw new InvalidOperationException("Configure GoogleDrive__CredentialsJson, GoogleDrive__CredentialsBase64 ou GoogleDrive__CredentialsPath.");
        return GoogleCredential.FromServiceAccountCredential(ServiceAccountCredential.FromServiceAccountData(stream))
            .CreateScoped(DriveService.Scope.DriveFile);
    }

    private DriveService CreateService() => new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential.Value,
            ApplicationName = "CasamentoTatianaDiogo"
        });

    public async Task<DriveUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string folderId, CancellationToken ct)
    {
        using var service = CreateService();
        var request = service.Files.Create(new Google.Apis.Drive.v3.Data.File { Name = fileName, Parents = [folderId] }, stream, contentType);
        request.SupportsAllDrives = true;
        request.Fields = "id,webViewLink";
        var progress = await request.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
            throw progress.Exception ?? new IOException("Google Drive upload failed.");
        return new DriveUploadResult(request.ResponseBody.Id, request.ResponseBody.WebViewLink);
    }

    public async Task<DriveUploadSession> BeginUploadAsync(string fileName, string contentType, long size, string folderId, CancellationToken ct)
    {
        using var service = CreateService();
        // A stable ID lets a retry recover if Drive commits the file before our DB save succeeds.
        var generate = service.Files.GenerateIds();
        generate.Count = 1;
        generate.Space = "drive";
        var ids = await generate.ExecuteAsync(ct);
        var fileId = ids.Ids.Single();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&supportsAllDrives=true&fields=id,webViewLink")
        {
            Content = JsonContent.Create(new { id = fileId, name = fileName, mimeType = contentType, parents = new[] { folderId } })
        };
        request.Headers.Add("X-Upload-Content-Type", contentType);
        request.Headers.Add("X-Upload-Content-Length", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var response = await service.HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var location = response.Headers.Location ?? throw new IOException("Google Drive did not return an upload session.");
        if (location.Scheme != "https" || !location.Host.EndsWith(".googleapis.com", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Google Drive returned an invalid upload session.");
        return new DriveUploadSession(location.AbsoluteUri, fileId);
    }

    public async Task<DriveChunkResult> UploadChunkAsync(string uploadUri, string fileId, Stream stream, long offset, long length, long total, CancellationToken ct)
    {
        using var service = CreateService();
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUri) { Content = new StreamContent(stream) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentLength = length;
        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + length - 1, total);
        using var response = await service.HttpClient.SendAsync(request, ct);
        if ((int)response.StatusCode == 308)
        {
            var received = response.Headers.TryGetValues("Range", out var ranges)
                ? long.Parse(ranges.Single().Split('-').Last(), System.Globalization.CultureInfo.InvariantCulture) + 1 : 0;
            return new DriveChunkResult(received);
        }
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var get = service.Files.Get(fileId);
            get.Fields = "id,webViewLink,size";
            get.SupportsAllDrives = true;
            var file = await get.ExecuteAsync(ct);
            if (file.Size != total)
                throw new IOException("The Google Drive upload session has expired. Start the upload again.");
            return new DriveChunkResult(total, new DriveUploadResult(file.Id, file.WebViewLink));
        }
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = document.RootElement;
        if (root.GetProperty("id").GetString() != fileId)
            throw new IOException("Google Drive returned an unexpected file ID.");
        return new DriveChunkResult(total, new DriveUploadResult(fileId,
            root.TryGetProperty("webViewLink", out var link) ? link.GetString() : $"https://drive.google.com/file/d/{fileId}/view"));
    }
}

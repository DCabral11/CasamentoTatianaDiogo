using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using System.Text.RegularExpressions;

namespace CasamentoTatianaDiogo.Services
{
    public class PhotoUploadService(ApplicationDbContext db, IWeddingSettingsService settingsSvc, IGoogleDriveService drive, ILogger<PhotoUploadService> logger) : IPhotoUploadService
    {
        public async Task<(bool open, string message, PhotoUploadSettings settings)> GetWindowAsync()
        {
            var s = await settingsSvc.GetPhotoSettingsAsync();
            var now = DateTime.Now;

            if (!s.IsEnabled)
                return (false, "O upload de fotos está desativado.", s);

            if (s.OpensAt.HasValue && now < s.OpensAt.Value)
                return (false, $"O upload de fotos ainda não está aberto. Abrirá em {s.OpensAt.Value}.", s);

            if (s.ClosesAt.HasValue && now > s.ClosesAt.Value)
                return (false, $"O upload de fotos já foi encerrado. Encerrado em {s.ClosesAt.Value}.", s);

            return (true, "O upload de fotos está aberto.", s);
        }

        public async Task<(bool ok, string message)> UploadAsync(PhotoUploadViewModel vm, string? ip, CancellationToken ct)
        {
            var window = await GetWindowAsync();

            if (!window.open)
                return (false, window.message);

            var s = window.settings;
            var folder = s.GoogleDriveFolderId;

            if (string.IsNullOrWhiteSpace(folder))
                return (false, "A pasta do Google Drive não está configurada.");

            var allowed = s.AllowedExtensionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).ToHashSet();
            var mimeAllowed = new HashSet<string>
            {
                "image/jpeg",
                "image/png",
                "image/heic",
                "image/heif"
            };

            foreach (var file in vm.Files.Where(f => f.Length > 0))
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                    return (false, $"O arquivo {file.FileName} tem uma extensão não permitida.");

                if (file.Length > s.MaxFilesSizeMb * 1024L * 1024L)
                    return (false, $"O arquivo {file.FileName} excede o tamanho máximo permitido de {s.MaxFilesSizeMb} MB.");

                if (!mimeAllowed.Contains(file.ContentType.ToLowerInvariant()))
                    return (false, $"O arquivo {file.FileName} tem um tipo MIME não permitido.");

                var safe = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), "[^a-zA-Z0-9_-]", "_");
                var stored = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safe}{ext}";

                await using var stream = file.OpenReadStream();
                var result = await drive.UploadAsync(stream, stored, file.ContentType, folder!, ct);

                db.PhotoUploads.Add(new PhotoUpload
                {
                    OriginalFileName = file.FileName,
                    GoogleDriveFileId = result.FileId,
                    GoogleDriveWebViewLink = result.WebViewLink,
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    GuestName = vm.GuestName,
                    Notes = vm.Notes,
                    UploadIp = ip
                });
            }

            await db.SaveChangesAsync();

            return (true, "Upload concluído com sucesso.");
        }
    }
}

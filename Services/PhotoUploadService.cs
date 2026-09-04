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
                return (false, "O envio de fotografias não está disponível neste momento.", s);

            if (s.OpensAt.HasValue && now < s.OpensAt.Value)
                return (false, $"O envio de fotografias abre em {s.OpensAt.Value:dd/MM/yyyy 'às' HH:mm}.", s);

            if (s.ClosesAt.HasValue && now > s.ClosesAt.Value)
                return (false, $"O envio de fotografias encerrou em {s.ClosesAt.Value:dd/MM/yyyy 'às' HH:mm}.", s);

            return (true, "Partilha as tuas melhores fotografias connosco!", s);
        }

        public async Task<(bool ok, string message)> UploadAsync(PhotoUploadViewModel vm, string? ip, CancellationToken ct)
        {
            var window = await GetWindowAsync();

            if (!window.open)
                return (false, window.message);

            var s = window.settings;
            var folder = s.GoogleDriveFolderId;

            if (string.IsNullOrWhiteSpace(folder))
                return (false, "O envio de fotografias ainda está a ser preparado. Tenta novamente mais tarde.");

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
                    return (false, $"O ficheiro {file.FileName} não tem um formato permitido.");

                if (file.Length > s.MaxFilesSizeMb * 1024L * 1024L)
                    return (false, $"O ficheiro {file.FileName} excede o limite de {s.MaxFilesSizeMb} MB.");

                if (!mimeAllowed.Contains(file.ContentType.ToLowerInvariant()))
                    return (false, $"O ficheiro {file.FileName} não pode ser enviado.");

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

            return (true, "As tuas fotografias foram enviadas. Obrigado por partilhares este momento connosco!");
        }
    }
}

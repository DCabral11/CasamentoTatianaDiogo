using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public class ResumablePhotoUploadService(ApplicationDbContext db, IPhotoUploadService photos,
    IGoogleDriveService drive, IDataProtectionProvider protection, TimeProvider time)
{
    // Drive requires non-final chunks to be multiples of 256 KiB; Vercel caps requests at 4.5 MB.
    public const int ChunkSize = 2 * 1024 * 1024;
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("PhotoUploadSession.v1").ToTimeLimitedDataProtector();

    public async Task<PhotoUploadTicket> BeginAsync(BeginPhotoUploadRequest request, string? ip, CancellationToken ct)
    {
        var window = await photos.GetWindowAsync();
        if (!window.open)
            throw new PhotoUploadValidationException(window.message);
        var settings = window.settings;
        if (string.IsNullOrWhiteSpace(settings.GoogleDriveFolderId))
            throw new PhotoUploadValidationException("O envio de fotografias ainda está a ser preparado. Tenta novamente mais tarde.");

        var name = Path.GetFileName(request.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var allowed = settings.AllowedExtensionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var validMime = extension switch
        {
            ".jpg" or ".jpeg" => request.ContentType == "image/jpeg",
            ".png" => request.ContentType == "image/png",
            ".heic" => request.ContentType is "image/heic" or "image/heif",
            ".heif" => request.ContentType == "image/heif",
            _ => false
        };
        if (string.IsNullOrWhiteSpace(name) || !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase) || !validMime)
            throw new PhotoUploadValidationException("Escolhe uma fotografia num dos formatos permitidos.");
        if (request.SizeBytes <= 0 || request.SizeBytes > settings.MaxFilesSizeMb * 1024L * 1024L)
            throw new PhotoUploadValidationException($"A fotografia excede o limite de {settings.MaxFilesSizeMb} MB ou está vazia.");

        var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(name), "[^a-zA-Z0-9_-]", "_");
        var session = await drive.BeginUploadAsync($"{Guid.NewGuid():N}_{safeName}{extension}", request.ContentType,
            request.SizeBytes, settings.GoogleDriveFolderId, ct);
        var pending = new PendingPhotoUpload
        {
            UploadUri = session.UploadUri,
            DriveFileId = session.FileId,
            OriginalFileName = name,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            GuestName = request.GuestName,
            Notes = request.Notes,
            UploadIp = ip,
            ExpiresAt = time.GetUtcNow().UtcDateTime.AddHours(1)
        };
        db.PendingPhotoUploads.Add(pending);
        await db.SaveChangesAsync(ct);
        return new PhotoUploadTicket(protector.Protect(pending.Id.ToString(), TimeSpan.FromHours(1)), ChunkSize);
    }

    public async Task<PhotoUploadProgress> UploadChunkAsync(string token, long offset, byte[] bytes, CancellationToken ct)
    {
        Guid id;
        try { id = Guid.Parse(protector.Unprotect(token)); }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        { throw new PhotoUploadValidationException("O envio expirou. Seleciona novamente a fotografia."); }

        var pending = await db.PendingPhotoUploads.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pending == null || pending.ExpiresAt <= time.GetUtcNow().UtcDateTime)
            throw new PhotoUploadValidationException("O envio expirou. Seleciona novamente a fotografia.");
        if (pending.PhotoUploadId.HasValue)
            return new PhotoUploadProgress(pending.SizeBytes, true);
        // Returning the recorded position makes a repeated request safe after a lost response.
        if (offset < pending.UploadedBytes)
            return new PhotoUploadProgress(pending.UploadedBytes, false);
        if (offset != pending.UploadedBytes || bytes.Length == 0 || bytes.Length > ChunkSize ||
            offset + bytes.Length > pending.SizeBytes ||
            (offset + bytes.Length < pending.SizeBytes && bytes.Length % (256 * 1024) != 0))
            throw new PhotoUploadValidationException("A fotografia não foi enviada na ordem esperada. Tenta novamente.");

        var window = await photos.GetWindowAsync();
        if (!window.open)
            throw new PhotoUploadValidationException(window.message);

        using var stream = new MemoryStream(bytes, writable: false);
        var progress = await drive.UploadChunkAsync(pending.UploadUri, pending.DriveFileId, stream,
            offset, bytes.Length, pending.SizeBytes, ct);
        if (progress.UploadedBytes < offset || progress.UploadedBytes > offset + bytes.Length ||
            (progress.CompletedFile != null && progress.UploadedBytes != pending.SizeBytes))
            throw new IOException("Google Drive returned an invalid upload position.");
        pending.UploadedBytes = progress.UploadedBytes;
        if (progress.CompletedFile != null)
        {
            pending.PhotoUpload = new PhotoUpload
            {
                OriginalFileName = pending.OriginalFileName,
                GoogleDriveFileId = progress.CompletedFile.FileId,
                GoogleDriveWebViewLink = progress.CompletedFile.WebViewLink,
                ContentType = pending.ContentType,
                SizeBytes = pending.SizeBytes,
                GuestName = pending.GuestName,
                Notes = pending.Notes,
                UploadIp = pending.UploadIp,
                UploadedAt = time.GetUtcNow().UtcDateTime
            };
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            pending = await db.PendingPhotoUploads.SingleAsync(x => x.Id == id, ct);
        }
        return new PhotoUploadProgress(pending.UploadedBytes, pending.PhotoUploadId.HasValue);
    }
}

public class PhotoUploadValidationException(string message) : Exception(message);

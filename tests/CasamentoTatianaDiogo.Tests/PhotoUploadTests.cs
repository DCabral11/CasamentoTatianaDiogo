using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class PhotoUploadTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
    private readonly FakeDrive drive = new();
    private readonly OpenPhotos photos = new();

    public PhotoUploadTests()
    {
        connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private ApplicationDbContext NewContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    private ResumablePhotoUploadService NewService(ApplicationDbContext db) => new(db, photos, drive, protection, TimeProvider.System);
    private static BeginPhotoUploadRequest Request(long size) => new() { FileName = "wedding.jpg", ContentType = "image/jpeg", SizeBytes = size, GuestName = "Guest" };

    [Fact]
    public async Task LargePhotoSurvivesInstanceChangesAndFinalChunkRetries()
    {
        const int size = 5 * 1024 * 1024 + 100;
        PhotoUploadTicket ticket;
        using (var db = NewContext()) ticket = await NewService(db).BeginAsync(Request(size), "127.0.0.1", default);
        Assert.True(ticket.ChunkSize < 4_500_000);
        long offset = 0;
        while (offset < size)
        {
            using var db = NewContext();
            var progress = await NewService(db).UploadChunkAsync(ticket.Token, offset, new byte[Math.Min(ticket.ChunkSize, size - offset)], default);
            Assert.Equal(progress.UploadedBytes == size, progress.Completed);
            offset = progress.UploadedBytes;
        }
        using (var db = NewContext())
        {
            var retry = await NewService(db).UploadChunkAsync(ticket.Token, size - 1, [0], default);
            Assert.True(retry.Completed);
            var photo = await db.PhotoUploads.SingleAsync();
            Assert.Equal(size, photo.SizeBytes);
            Assert.Equal("Guest", photo.GuestName);
            Assert.Equal(3, drive.ChunkCalls);
            db.PhotoUploads.Remove(photo);
            await db.SaveChangesAsync();
            Assert.Empty(db.PendingPhotoUploads);
        }
    }

    [Fact]
    public async Task RetriedIntermediateChunkReturnsSavedOffsetWithoutSendingAgain()
    {
        using var db = NewContext();
        var service = NewService(db);
        var ticket = await service.BeginAsync(Request(5 * 1024 * 1024), null, default);
        await service.UploadChunkAsync(ticket.Token, 0, new byte[ticket.ChunkSize], default);
        var retry = await service.UploadChunkAsync(ticket.Token, 0, new byte[ticket.ChunkSize], default);
        Assert.Equal(ticket.ChunkSize, retry.UploadedBytes);
        Assert.False(retry.Completed);
        Assert.Equal(1, drive.ChunkCalls);
        Assert.Empty(db.PhotoUploads);
    }

    [Fact]
    public async Task RejectsOversizedPhotosBeforeContactingDrive()
    {
        using var db = NewContext();
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => NewService(db).BeginAsync(Request(16 * 1024 * 1024), null, default));
        Assert.Equal(0, drive.BeginCalls);
    }

    [Fact]
    public async Task RejectsMismatchedMimeType()
    {
        using var db = NewContext();
        var request = Request(10);
        request.ContentType = "text/html";
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => NewService(db).BeginAsync(request, null, default));
        Assert.Equal(0, drive.BeginCalls);
    }

    [Fact]
    public async Task RejectsTamperedTokensAndOutOfOrderChunks()
    {
        using var db = NewContext();
        var service = NewService(db);
        var ticket = await service.BeginAsync(Request(5 * 1024 * 1024), null, default);
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => service.UploadChunkAsync("invalid-token", 0, [0], default));
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => service.UploadChunkAsync(ticket.Token, 1, [0], default));
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => service.UploadChunkAsync(ticket.Token, 0, [0], default));
        Assert.Equal(0, drive.ChunkCalls);
    }

    [Fact]
    public async Task RejectsExpiredSessionsAndClosedUploadWindows()
    {
        using var db = NewContext();
        var service = NewService(db);
        var ticket = await service.BeginAsync(Request(10), null, default);
        photos.IsOpen = false;
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => service.UploadChunkAsync(ticket.Token, 0, new byte[10], default));
        photos.IsOpen = true;
        var pending = await db.PendingPhotoUploads.SingleAsync();
        pending.ExpiresAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PhotoUploadValidationException>(() => service.UploadChunkAsync(ticket.Token, 0, new byte[10], default));
        Assert.Equal(0, drive.ChunkCalls);
    }

    public void Dispose() => connection.Dispose();

    private class OpenPhotos : IPhotoUploadService
    {
        public bool IsOpen = true;
        public Task<(bool open, string message, PhotoUploadSettings settings)> GetWindowAsync() => Task.FromResult((IsOpen, "Closed", new PhotoUploadSettings { IsEnabled = true, GoogleDriveFolderId = "test-folder" }));
        public Task<(bool ok, string message)> UploadAsync(PhotoUploadViewModel vm, string? ip, CancellationToken ct) => throw new NotSupportedException();
    }

    private class FakeDrive : IGoogleDriveService
    {
        public int BeginCalls;
        public int ChunkCalls;
        public Task<DriveUploadSession> BeginUploadAsync(string name, string type, long size, string folder, CancellationToken ct)
        {
            BeginCalls++;
            return Task.FromResult(new DriveUploadSession("https://www.googleapis.com/upload/test", Guid.NewGuid().ToString()));
        }
        public Task<DriveChunkResult> UploadChunkAsync(string uri, string id, Stream stream, long offset, long length, long total, CancellationToken ct)
        {
            ChunkCalls++;
            return Task.FromResult(new DriveChunkResult(offset + length, offset + length == total ? new DriveUploadResult(id, "https://drive.google.com/test") : null));
        }
        public Task<DriveUploadResult> UploadAsync(Stream stream, string name, string type, string folder, CancellationToken ct) => throw new NotSupportedException();
    }
}

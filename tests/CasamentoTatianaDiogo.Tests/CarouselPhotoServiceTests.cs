using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class CarouselPhotoServiceTests
{
    [Fact]
    public void VisitsReuseTheAlbumAndFileChangesRefreshThePhotosAndVariants()
    {
        using var files = new AlbumFiles();
        files.Paths.UnionWith(["images/carousel-02.png", "images/carousel-01.jpeg", "images/not-an-album.jpg",
            "images/carousel-invalid.txt", "images/home/carousel-01.jpeg-480.webp", "images/home/carousel-01.jpeg-768.webp",
            "images/home/carousel-01.jpeg-thumb.webp"]);
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var service = new CarouselPhotoService(new AlbumEnvironment(files), memory);

        var first = service.GetPhotos();
        Assert.Equal(new[] { "/images/carousel-01.jpeg", "/images/carousel-02.png" }, first.Select(x => x.Url));
        Assert.Equal("/images/home/carousel-01.jpeg-480.webp 480w, /images/home/carousel-01.jpeg-768.webp 768w", first[0].SrcSet);
        Assert.Equal("/images/home/carousel-01.jpeg-thumb.webp", first[0].ThumbnailUrl);
        Assert.Null(first[1].SrcSet);
        Assert.Equal(first[1].Url, first[1].ThumbnailUrl);
        Assert.Same(first, service.GetPhotos());
        Assert.Equal(1, files.DirectoryReads);

        files.Paths.Remove("images/carousel-02.png");
        files.Paths.Remove("images/home/carousel-01.jpeg-768.webp");
        files.Paths.Add("images/carousel-03 our day.webp");
        files.Change();
        var updated = service.GetPhotos();
        Assert.NotSame(first, updated);
        Assert.Equal(2, files.DirectoryReads);
        Assert.Equal(new[] { "/images/carousel-01.jpeg", "/images/carousel-03%20our%20day.webp" }, updated.Select(x => x.Url));
        Assert.Null(updated[0].SrcSet);
    }

    [Fact]
    public void AnEmptyAlbumIsCachedAndNewPhotosBecomeVisibleOnChange()
    {
        using var files = new AlbumFiles();
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var service = new CarouselPhotoService(new AlbumEnvironment(files), memory);
        Assert.Empty(service.GetPhotos());
        Assert.Empty(service.GetPhotos());
        Assert.Equal(1, files.DirectoryReads);
        files.Paths.Add("images/carousel-01.jpg");
        files.Change();
        Assert.Single(service.GetPhotos());
    }

    private sealed class AlbumFiles : IFileProvider, IDisposable
    {
        private CancellationTokenSource changes = new();
        public HashSet<string> Paths { get; } = [];
        public int DirectoryReads { get; private set; }
        public IFileInfo GetFileInfo(string subpath) => new AlbumFile(subpath.Split('/').Last(), Paths.Contains(subpath));
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            DirectoryReads++;
            return new AlbumDirectory(Paths.Where(path => path.StartsWith(subpath + "/", StringComparison.Ordinal) &&
                !path[(subpath.Length + 1)..].Contains('/')).Select(GetFileInfo).ToArray());
        }
        public IChangeToken Watch(string filter) => new CancellationChangeToken(changes.Token);
        public void Change()
        {
            var previous = changes;
            changes = new();
            previous.Cancel();
            previous.Dispose();
        }
        public void Dispose() => changes.Dispose();
    }

    private sealed record AlbumFile(string Name, bool Exists) : IFileInfo
    {
        public bool IsDirectory => false;
        public long Length => 0;
        public string? PhysicalPath => null;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public Stream CreateReadStream() => throw new NotSupportedException();
    }

    private sealed record AlbumDirectory(IFileInfo[] Files) : IDirectoryContents
    {
        public bool Exists => true;
        public IEnumerator<IFileInfo> GetEnumerator() => ((IEnumerable<IFileInfo>)Files).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class AlbumEnvironment(IFileProvider files) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AlbumTests";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = files;
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = files;
    }
}

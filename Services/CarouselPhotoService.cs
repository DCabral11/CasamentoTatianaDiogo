using CasamentoTatianaDiogo.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace CasamentoTatianaDiogo.Services;

public sealed class CarouselPhotoService(IWebHostEnvironment environment, IMemoryCache cache)
{
    private readonly object loading = new();
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public IReadOnlyList<CarouselPhoto> GetPhotos()
    {
        if (cache.TryGetValue(typeof(CarouselPhotoService), out IReadOnlyList<CarouselPhoto>? photos))
            return photos!;

        lock (loading)
        {
            return cache.GetOrCreate<IReadOnlyList<CarouselPhoto>>(typeof(CarouselPhotoService), entry =>
            {
                var files = environment.WebRootFileProvider;
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.AddExpirationToken(files.Watch("images/carousel-*"));
                entry.AddExpirationToken(files.Watch("images/home/*"));

                return Array.AsReadOnly(files.GetDirectoryContents("images")
                    .Where(file => !file.IsDirectory && file.Name.StartsWith("carousel-", StringComparison.Ordinal) &&
                        Extensions.Contains(Path.GetExtension(file.Name)))
                    .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(file =>
                    {
                        var name = Uri.EscapeDataString(file.Name);
                        var url = $"/images/{name}";
                        var variant = $"/images/home/{name}";
                        var prefix = $"images/home/{file.Name}";
                        var srcSet = files.GetFileInfo(prefix + "-480.webp").Exists && files.GetFileInfo(prefix + "-768.webp").Exists
                            ? $"{variant}-480.webp 480w, {variant}-768.webp 768w" : null;
                        var thumbnail = files.GetFileInfo(prefix + "-thumb.webp").Exists ? variant + "-thumb.webp" : url;
                        return new CarouselPhoto(url, srcSet, thumbnail);
                    }).ToArray());
            })!;
        }
    }
}

using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class HomeController(IWeddingSettingsService settings, IWebHostEnvironment environment) : Controller
    {
        public async Task<IActionResult> Index() => View(new HomeViewModel(await settings.GetAsync(), GetCarouselImages()));

        public IActionResult Error() => View();

        private IReadOnlyList<string> GetCarouselImages()
        {
            var imagesDirectory = Path.Combine(environment.WebRootPath, "images");

            if (!Directory.Exists(imagesDirectory))
                return [];

            var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

            return Directory.EnumerateFiles(imagesDirectory, "carousel-*")
                .Where(path => validExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => $"/images/{Uri.EscapeDataString(Path.GetFileName(path))}")
                .ToList();
        }
    }
}

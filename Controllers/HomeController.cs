using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class HomeController(IWeddingSettingsService settings, IWebHostEnvironment environment) : Controller
    {
        public async Task<IActionResult> Index() => View(new HomeViewModel(await settings.GetAsync(), GetCarouselImages()));

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return ErrorView("Algo não correu como esperado", "Estamos a tratar deste imprevisto. Podes voltar ao início ou tentar novamente dentro de instantes.", "bi-heartbreak", "Erro temporário");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int id)
        {
            Response.StatusCode = id;

            return id switch
            {
                StatusCodes.Status404NotFound => ErrorView("Esta página já não está aqui", "O endereço pode estar incompleto ou a página pode ter mudado. Vamos ajudar-te a voltar ao caminho certo.", "bi-map", "Página não encontrada"),
                StatusCodes.Status403Forbidden => ErrorView("Não tens acesso a esta página", "Se acreditas que devias poder aceder, inicia sessão com a conta certa e volta a tentar.", "bi-shield-lock", "Acesso restrito"),
                StatusCodes.Status400BadRequest => ErrorView("Não foi possível concluir o pedido", "Confirma a informação preenchida e tenta novamente.", "bi-exclamation-circle", "Pedido inválido"),
                _ => ErrorView("Não foi possível abrir esta página", "Tenta novamente dentro de instantes. Se o problema continuar, fala connosco.", "bi-exclamation-circle", "Indisponível de momento")
            };
        }

        private ViewResult ErrorView(string title, string message, string icon, string label)
        {
            ViewData["ErrorTitle"] = title;
            ViewData["ErrorMessage"] = message;
            ViewData["ErrorIcon"] = icon;
            ViewData["ErrorLabel"] = label;

            return View("Error");
        }

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

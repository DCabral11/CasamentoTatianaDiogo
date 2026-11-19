#region References

using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace CasamentoTatianaDiogo.Controllers
{
    public class HomeController(IWeddingSettingsService settings, CarouselPhotoService photos) : Controller
    {
        public async Task<IActionResult> Index() => View(new HomeViewModel(await settings.GetPresentationAsync(), photos.GetPhotos()));

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;

            return ErrorView("Algo não correu como esperado", "Estamos a tratar deste imprevisto. Podes voltar ao início ou tentar novamente dentro de instantes.", "bi-heartbreak", "Erro temporário");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status([FromRoute] int id)
        {
            if (id < 400 || id > 599)
                id = StatusCodes.Status404NotFound;

            Response.StatusCode = id;

            return id switch
            {
                StatusCodes.Status404NotFound => ErrorView("Esta página já não está aqui", "O endereço pode estar incompleto ou a página pode ter mudado. Vamos ajudar-te a voltar ao caminho certo.", "bi-map", "Página não encontrada"),
                StatusCodes.Status403Forbidden => ErrorView("Não tens acesso a esta página", "Se acreditas que devias poder aceder, inicia sessão com a conta certa e volta a tentar.", "bi-shield-lock", "Acesso restrito"),
                StatusCodes.Status400BadRequest => ErrorView("Não foi possível concluir o pedido", "Confirma a informação preenchida e tenta novamente.", "bi-exclamation-circle", "Pedido inválido"),
                _ => ErrorView("Não foi possível abrir esta página", "Tenta novamente dentro de instantes. Se o problema continuar, fala connosco.", "bi-exclamation-circle", "Indisponível de momento")
            };
        }

        #region Private Methods

        private ViewResult ErrorView(string title, string message, string icon, string label)
        {
            ViewData["ErrorTitle"] = title;
            ViewData["ErrorMessage"] = message;
            ViewData["ErrorIcon"] = icon;
            ViewData["ErrorLabel"] = label;

            return View("Error");
        }

        #endregion
    }
}

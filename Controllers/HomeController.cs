using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class HomeController(IWeddingSettingsService settings) : Controller
    {
        public async Task<IActionResult> Index() => View(new HomeViewModel(await settings.GetAsync(), new[] { "images/homepage-couple.jpeg", "/images/homepage-couple.jpeg" }));

        public IActionResult Error() => View();
    }
}
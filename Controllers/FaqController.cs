using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FaqController(FaqService faqs) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!(await faqs.GetSettingsAsync()).IsEnabled) return NotFound();
        return View(await faqs.GetPageAsync());
    }
}

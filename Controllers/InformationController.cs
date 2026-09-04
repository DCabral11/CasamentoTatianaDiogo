using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers
{
    public class InformationController(IWeddingSettingsService settings, ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View(new InformationViewModel(await settings.GetAsync(), await db.TimelineEvents.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ThenBy(t => t.EventDateTime).ToListAsync()));
    }
}

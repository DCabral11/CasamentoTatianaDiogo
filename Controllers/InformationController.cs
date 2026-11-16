using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers
{
    public class InformationController(IWeddingSettingsService settings, ApplicationDbContext db, WeddingClock clock) : Controller
    {
        public async Task<IActionResult> Index() => View(new InformationViewModel(await settings.GetAsync(), await db.TimelineEvents.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ThenBy(t => t.EventDateTime).ToListAsync()));

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            var wedding = await settings.GetAsync();
            if (clock.EventStartUtc(wedding.WeddingDateTime) is not DateTime start) return NotFound();
            return File(WeddingCalendar.Create(wedding, start), "text/calendar; charset=utf-8", "casamento.ics");
        }
    }
}

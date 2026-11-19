#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers
{
    public class InformationController(IWeddingSettingsService settings, ApplicationDbContext db, WeddingClock clock, PublicContentCache cache) : Controller
    {
        public async Task<IActionResult> Index() => View(new InformationViewModel(await settings.GetPresentationAsync(),
            await cache.GetAsync<IReadOnlyList<Models.TimelineEvent>>("timeline", async () =>
                await db.TimelineEvents.AsNoTracking().Where(t => t.IsActive)
                    .OrderBy(t => t.DisplayOrder).ThenBy(t => t.EventDateTime).ToListAsync())));

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            var wedding = await settings.GetAsync();

            if (clock.EventStartUtc(wedding.WeddingDateTime) is not DateTime start) 
                return NotFound();
            
            return File(WeddingCalendar.Create(wedding, start, clock.EventStartUtc(wedding.WeddingEndDateTime)), "text/calendar; charset=utf-8", "casamento.ics");
        }
    }
}

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
    public class InformationController(IWeddingSettingsService settings, ApplicationDbContext db, WeddingClock clock, PublicContentCache cache, SiteTranslationService translations) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var originals = await cache.GetAsync<IReadOnlyList<Models.TimelineEvent>>("timeline", async () =>
                await db.TimelineEvents.AsNoTracking().Where(t => t.IsActive)
                    .OrderBy(t => t.DisplayOrder).ThenBy(t => t.EventDateTime).ToListAsync());
            var timeline = new List<Models.TimelineEvent>();
            foreach (var item in originals) timeline.Add(new()
            {
                Id = item.Id, EventDateTime = item.EventDateTime, DisplayOrder = item.DisplayOrder, IconName = item.IconName, IsActive = item.IsActive,
                Title = await translations.TranslateAsync($"TimelineEvent.{item.Id}.Title", item.Title),
                Description = await translations.TranslateAsync($"TimelineEvent.{item.Id}.Description", item.Description)
            });
            return View(new InformationViewModel(await settings.GetPresentationAsync(), timeline));
        }

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

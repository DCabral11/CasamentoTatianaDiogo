using static CasamentoTatianaDiogo.Services.AdminFilterOptions;
using CasamentoTatianaDiogo.ViewModels.Admin;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class TimelineController(ApplicationDbContext db, CasamentoTatianaDiogo.Services.WeddingClock clock) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var rows = await db.TimelineEvents.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync();
            return View("~/Views/Admin/Timeline/Index.cshtml", new AdminListFilters<TimelineEvent>(this, rows, "Título ou descrição", x => $"{x.Title} {x.Description}")
                .Select("active", "Visível no site", YesNo, x => Flag(x.IsActive))
                .Dates("Data do evento", x => DateOnly.FromDateTime(x.EventDateTime)).Results());
        }

        public IActionResult Create() => View("~/Views/Admin/Timeline/Edit.cshtml", new TimelineEvent
        {
            EventDateTime = clock.Now
        });

        public async Task<IActionResult> Edit(int id) => View("~/Views/Admin/Timeline/Edit.cshtml", await db.TimelineEvents.FindAsync(id));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(TimelineEvent m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Timeline/Edit.cshtml", m);

            m.EventDateTime = new DateTime(
                m.EventDateTime.Year,
                m.EventDateTime.Month,
                m.EventDateTime.Day,
                m.EventDateTime.Hour,
                m.EventDateTime.Minute,
                0,
                m.EventDateTime.Kind);

            if (m.Id == 0)
                db.Add(m);
            else
                db.Update(m);

            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var x = await db.TimelineEvents.FindAsync(id);

            if (x != null)
            {
                db.Remove(x);

                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

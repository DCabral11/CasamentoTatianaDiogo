using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class TimelineController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Timeline/Index.cshtml", await db.TimelineEvents.OrderBy(x => x.DisplayOrder).ToListAsync());

        public IActionResult Create() => View("~/Views/Admin/Timeline/Edit.cshtml", new TimelineEvent
        {
            EventDateTime = DateTime.Now
        });

        public async Task<IActionResult> Edit(int id) => View("~/Views/Admin/Timeline/Edit.cshtml", await db.TimelineEvents.FindAsync(id));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(TimelineEvent m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Timeline/Edit.cshtml", m);

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

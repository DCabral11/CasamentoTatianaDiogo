#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CasamentoTatianaDiogo.Services.AdminFilterOptions;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Photos/[action]/{id?}")]
    public class PhotoAdminController(ApplicationDbContext db, WeddingClock clock) : Controller
    {
        #region Controller - General Methods

        public async Task<IActionResult> Index()
        {
            var page = new AdminQueryPage<PhotoUpload>(this, db.PhotoUploads.AsNoTracking(), "Convidado, Ficheiro ou Notas");
            var types = await db.PhotoUploads.Select(x => x.ContentType).Distinct().OrderBy(x => x).ToListAsync(page.Cancellation);

            page.Select("type", "Formato", States(types), value => p => p.ContentType == value);

            var from = page.Date("from", "Envio (Hora de Lisboa) Desde");
            var until = page.Date("until", "Envio (Hora de Lisboa) Até");

            if (from > until)
                page.InvalidDates();

            if (from.HasValue && from > DateOnly.MinValue)
            {
                var start = clock.StartOfDayUtc(from.Value);

                page.Where(p => p.UploadedAt >= start);
            }

            if (until.HasValue && until < DateOnly.MaxValue)
            {
                var end = clock.StartOfDayUtc(until.Value.AddDays(1));

                page.Where(p => p.UploadedAt < end);
            }

            var query = await page.BuildAsync(q => q.OrderByDescending(p => p.UploadedAt).ThenByDescending(p => p.Id), p => new { p.Id, p.GuestName, p.OriginalFileName, p.Notes }, p => p.Id, p => $"{p.GuestName} {p.OriginalFileName} {p.Notes}");

            return View("~/Views/Admin/Photos/Index.cshtml", await query.ToListAsync(page.Cancellation));
        }

        public async Task<IActionResult> Settings() => View("~/Views/Admin/Photos/Settings.cshtml", await db.PhotoUploadSettings.FirstAsync());

        #endregion

        #region Controller - POST Methods

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(PhotoUploadSettings m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Photos/Settings.cshtml", m);

            db.Update(m);

            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var x = await db.PhotoUploads.FindAsync(id);

            if (x != null)
            {
                db.Remove(x);

                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}

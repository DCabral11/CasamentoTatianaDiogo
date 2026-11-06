using static CasamentoTatianaDiogo.Services.AdminFilterOptions;
using CasamentoTatianaDiogo.ViewModels.Admin;
using CasamentoTatianaDiogo.Services;
#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Photos/[action]/{id?}")]
    public class PhotoAdminController(ApplicationDbContext db, WeddingClock clock) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var rows = await db.PhotoUploads.AsNoTracking().OrderByDescending(p => p.UploadedAt).ThenByDescending(p => p.Id).ToListAsync();
            return View("~/Views/Admin/Photos/Index.cshtml", new AdminListFilters<PhotoUpload>(this, rows, "Convidado, ficheiro ou notas", x => $"{x.GuestName} {x.OriginalFileName} {x.Notes}")
                .Select("type", "Formato", rows.Select(x => x.ContentType).Distinct().Order().Select(x => new AdminFilterOption(x, x)), x => x.ContentType)
                .Dates("Envio (hora de Lisboa)", x => DateOnly.FromDateTime(clock.ToLocal(x.UploadedAt))).Results());
        }

        public async Task<IActionResult> Settings() => View("~/Views/Admin/Photos/Settings.cshtml", await db.PhotoUploadSettings.FirstAsync());

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
    }
}

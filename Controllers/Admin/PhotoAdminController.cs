using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class PhotoAdminController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Photos/Index.cshtml", await db.PhotoUploads.OrderByDescending(p => p.UploadedAt).ToListAsync());

        public async Task<IActionResult> Settings() => View("~Views/Admin/Photos/Settings.cshtml", await db.PhotoUploadSettings.FirstAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(PhotoUploadSettings m)
        {
            if (!ModelState.IsValid)
                return View("~Views/Admin/Photos/Settings.cshtml", m);

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

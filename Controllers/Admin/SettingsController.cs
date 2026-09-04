using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class SettingsController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Settings/Index.cshtml", await db.WeddingSettings.FirstAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(WeddingSettings m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Settings/Index.cshtml", m);

            m.UpdatedAt = DateTime.Now;

            db.Update(m);
            await db.SaveChangesAsync();

            TempData["Success"] = "Settings saved.";

            return RedirectToAction(nameof(Index));
        }
    }
}

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class ContentController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Content/Index.cshtml", await db.SiteContents.OrderBy(content => content.Description).ToListAsync());

        public async Task<IActionResult> Edit(int id)
        {
            var content = await db.SiteContents.FindAsync(id);
            return content == null ? NotFound() : View("~/Views/Admin/Content/Edit.cshtml", content);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SiteContent model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Content/Edit.cshtml", model);

            db.Update(model);
            await db.SaveChangesAsync();
            TempData["Success"] = "Texto atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
    }
}

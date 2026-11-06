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
    [Route("Admin/[controller]/[action]/{id?}")]
    public class ContentController(ApplicationDbContext db) : Controller
    {
        /// <summary>
        /// Loads the content management page.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var rows = await db.SiteContents.AsNoTracking().OrderBy(x => x.Description).ThenBy(x => x.Id).ToListAsync();
            return View("~/Views/Admin/Content/Index.cshtml", new AdminListFilters<SiteContent>(this, rows, "Descrição, chave ou texto", x => $"{x.Description} {x.Key} {x.Value}")
                .Select("html", "Conteúdo HTML", YesNo, x => Flag(x.IsHtml)).Results());
        }

        /// <summary>
        /// Loads the content edit page.
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            var content = await db.SiteContents.FindAsync(id);

            return content == null ? NotFound() : View("~/Views/Admin/Content/Edit.cshtml", content);
        }

        /// <summary>
        /// Saves the content changes made in the edit page.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SiteContent model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Content/Edit.cshtml", model);

            db.Update(model);
            await db.SaveChangesAsync();

            TempData["Success"] = "Texto Atualizado com Sucesso!";

            return RedirectToAction(nameof(Index));
        }
    }
}
using static CasamentoTatianaDiogo.Services.AdminFilterOptions;
using CasamentoTatianaDiogo.ViewModels.Admin;
using CasamentoTatianaDiogo.ViewModels;
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
        private static readonly string[] TravelKeys = WeddingTravel.Topics.Select(topic => topic.Key).ToArray();

        [HttpGet]
        public async Task<IActionResult> Travel()
        {
            var texts = await db.SiteContents.Where(x => TravelKeys.Contains(x.Key))
                .ToDictionaryAsync(x => x.Key, x => x.Value);
            return View("~/Views/Admin/Content/Travel.cshtml", texts);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Travel(Dictionary<string, string?> guidance)
        {
            if (TravelKeys.Any(key => !guidance.ContainsKey(key)))
                return BadRequest();

            var saved = await db.SiteContents.Where(x => TravelKeys.Contains(x.Key)).ToDictionaryAsync(x => x.Key);
            foreach (var topic in WeddingTravel.Topics)
            {
                var value = guidance[topic.Key]?.Trim();
                var text = saved.GetValueOrDefault(topic.Key);
                if (string.IsNullOrEmpty(value))
                {
                    if (text != null) db.SiteContents.Remove(text);
                }
                else if (text != null) text.Value = value;
                else db.SiteContents.Add(new SiteContent { Key = topic.Key, Description = $"Informações · {topic.Title}", Value = value });
            }
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Não foi possível guardar as indicações. Revê o texto e tenta novamente.");
                return View("~/Views/Admin/Content/Travel.cshtml", guidance);
            }
            TempData["Success"] = "As informações práticas foram guardadas.";
            return RedirectToAction(nameof(Travel));
        }

        /// <summary>
        /// Loads the content management page.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var rows = await db.SiteContents.AsNoTracking().Where(x => !TravelKeys.Contains(x.Key))
                .OrderBy(x => x.Description).ThenBy(x => x.Id).ToListAsync();
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

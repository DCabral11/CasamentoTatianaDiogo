#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels;
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
        #region Properties

        private static readonly string[] TravelKeys = WeddingTravel.Topics.Select(topic => topic.Key).ToArray();
        private static readonly string[] ManagedKeys = TravelKeys.Concat(WeddingPageMessages.Pages.SelectMany(p => p.Fields).Select(f => f.Key)).ToArray();

        #endregion

        #region Public Methods

        #region Default Controller Methods

        public async Task<IActionResult> Index()
        {
            var rows = await db.SiteContents.AsNoTracking().Where(x => ManagedKeys.Contains(x.Key)).OrderBy(x => x.Description).ThenBy(x => x.Id).ToListAsync();

            return View("~/Views/Admin/Content/Index.cshtml", new AdminListFilters<SiteContent>(this, rows, "Descrição, chave ou texto", x => $"{x.Description} {x.Key} {x.Value}").Select("html", "Conteúdo HTML", AdminFilterOptions.YesNo, x => AdminFilterOptions.Flag(x.IsHtml)).Results());
        }

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

            TempData["Success"] = "Texto Atualizado com Sucesso!";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region GET Methods

        [HttpGet]
        public async Task<IActionResult> PhaseMessages(string page = "Home")
        {
            var section = WeddingPageMessages.Find(page);

            if (section == null)
                return NotFound();

            ViewData["MessagePage"] = section;

            var keys = section.Fields.Select(f => f.Key).ToArray();
            var texts = await db.SiteContents.Where(x => keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value);

            return View("~/Views/Admin/Content/PhaseMessages.cshtml", texts);
        }

        [HttpGet]
        public async Task<IActionResult> Travel()
        {
            var texts = await db.SiteContents.Where(x => TravelKeys.Contains(x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value);

            return View("~/Views/Admin/Content/Travel.cshtml", texts);
        }

        #endregion

        #region POST Methods

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PhaseMessages(string page, Dictionary<string, string?> guidance)
        {
            var section = WeddingPageMessages.Find(page);

            if (section == null)
                return NotFound();

            ViewData["MessagePage"] = section;

            var fields = section.Fields.ToDictionary(f => f.Key, f => $"{section.Title} · {f.Label}");

            return await SaveTexts(fields, guidance, nameof(PhaseMessages), new { page }, "Mensagens guardadas.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Travel(Dictionary<string, string?> guidance) => SaveTexts(WeddingTravel.Topics.ToDictionary(t => t.Key, t => $"Informações · {t.Title}"), guidance, nameof(Travel), null, "As informações práticas foram guardadas.");

        #endregion

        #endregion

        #region Private Methods

        private async Task<IActionResult> SaveTexts(Dictionary<string, string> fields, Dictionary<string, string?> guidance, string action, object? route, string success)
        {
            var keys = fields.Keys.ToArray();

            if (keys.Any(key => !guidance.ContainsKey(key)))
                return BadRequest();

            var saved = await db.SiteContents.Where(x => keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key);

            foreach (var (key, description) in fields)
            {
                var value = guidance[key]?.Trim();
                var text = saved.GetValueOrDefault(key);

                if (string.IsNullOrEmpty(value))
                {
                    if (text != null)
                        db.SiteContents.Remove(text);
                }
                else if (text != null)
                    text.Value = value;
                else
                    db.SiteContents.Add(new SiteContent { Key = key, Description = description, Value = value });
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Não foi possível guardar as indicações. Revê o texto e tenta novamente.");

                return View($"~/Views/Admin/Content/{action}.cshtml", guidance);
            }

            TempData["Success"] = success;

            return RedirectToAction(action, route);
        }

        #endregion
    }
}
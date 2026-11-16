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
    public class InvitationGroupsController(ApplicationDbContext db) : Controller
    {
        /// <summary>
        /// Handles the index page for invitation groups, retrieving a list of families and their associated guests from the database and passing it to the view for display.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var page = new AdminQueryPage<Family>(this, db.Families.AsNoTracking(), "Família, referência ou nome de convidado");
            page.Select("groupRsvp", "Permite resposta familiar", YesNo, value => f => f.AllowGroupRsvp == (value == "yes"));
            page.Select("status", "Situação das respostas", Options(("empty", "Sem convidados"), ("pending", "Com respostas em falta"), ("complete", "Todos responderam")),
                value => value switch
                {
                    "empty" => f => !f.Guests.Any(),
                    "pending" => f => f.Guests.Any(g => g.CurrentStatus == Models.Enums.RsvpStatus.Pending),
                    _ => f => f.Guests.Any() && !f.Guests.Any(g => g.CurrentStatus == Models.Enums.RsvpStatus.Pending)
                });
            var query = await page.BuildAsync(q => q.OrderBy(f => f.Name).ThenBy(f => f.Id),
                f => new { f.Id, f.Name, f.GroupCode, Names = f.Guests.OrderBy(g => g.Id).Select(g => g.DisplayName).ToList() },
                f => f.Id, f => $"{f.Name} {f.GroupCode} {string.Join(' ', f.Names)}");
            return View("~/Views/Admin/InvitationGroups/Index.cshtml", await query.ToListAsync(page.Cancellation));
        }

        /// <summary>
        /// Handles the creation of a new invitation group by returning the edit view with a new Family model instance, allowing the user to input details for the new group.
        /// </summary>
        public IActionResult Create() => View("~/Views/Admin/InvitationGroups/Edit.cshtml", new Family());

        /// <summary>
        /// Handles the editing of an existing invitation group by retrieving the specified Family model instance from the database based on the provided ID and passing it to the edit view for modification.
        /// </summary>
        public async Task<IActionResult> Edit(int id) => View("~/Views/Admin/InvitationGroups/Edit.cshtml", await db.Families.FindAsync(id));

        /// <summary>
        /// Handles the saving of an invitation group, either creating a new entry or updating an existing one in the database based on the provided Family model instance, and then redirects to the index page upon successful save.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Family m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/InvitationGroups/Edit.cshtml", m);

            if (m.Id == 0)
                db.Add(m);
            else
            {
                db.Update(m);
                m.RsvpVersion = Guid.NewGuid();
            }

            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                ModelState.AddModelError(string.Empty, "Este convite foi alterado por outra pessoa. Os teus dados não foram guardados. Abre a versão mais recente antes de continuar.");
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View("~/Views/Admin/InvitationGroups/Edit.cshtml", m);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Handles the deletion of an invitation group by retrieving the specified Family model instance from the database based on the provided ID, removing it from the database, and then redirecting to the index page upon successful deletion.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var x = await db.Families.FindAsync(id);

            if (x != null)
            {
                db.Remove(x);

                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

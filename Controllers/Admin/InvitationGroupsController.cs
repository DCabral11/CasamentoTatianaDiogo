#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CasamentoTatianaDiogo.Services.AdminFilterOptions;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class InvitationGroupsController(ApplicationDbContext db) : Controller
    {
        #region Controller - General Methods

        public async Task<IActionResult> Index()
        {
            var page = new AdminQueryPage<Family>(this, db.Families.AsNoTracking(), "Família, Referência ou Nome de Convidado");

            page.Select("groupRsvp", "Permite resposta familiar", YesNo, value => f => f.AllowGroupRsvp == (value == "yes"));
            page.Select("status", "Situação das Respostas", Options(("empty", "Sem Convidados"), ("pending", "Com Respostas em Falta"), ("complete", "Todos Responderam")),
                value => value switch
                {
                    "empty" => f => !f.Guests.Any(),
                    "pending" => f => f.Guests.Any(g => g.CurrentStatus == RsvpStatus.Pending),
                    _ => f => f.Guests.Any() && !f.Guests.Any(g => g.CurrentStatus == RsvpStatus.Pending)
                });

            var query = await page.BuildAsync(q => q.OrderBy(f => f.Name).ThenBy(f => f.Id), f => new 
            { 
                f.Id, f.Name, f.GroupCode, Names = f.Guests.OrderBy(g => g.Id).Select(g => g.DisplayName).ToList() 
            }, f => f.Id, f => $"{f.Name} {f.GroupCode} {string.Join(' ', f.Names)}");

            return View("~/Views/Admin/InvitationGroups/Index.cshtml", await query.ToListAsync(page.Cancellation));
        }

        public IActionResult Create() => View("~/Views/Admin/InvitationGroups/Edit.cshtml", new Family());

        public async Task<IActionResult> Edit(int id) => View("~/Views/Admin/InvitationGroups/Edit.cshtml", await db.Families.FindAsync(id));

        #endregion

        #region Controller - POST Methods

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

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();

                ModelState.AddModelError(string.Empty, "Este convite foi alterado por outra pessoa. Os teus dados não foram guardados. Abre a versão mais recente antes de continuar.");
                Response.StatusCode = StatusCodes.Status409Conflict;

                return View("~/Views/Admin/InvitationGroups/Edit.cshtml", m);
            }

            return RedirectToAction(nameof(Index));
        }

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

        #endregion
    }
}
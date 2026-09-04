using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class InvitationGroupsController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/InvitationGroups/Index.cshtml", await db.Families.Include(f => f.Guests).ToListAsync());

        public IActionResult Create() => View("~/Views/Admin/InvitationGroups/Edit.cshtml", new Family());

        public async Task<IActionResult> Edit(int id) => View("~/Views/Admin/InvitationGroups/Edit.cshtml", await db.Families.FindAsync(id));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Family m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/InvitationGroups/Edit.cshtml", m);

            if (m.Id == 0)
                db.Add(m);
            else
                db.Update(m);

            await db.SaveChangesAsync();

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
    }
}

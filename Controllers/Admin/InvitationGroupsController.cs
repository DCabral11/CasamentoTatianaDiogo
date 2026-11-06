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
        public async Task<IActionResult> Index() => View("~/Views/Admin/InvitationGroups/Index.cshtml", await db.Families.Include(f => f.Guests).ToListAsync());

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
                db.Update(m);

            await db.SaveChangesAsync();

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

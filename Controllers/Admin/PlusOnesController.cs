using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class PlusOnesController(ApplicationDbContext db) : Controller
    {
        private async Task LoadGuestsAsync() =>
            ViewBag.Guests = new SelectList(await db.Guests.OrderBy(guest => guest.DisplayName).ToListAsync(), "Id", "DisplayName");

        public async Task<IActionResult> Index() =>
            View("~/Views/Admin/PlusOnes/Index.cshtml", await db.PlusOnes.Include(plusOne => plusOne.MainGuest).OrderBy(plusOne => plusOne.PlusOneDisplayName).ToListAsync());

        public async Task<IActionResult> Create()
        {
            await LoadGuestsAsync();
            return View("~/Views/Admin/PlusOnes/Edit.cshtml", new PlusOne());
        }

        public async Task<IActionResult> Edit(int id)
        {
            var plusOne = await db.PlusOnes.FindAsync(id);
            if (plusOne == null)
                return NotFound();

            await LoadGuestsAsync();
            return View("~/Views/Admin/PlusOnes/Edit.cshtml", plusOne);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(PlusOne model)
        {
            if (string.IsNullOrWhiteSpace(model.PlusOneDisplayName))
                model.PlusOneDisplayName = $"{model.PlusOneFirstName} {model.PlusOneLastName}".Trim();

            if (!ModelState.IsValid)
            {
                await LoadGuestsAsync();
                return View("~/Views/Admin/PlusOnes/Edit.cshtml", model);
            }

            if (model.Id == 0)
                db.PlusOnes.Add(model);
            else
                db.Update(model);

            await db.SaveChangesAsync();
            TempData["Success"] = "Acompanhante guardado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var plusOne = await db.PlusOnes.FindAsync(id);
            if (plusOne != null)
            {
                db.PlusOnes.Remove(plusOne);
                await db.SaveChangesAsync();
                TempData["Success"] = "Acompanhante removido.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

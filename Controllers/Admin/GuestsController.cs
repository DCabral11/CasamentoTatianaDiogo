using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class GuestsController(ApplicationDbContext db) : Controller
    {
        async Task Load()
        {
            ViewBag.Families = new SelectList(await db.Families.OrderBy(f => f.Name).ToListAsync(), "Id", "Name");
        }

        public async Task<IActionResult> Index() => View("~/Views/Admin/Guests/Index.cshtml", await db.Guests.Include(g => g.Family).OrderBy(g => g.DisplayName).ToListAsync());

        public async Task<IActionResult> Create()
        {
            await Load();

            return View("~/Views/Admin/Guests/Edit.cshtml", new Guest());
        }

        public async Task<IActionResult> Edit(int id)
        {
            await Load();

            return View("~/Views/Admin/Guests/Edit.cshtml", await db.Guests.FindAsync(id));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Guest m)
        {
            if (string.IsNullOrWhiteSpace(m.DisplayName))
                m.DisplayName = $"{m.FirstName} {m.LastName}";

            if (!ModelState.IsValid)
            {
                await Load();

                return View("~/Views/Admin/Guests/Edit.cshtml", m);
            }

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
            var x = await db.Guests.FindAsync(id);

            if (x != null)
            {
                db.Remove(x);

                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<FileResult> ExportCsv()
        {
            var rows = await db.Guests.Include(g => g.Family).ToListAsync();

            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms, Encoding.UTF8);
            using var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

            csv.WriteRecords(rows.Select(g => new
            {
                g.DisplayName,
                g.Email,
                g.Phone,
                Family = g.Family!.Name,
                g.CurrentStatus,
                g.Notes
            }));

            sw.Flush();

            return File(ms.ToArray(), "text/csv", "guests.csv");
        }
    }
}

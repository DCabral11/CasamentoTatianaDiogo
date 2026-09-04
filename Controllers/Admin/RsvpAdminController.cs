using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models.Enums;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class RsvpAdminController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index(int? familyId) => View("~/Views/Admin/Rsvp/Index.cshtml", await db.Guests.Include(g => g.Family).Include(g => g.RsvpResponses).Where(g => !familyId.HasValue || g.FamilyId == familyId).OrderBy(g => g.DisplayName).ToListAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, RsvpStatus status)
        {
            var g = await db.Guests.FindAsync(id);

            if (g != null)
            {
                g.CurrentStatus = status;

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
                Family = g.Family!.Name,
                g.CurrentStatus,
                g.Email,
                g.Phone,
                g.Notes
            }));

            sw.Flush();

            return File(ms.ToArray(), "text/csv", "rsvp.csv");
        }
    }
}

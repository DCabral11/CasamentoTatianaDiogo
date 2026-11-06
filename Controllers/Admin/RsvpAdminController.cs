using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models.Enums;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using CasamentoTatianaDiogo.Services;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Rsvp/[action]/{id?}")]
    public class RsvpAdminController(ApplicationDbContext db, WeddingClock clock) : Controller
    {
        public async Task<IActionResult> Index(int? familyId)
        {
            ViewData["FamilyId"] = familyId;
            return View("~/Views/Admin/Rsvp/Index.cshtml", await db.Guests
            .Include(g => g.Family)
            .Include(g => g.RsvpResponses)
            .Where(g => g.RsvpResponses.Any() && (!familyId.HasValue || g.FamilyId == familyId))
            .OrderBy(g => g.DisplayName)
            .ToListAsync());
        }

        [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<FileResult> ExportExcel(int? familyId)
        {
            var guests = await db.Guests.AsNoTracking().AsSplitQuery()
                .Include(guest => guest.Family)
                .Include(guest => guest.PlusOnes)
                .Include(guest => guest.RsvpResponses).ThenInclude(response => response.PlusOne)
                .Where(guest => !familyId.HasValue || guest.FamilyId == familyId)
                .OrderBy(guest => guest.Family!.Name).ThenBy(guest => guest.DisplayName).ThenBy(guest => guest.Id)
                .ToListAsync();
            var contacts = await db.RsvpInvitationContacts.AsNoTracking()
                .Where(contact => !familyId.HasValue || contact.FamilyId == familyId)
                .ToDictionaryAsync(contact => contact.FamilyId);
            var bytes = RsvpExcelExport.Create(guests, contacts, clock, familyId);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"historico-rsvp-{clock.Now:yyyyMMdd-HHmm}.xlsx");
        }

        public async Task<IActionResult> Details(int id)
        {
            var response = await db.RsvpResponses
                .Include(r => r.Guest)!.ThenInclude(g => g!.Family)
                .Include(r => r.PlusOne)
                .Where(r => r.GuestId == id)
                .OrderByDescending(r => r.SubmittedAt)
                .FirstOrDefaultAsync();

            if (response == null) return NotFound();
            ViewData["VerifiedInvitationContact"] = await db.RsvpInvitationContacts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.FamilyId == response.Guest!.FamilyId);
            return View("~/Views/Admin/Rsvp/Details.cshtml", response);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? familyId)
        {
            var guest = await db.Guests
                .Include(g => g.RsvpResponses)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                TempData["Error"] = "Não encontrámos a resposta que pretendia apagar.";
            }
            else if (guest.RsvpResponses.Count == 0)
            {
                TempData["Info"] = $"{guest.DisplayName} ainda não tinha uma resposta enviada.";
            }
            else
            {
                db.RsvpResponses.RemoveRange(guest.RsvpResponses);
                guest.CurrentStatus = RsvpStatus.Pending;
                await db.SaveChangesAsync();
                TempData["Success"] = $"A resposta de {guest.DisplayName} foi apagada. O estado voltou a ‘Por responder’.";
            }

            return RedirectToAction(nameof(Index), new { familyId });
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

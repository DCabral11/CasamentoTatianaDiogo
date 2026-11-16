using CasamentoTatianaDiogo.Services.Interfaces;
using static CasamentoTatianaDiogo.Services.AdminFilterOptions;
using CasamentoTatianaDiogo.ViewModels.Admin;
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
    public class RsvpAdminController(ApplicationDbContext db, WeddingClock clock, IWeddingSettingsService settingsService) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var query = await GuestQueryAsync(this, db, settingsService);
            var rows = await query.Select(g => new RsvpAdminRow(g.Id, g.DisplayName, g.Family!.Name,
                g.Family.RsvpVersion, g.CurrentStatus, g.IsPlusOne, g.MainGuestId, g.RsvpResponses.Any())).ToListAsync(HttpContext.RequestAborted);
            return View("~/Views/Admin/Rsvp/Index.cshtml", rows);
        }

        [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<FileResult> ExportExcel(int? familyId)
        {
            var query = await GuestQueryAsync(this, db, settingsService, export: true);
            var guests = await query.AsSplitQuery().Include(g => g.Family).Include(g => g.PlusOnes)
                .Include(g => g.RsvpResponses).ThenInclude(r => r.PlusOne)
                .OrderBy(g => g.Family!.Name).ThenBy(g => g.DisplayName).ThenBy(g => g.Id).ToListAsync(HttpContext.RequestAborted);
            var contacts = await db.RsvpInvitationContacts.AsNoTracking()
                .Where(contact => !familyId.HasValue || contact.FamilyId == familyId)
                .ToDictionaryAsync(contact => contact.FamilyId);
            var formats = await settingsService.GetAsync();
            var bytes = RsvpExcelExport.Create(guests, contacts, clock, familyId, formats);
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
        public async Task<IActionResult> Delete(int id, int? familyId, Guid rsvpVersion)
        {
            var guest = await db.Guests
                .Include(g => g.Family)
                .Include(g => g.RsvpResponses)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                TempData["Error"] = "Não encontrámos a resposta que pretendia apagar.";
            }
            else if (guest.Family!.RsvpVersion != rsvpVersion)
            {
                TempData["Error"] = "Este convite foi alterado desde que abriste a lista. Nada foi apagado; revê a resposta mais recente.";
            }
            else if (guest.RsvpResponses.Count == 0)
            {
                TempData["Info"] = $"{guest.DisplayName} ainda não tinha uma resposta enviada.";
            }
            else
            {
                db.RsvpResponses.RemoveRange(guest.RsvpResponses);
                guest.CurrentStatus = RsvpStatus.Pending;
                foreach (var companion in await db.Guests.Where(g => g.MainGuestId == guest.Id).ToListAsync())
                    companion.CurrentStatus = RsvpStatus.Pending;
                guest.Family!.RsvpVersion = Guid.NewGuid();
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException)
                {
                    db.ChangeTracker.Clear();
                    TempData["Error"] = "A resposta foi alterada entretanto. Nada foi apagado; revê a versão atual antes de tentar novamente.";
                    return RedirectToAction(nameof(Index), new { familyId });
                }
                TempData["Success"] = $"A resposta de {guest.DisplayName} foi apagada. O estado voltou a ‘Por responder’.";
            }

            return RedirectToAction(nameof(Index), new { familyId });
        }

        public async Task<FileResult> ExportCsv()
        {
            var query = await GuestQueryAsync(this, db, settingsService, export: true);
            var rows = await query.Include(g => g.Family).ToListAsync(HttpContext.RequestAborted);

            var formats = await settingsService.GetAsync();
            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms, Encoding.UTF8);
            using var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

            csv.WriteRecords(rows.Select(g => new
            {
                g.DisplayName,
                Family = g.Family!.Name,
                g.CurrentStatus,
                Email = ContactFormats.Email(g.Email, formats)?.Display ?? g.Email,
                Phone = ContactFormats.Phone(g.Phone, formats)?.Display ?? g.Phone,
                g.Notes
            }));

            sw.Flush();

            return File(ms.ToArray(), "text/csv", "rsvp.csv");
        }
    }
}

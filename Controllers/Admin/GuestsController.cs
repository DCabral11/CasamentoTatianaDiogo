#region References

using CasamentoTatianaDiogo.Common.Errors;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Text;
using static CasamentoTatianaDiogo.Services.AdminFilterOptions;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class GuestsController(ApplicationDbContext db, IWebHostEnvironment environment, IAppMessageService messages, IWeddingSettingsService settingsService) : Controller
    {
        #region Controller - General Methods

        public async Task<IActionResult> Index()
        {
            var query = await GuestQueryAsync(this, db, settingsService);

            return View("~/Views/Admin/Guests/Index.cshtml", await query.Include(g => g.MainGuest).ToListAsync(HttpContext.RequestAborted));
        }

        public async Task<IActionResult> Create()
        {
            await Load();

            return View("~/Views/Admin/Guests/Edit.cshtml", new Guest());
        }

        public async Task<IActionResult> Edit(int id)
        {
            var guest = await db.Guests.FindAsync(id);

            if (guest == null)
                return NotFound();

            await Load(id);

            return View("~/Views/Admin/Guests/Edit.cshtml", guest);
        }

        #endregion

        #region Controller - POST Methods

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Save([Bind("Id,FamilyId,FirstName,LastName,DisplayName,Email,Phone,AvatarFileName,AvatarScale,AvatarPositionX,AvatarPositionY,AllowPlusOne,IsPlusOne,MainGuestId,IsChild,Notes")] Guest m)
        {
            var id = m.Id;

            return db.Database.CreateExecutionStrategy().ExecuteAsync(() => { m.Id = id; return SaveCore(m); }); 
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var x = await db.Guests.FindAsync(id);

            if (x != null)
            {
                if (await db.Guests.AnyAsync(g => g.MainGuestId == id) || await db.RsvpResponses.AnyAsync(r => r.GuestId == id || (x.IsPlusOne && r.GuestId == x.MainGuestId) || (r.PlusOne != null && r.PlusOne.CompanionGuestId == id)))
                {
                    TempData["Error"] = "Este convidado tem acompanhantes ou uma resposta RSVP associada. Revê essas ligações antes de o remover.";
                    
                    return RedirectToAction(nameof(Index));
                }

                var link = await db.PlusOnes.SingleOrDefaultAsync(p => p.CompanionGuestId == id);

                if (link != null)
                {
                    link.CompanionGuestId = null;
                    link.IsArchived = true;
                }

                var family = await db.Families.FindAsync(x.FamilyId);

                if (family != null)
                    family.RsvpVersion = Guid.NewGuid();

                db.Remove(x);

                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Controller - Functionality Methods

        public async Task<FileResult> ExportCsv()
        {
            var query = await GuestQueryAsync(this, db, settingsService, export: true);
            var rows = await query.Include(g => g.Family).Include(g => g.MainGuest).ToListAsync(HttpContext.RequestAborted);

            var formats = await settingsService.GetAsync();
            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms, Encoding.UTF8);
            using var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

            csv.WriteRecords(rows.Select(g => new
            {
                g.DisplayName,
                Email = ContactFormats.Email(g.Email, formats)?.Display ?? g.Email,
                Phone = ContactFormats.Phone(g.Phone, formats)?.Display ?? g.Phone,
                g.AllowPlusOne,
                g.IsPlusOne,
                Accompanies = g.MainGuest?.DisplayName,
                Family = g.Family!.Name,
                g.CurrentStatus,
                g.Notes
            }));

            sw.Flush();

            return File(ms.ToArray(), "text/csv", "guests.csv");
        }

        #endregion

        #region Private Methods

        private async Task Load(int guestId = 0)
        {
            ViewBag.MainGuests = new SelectList(await db.Guests
                .AsNoTracking()
                .Where(g => g.Id != guestId && !g.IsPlusOne && g.AllowPlusOne)
                .OrderBy(g => g.DisplayName)
                .Select(g => new
                {
                    g.Id, 
                    Name = g.DisplayName + " - " + g.Family!.Name + " (#" + g.Id + ")"
                })
                .ToListAsync(), "Id", "Name");

            var families = await db.Families.OrderBy(f => f.Name).ThenBy(f => f.GroupCode).Select(f => new
            {
                f.Id,
                Name = string.IsNullOrWhiteSpace(f.GroupCode) ? f.Name : $"{f.Name} — {f.GroupCode}"
            }).ToListAsync();

            ViewBag.Families = new SelectList(families, "Id", "Name");

            var avatarsDirectory = Path.Combine(environment.WebRootPath, "images", "guests");
            var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            var avatars = Directory.Exists(avatarsDirectory)
                ? Directory.EnumerateFiles(avatarsDirectory)
                    .Where(path => validExtensions.Contains(Path.GetExtension(path)))
                    .Select(Path.GetFileName)
                    .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];

            ViewBag.Avatars = new SelectList(avatars);
        }

        private async Task<IActionResult> SaveCore(Guest m)
        {
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var original = m.Id == 0 ? null : await db.Guests.FindAsync(m.Id);

            if (m.Id != 0 && original == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(m.DisplayName))
            {
                m.DisplayName = $"{m.FirstName} {m.LastName}";

                ModelState.Remove(nameof(m.DisplayName));
            }

            var settings = await settingsService.GetAsync();

            m.Phone = ContactFormats.SavePhone(m.Phone, settings, e => ModelState.AddModelError(nameof(m.Phone), e));
            m.Email = ContactFormats.SaveEmail(m.Email, settings, 200, e => ModelState.AddModelError(nameof(m.Email), e));

            foreach (var error in await GuestCompanions.ValidateAsync(db, m))
                ModelState.AddModelError(error.Field, error.Message);

            if (!await db.Families.AnyAsync(x => x.Id == m.FamilyId))
                ModelState.AddModelError(nameof(m.FamilyId), "Seleciona uma família válida.");

            var avatarsDirectory = Path.Combine(environment.WebRootPath, "images", "guests");

            if (!string.IsNullOrWhiteSpace(m.AvatarFileName))
            {
                m.AvatarFileName = Path.GetFileName(m.AvatarFileName);

                if (!System.IO.File.Exists(Path.Combine(avatarsDirectory, m.AvatarFileName)))
                    ModelState.AddModelError(nameof(m.AvatarFileName), messages.Get(ErrorCode.GuestAvatarNotFound));
            }

            if (!ModelState.IsValid)
            {
                await Load(m.Id);

                return View("~/Views/Admin/Guests/Edit.cshtml", m);
            }

            if (m.Id == 0)
                db.Add(m);
            else
            {
                m.CurrentStatus = original!.CurrentStatus;
                db.Entry(original).CurrentValues.SetValues(m);
            }

            var familyIds = new[] { m.FamilyId, original == null ? m.FamilyId : db.Entry(original).Property(x => x.FamilyId).OriginalValue };

            foreach (var family in await db.Families.Where(x => familyIds.Contains(x.Id)).ToListAsync())
                family.RsvpVersion = Guid.NewGuid();
            
            await db.SaveChangesAsync();
            await GuestCompanions.SyncAsync(db, original ?? m);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
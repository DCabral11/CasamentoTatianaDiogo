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
    public class GuestsController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
    {
        async Task Load()
        {
            var families = await db.Families
                .OrderBy(f => f.Name)
                .ThenBy(f => f.GroupCode)
                .Select(f => new
                {
                    f.Id,
                    Name = string.IsNullOrWhiteSpace(f.GroupCode) ? f.Name : $"{f.Name} — {f.GroupCode}"
                })
                .ToListAsync();

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

            var avatarsDirectory = Path.Combine(environment.WebRootPath, "images", "guests");
            if (!string.IsNullOrWhiteSpace(m.AvatarFileName))
            {
                m.AvatarFileName = Path.GetFileName(m.AvatarFileName);
                if (!System.IO.File.Exists(Path.Combine(avatarsDirectory, m.AvatarFileName)))
                    ModelState.AddModelError(nameof(m.AvatarFileName), "Seleciona uma imagem disponível na lista.");
            }

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

#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin"), AutoValidateAntiforgeryToken]
    [Route("Admin/Musicas")]
    public class MusicController(ApplicationDbContext db) : Controller
    {
        #region Constant Variables

        private const string Views = "~/Views/Admin/Music/";

        #endregion

        /// <summary>
        /// Handles the index page for music, retrieving a list of musics and moments.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> Index() => View(Views + "Index.cshtml", await db.MusicSections.AsNoTracking().OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).Include(s => s.Moments.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)).ToListAsync());

        #region Control Music Sections

        /// <summary>
        /// Handles the section pane
        /// </summary>
        [HttpGet("Seccao/{id:int?}")]
        public async Task<IActionResult> Section(int? id)
        {
            var section = id.HasValue ? await db.MusicSections.FindAsync(id.Value) : new MusicSection { DisplayOrder = await NextOrder(db.MusicSections.Select(s => s.DisplayOrder)) };

            return section == null ? NotFound() : View(Views + "Section.cshtml", section);
        }

        /// <summary>
        /// Handles the section creation page
        /// </summary>
        [HttpPost("Seccao/Guardar")]
        public async Task<IActionResult> SaveSection([Bind("Id,Name,DisplayOrder")] MusicSection input)
        {
            if (!ModelState.IsValid)
                return View(Views + "Section.cshtml", input);

            var section = input.Id == 0 ? db.MusicSections.Add(new()).Entity : await db.MusicSections.FindAsync(input.Id);

            if (section == null)
                return NotFound();

            section.Name = input.Name.Trim();
            section.DisplayOrder = input.DisplayOrder;

            await db.SaveChangesAsync();
            
            return BackToSection(section.Id);
        }

        /// <summary>
        /// Handles the section deletion page
        /// </summary>
        [HttpPost("Seccao/{id:int}/Eliminar")]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var section = await db.MusicSections.FindAsync(id);

            if (section == null)
                return NotFound();

            // Keep the empty-section check and deletion atomic if another admin adds a moment.
            if (await db.MusicSections.Where(s => s.Id == id && !s.Moments.Any()).ExecuteDeleteAsync() == 0)
            {
                TempData["Error"] = "Move ou elimina os momentos antes de eliminar esta secção.";

                return BackToSection(id);
            }
            
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Control Music Moments

        /// <summary>
        /// Handles the moments pane
        /// </summary>
        [HttpGet("Momento/{id:int?}")]
        public async Task<IActionResult> Moment(int? id, int sectionId)
        {
            if (id.HasValue)
            {
                var moment = await db.MusicMoments.FindAsync(id.Value);

                return moment == null ? NotFound() : await MomentForm(moment);
            }

            if (!await db.MusicSections.AnyAsync(s => s.Id == sectionId))
                return NotFound();

            return await MomentForm(new MusicMoment
            {
                SectionId = sectionId,
                DisplayOrder = await NextOrder(db.MusicMoments.Where(m => m.SectionId == sectionId).Select(m => m.DisplayOrder))
            });
        }

        /// <summary>
        /// Handles the moments creation page
        /// </summary>
        [HttpPost("Momento/Guardar")]
        public async Task<IActionResult> SaveMoment([Bind("Id,SectionId,Name,Song,DisplayOrder")] MusicMoment input)
        {
            if (!await db.MusicSections.AnyAsync(s => s.Id == input.SectionId))
                ModelState.AddModelError(nameof(input.SectionId), "Escolhe uma secção existente.");

            if (!ModelState.IsValid) 
                return await MomentForm(input);
            
            var moment = input.Id == 0 ? db.MusicMoments.Add(new()).Entity : await db.MusicMoments.FindAsync(input.Id);
            
            if (moment == null) 
                return NotFound();

            moment.SectionId = input.SectionId;
            moment.Name = input.Name.Trim();
            moment.Song = input.Song?.Trim();
            moment.DisplayOrder = input.DisplayOrder;
            
            await db.SaveChangesAsync();
            
            return BackToSection(moment.SectionId);
        }

        /// <summary>
        /// Handles the moments deletion page
        /// </summary>
        [HttpPost("Momento/{id:int}/Eliminar")]
        public async Task<IActionResult> DeleteMoment(int id)
        {
            var moment = await db.MusicMoments.FindAsync(id);

            if (moment == null) 
                return NotFound();

            db.MusicMoments.Remove(moment);
            
            await db.SaveChangesAsync();
            
            return BackToSection(moment.SectionId);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles the section pane according to the moements
        /// </summary>
        private async Task<ViewResult> MomentForm(MusicMoment moment)
        {
            ViewBag.Sections = new SelectList(await db.MusicSections.AsNoTracking().OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToListAsync(), nameof(MusicSection.Id), nameof(MusicSection.Name));

            return View(Views + "Moment.cshtml", moment);
        }

        /// <summary>
        /// Handles returning to section
        /// </summary>
        private IActionResult BackToSection(int id) => RedirectToAction(nameof(Index), null, null, $"seccao-{id}");

        /// <summary>
        /// Defines the next order on the moments and sections
        /// </summary>
        private static async Task<int> NextOrder(IQueryable<int> orders) => Math.Min((await orders.MaxAsync(n => (int?)n) ?? 0) + 10, 9999);

        #endregion
    }
}
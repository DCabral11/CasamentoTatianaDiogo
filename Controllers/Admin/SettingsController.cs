using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class SettingsController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Settings/Index.cshtml", await db.WeddingSettings.FirstAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(WeddingSettings m)
        {
            if (ViewModels.SiteTheme.NormalizeLogo(m.LogoPath) == null)
                ModelState.AddModelError(nameof(m.LogoPath), "Indica um caminho de imagem ou um endereço HTTP ou HTTPS.");
            
            if (!string.IsNullOrWhiteSpace(m.SpotifyTrackUrl) && Services.SpotifyTrack.Normalize(m.SpotifyTrackUrl) == null)
                ModelState.AddModelError(nameof(m.SpotifyTrackUrl), "Cola a ligação de uma faixa do Spotify (open.spotify.com/track/…).");
            
            if (!ModelState.IsValid)
                return View("~/Views/Admin/Settings/Index.cshtml", m);

            m.UpdatedAt = DateTime.UtcNow;
            m.SpotifyTrackUrl = Services.SpotifyTrack.Normalize(m.SpotifyTrackUrl);

            db.Update(m);
            await db.SaveChangesAsync();

            TempData["Success"] = "As definições foram guardadas.";

            return RedirectToAction(nameof(Index));
        }
    }
}

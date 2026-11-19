#region References

using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class SettingsController(ApplicationDbContext db, Services.WeddingClock clock) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Settings/Index.cshtml", await db.WeddingSettings.FirstAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(WeddingSettings m)
        {
            var start = clock.EventStartUtc(m.WeddingDateTime);
            var end = clock.EventStartUtc(m.WeddingEndDateTime);

            if (m.WeddingDateTime.HasValue && start == null)
                ModelState.AddModelError(nameof(m.WeddingDateTime), $"Esta hora de início não existe no fuso {clock.TimeZoneId} devido à mudança da hora.");

            if (m.WeddingEndDateTime.HasValue && (end == null || start == null || end <= start))
                ModelState.AddModelError(nameof(m.WeddingEndDateTime), $"Indica uma hora de fim válida no fuso {clock.TimeZoneId}, posterior ao início.");
            
            if (!Services.ContactFormats.SupportedRegion(m.PhoneRegion))
                ModelState.AddModelError(nameof(m.PhoneRegion), "Escolhe um país da lista.");
            
            if (!string.IsNullOrWhiteSpace(m.DefaultEmailDomain) && Services.ContactFormats.Domain(m.DefaultEmailDomain) == null)
                ModelState.AddModelError(nameof(m.DefaultEmailDomain), "Indica apenas o domínio, por exemplo exemplo.pt.");

            m.DefaultEmailDomain = ContactFormats.Domain(m.DefaultEmailDomain);
            m.BridePhone = ContactFormats.SavePhone(m.BridePhone, m, e => ModelState.AddModelError(nameof(m.BridePhone), e));
            m.GroomPhone = ContactFormats.SavePhone(m.GroomPhone, m, e => ModelState.AddModelError(nameof(m.GroomPhone), e));
            m.BrideEmail = ContactFormats.SaveEmail(m.BrideEmail, m, 200, e => ModelState.AddModelError(nameof(m.BrideEmail), e));
            m.GroomEmail = ContactFormats.SaveEmail(m.GroomEmail, m, 200, e => ModelState.AddModelError(nameof(m.GroomEmail), e));

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
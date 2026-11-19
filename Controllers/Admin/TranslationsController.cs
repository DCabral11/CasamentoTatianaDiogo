using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Translations/[action]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TranslationsController(ApplicationDbContext db, SiteTranslationService translations) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string language = "en")
    {
        if (language is not ("en" or "fr")) return BadRequest();
        return View("~/Views/Admin/Content/Translations.cshtml", await ReadAsync(language));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestFormLimits(ValueCountLimit = 10000)]
    public async Task<IActionResult> Save(TranslationsViewModel model)
    {
        if (model.Language is not ("en" or "fr")) return BadRequest();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var current = await ReadAsync(model.Language);
            model.Sources = current.Sources;
            if (current.Version != model.Version)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                ModelState.AddModelError("", T("Os conteúdos foram alterados entretanto. Recarrega a página antes de guardar as traduções."));
                return View("~/Views/Admin/Content/Translations.cshtml", model);
            }
            var allowed = current.Sources.Select(x => SiteTranslationService.Key(model.Language, x.Key)).ToHashSet(StringComparer.Ordinal);
            if (!allowed.SetEquals(model.Values.Keys) || model.Values.Values.Any(x => x?.Length > 30000)) return BadRequest();
            var prefix = SiteTranslationService.Prefix + model.Language + ".";
            var rows = await db.SiteContents.Where(x => x.Key.StartsWith(prefix)).ToDictionaryAsync(x => x.Key);
            foreach (var source in current.Sources)
            {
                var key = SiteTranslationService.Key(model.Language, source.Key);
                var value = model.Values[key]?.Trim();
                var row = rows.GetValueOrDefault(key);
                if (string.IsNullOrEmpty(value)) { if (row != null) db.SiteContents.Remove(row); }
                else if (row != null) row.Value = value;
                else db.SiteContents.Add(new SiteContent { Key = key, Description = source.Key, Value = value });
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = T("Traduções guardadas.");
            return RedirectToAction(nameof(Index), new { language = model.Language });
        });
    }

    private async Task<TranslationsViewModel> ReadAsync(string language)
    {
        var sources = await translations.SourcesAsync();
        var prefix = SiteTranslationService.Prefix + language + ".";
        var rows = await db.SiteContents.AsNoTracking().Where(x => x.Key.StartsWith(prefix)).ToDictionaryAsync(x => x.Key, x => x.Value);
        var values = sources.ToDictionary(x => SiteTranslationService.Key(language, x.Key), x => (string?)rows.GetValueOrDefault(SiteTranslationService.Key(language, x.Key)));
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sources, values }))));
        return new() { Language = language, Sources = sources, Values = values, Version = version };
    }
}

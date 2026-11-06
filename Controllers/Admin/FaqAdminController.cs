using static CasamentoTatianaDiogo.Services.AdminFilterOptions;
using CasamentoTatianaDiogo.ViewModels.Admin;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Faq/[action]/{id?}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FaqAdminController(ApplicationDbContext db, FaqService faqs) : Controller
{
    private const string Folder = "~/Views/Admin/Faq/";
    [HttpGet] public async Task<IActionResult> Index()
    {
        var page = await faqs.GetPageAsync(preview: true);
        ViewData["PublishedCount"] = page.Items.Count(x => x.IsPublished);
        ViewData["DraftCount"] = page.Items.Count(x => !x.IsPublished);
        var rows = new AdminListFilters<FaqItem>(this, page.Items, "Pergunta ou resposta", x => $"{x.Question} {x.Answer}")
            .Select("published", "Publicada", YesNo, x => Flag(x.IsPublished)).Results();
        return View(Folder + "Index.cshtml", page with { Items = rows });
    }
    [HttpGet] public IActionResult Create() => View(Folder + "Edit.cshtml", new FaqItem());
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var item = await db.FaqItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return item == null ? NotFound() : View(Folder + "Edit.cshtml", item);
    }
    [HttpGet] public async Task<IActionResult> Preview() => View("~/Views/Faq/Index.cshtml", await faqs.GetPageAsync(preview: true));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FaqItem model)
    {
        if (!ModelState.IsValid) return View(Folder + "Edit.cshtml", model);
        var item = model.Id == 0 ? new FaqItem() : await db.FaqItems.FindAsync(model.Id);
        if (item == null) return NotFound();
        if (model.Id != 0 && item.Version != model.Version) return ItemConflict(model);
        item.Question = model.Question.Trim(); item.Answer = model.Answer.Trim();
        item.DisplayOrder = model.DisplayOrder; item.IsPublished = model.IsPublished; item.Version = Guid.NewGuid();
        if (model.Id == 0) db.FaqItems.Add(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return ItemConflict(model); }
        TempData["Success"] = item.IsPublished ? "Pergunta guardada e publicada quando o módulo estiver ativo." : "Pergunta guardada como rascunho.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings([Bind(Prefix = "Settings")] FaqPageSettings model)
    {
        if (!ModelState.IsValid) return await SettingsView(model);
        var saved = await db.FaqPageSettings.FindAsync(1);
        if (saved == null) return NotFound();
        if (saved.Version != model.Version) return await SettingsConflict(model);
        saved.IsEnabled = model.IsEnabled; saved.Title = model.Title.Trim(); saved.NavigationLabel = model.NavigationLabel.Trim();
        saved.Introduction = model.Introduction?.Trim(); saved.EmptyMessage = model.EmptyMessage.Trim(); saved.Version = Guid.NewGuid();
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return await SettingsConflict(model); }
        TempData["Success"] = "Definições das perguntas frequentes guardadas.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, Guid version)
    {
        var item = await db.FaqItems.FindAsync(id);
        if (item == null) return NotFound();
        if (item.Version != version)
        {
            TempData["Error"] = "A pergunta foi alterada entretanto. Revê a versão atual antes de eliminar.";
            return RedirectToAction(nameof(Index));
        }
        db.FaqItems.Remove(item);
        try { await db.SaveChangesAsync(); TempData["Success"] = "Pergunta eliminada."; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "A pergunta foi alterada entretanto e não foi eliminada."; }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ItemConflict(FaqItem model)
    {
        db.ChangeTracker.Clear();
        Response.StatusCode = StatusCodes.Status409Conflict;
        ModelState.AddModelError("", "Esta pergunta foi alterada por outra pessoa. Abre a versão atual antes de guardar; os teus dados continuam abaixo.");
        return View(Folder + "Edit.cshtml", model);
    }
    private async Task<IActionResult> SettingsConflict(FaqPageSettings model)
    {
        db.ChangeTracker.Clear(); Response.StatusCode = StatusCodes.Status409Conflict;
        ModelState.AddModelError("", "As definições foram alteradas entretanto. Recarrega a página para consultar a versão atual antes de guardar.");
        return await SettingsView(model);
    }
    private async Task<IActionResult> SettingsView(FaqPageSettings model) =>
        View(Folder + "Index.cshtml", new FaqPageViewModel(model, (await faqs.GetPageAsync(preview: true)).Items, true));
}

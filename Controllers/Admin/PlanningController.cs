using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Planeamento")]
public class PlanningController(ApplicationDbContext db) : Controller
{
    [HttpGet("Orcamento")]
    public async Task<IActionResult> Budget(int? categoryId, int? supplierId)
    {
        var query = db.BudgetLines.AsNoTracking().Include(x => x.Category).Include(x => x.Supplier).AsQueryable();
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        ViewBag.SupplierFilter = supplierId;
        return await Page("Budget", await query.OrderBy(x => x.Category!.Name).ThenBy(x => x.Id).ToListAsync(), categoryId);
    }

    [HttpGet("Orcamento/Linha/{id:int?}")]
    public async Task<IActionResult> Line(int? id, int? categoryId, int? supplierId)
    {
        if (id.HasValue)
        {
            var existing = await db.BudgetLines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            return existing == null ? NotFound() : await LineForm(existing);
        }
        var supplier = supplierId.HasValue ? await db.Suppliers.FindAsync(supplierId.Value) : null;
        if (supplierId.HasValue && supplier == null) return NotFound();
        return await LineForm(new BudgetLine { CategoryId = supplier?.CategoryId ?? categoryId ?? 0, SupplierId = supplier?.Id });
    }

    [HttpPost("Orcamento/Guardar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLine([Bind("Id,CategoryId,SupplierId,RealCost,Paid,Status,NextPayment,Notes")] BudgetLine line)
    {
        await ValidateCategory(line.CategoryId);
        if (!BudgetLine.States.Contains(line.Status)) ModelState.AddModelError("Status", "Escolhe um estado da lista.");
        if (line.SupplierId.HasValue)
        {
            var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.SupplierId);
            if (supplier == null || supplier.CategoryId != line.CategoryId)
                ModelState.AddModelError("SupplierId", "Escolhe um fornecedor da categoria selecionada.");
            else
                line.RealCost ??= supplier.Price;
        }
        ValidateAmount(nameof(line.RealCost), line.RealCost);
        ValidateAmount(nameof(line.Paid), line.Paid);
        if (line.Paid > (line.RealCost ?? 0))
            ModelState.AddModelError("Paid", "O valor pago não pode ultrapassar o custo real. Indica primeiro o custo acordado.");
        if (!ModelState.IsValid) return await LineForm(line);

        if (line.Id == 0) db.BudgetLines.Add(line);
        else
        {
            var existing = await db.BudgetLines.FindAsync(line.Id);
            if (existing == null) return NotFound();
            db.Entry(existing).CurrentValues.SetValues(line);
        }
        if (!await SaveChanges("A categoria ou o fornecedor mudou. Revê a seleção.")) return await LineForm(line);
        return RedirectToAction(nameof(Budget));
    }

    [HttpGet("Fornecedores")]
    public async Task<IActionResult> Suppliers(int? categoryId)
    {
        var query = db.Suppliers.AsNoTracking().Include(x => x.Category).Include(x => x.Lines).AsQueryable();
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        return await Page("Suppliers", await query.OrderBy(x => x.Name).ToListAsync(), categoryId);
    }

    [HttpGet("Fornecedores/Ficha/{id:int?}")]
    public async Task<IActionResult> SupplierForm(int? id, int? categoryId)
    {
        var supplier = id.HasValue ? await db.Suppliers.FindAsync(id.Value) : new Supplier { CategoryId = categoryId ?? 0 };
        return supplier == null ? NotFound() : await Page("Supplier", supplier);
    }

    [HttpPost("Fornecedores/Guardar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplier([Bind("Id,CategoryId,Name,Contact,Email,Website,Status,Notes,Price")] Supplier supplier)
    {
        await ValidateCategory(supplier.CategoryId);
        ValidateAmount(nameof(supplier.Price), supplier.Price);
        if (!Supplier.States.Contains(supplier.Status)) ModelState.AddModelError("Status", "Escolhe um estado da lista.");
        if (supplier.Website != null && (!Uri.TryCreate(supplier.Website, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http")))
            ModelState.AddModelError("Website", "Usa um endereço começado por https:// ou http://.");
        if (!ModelState.IsValid) return await Page("Supplier", supplier);
        supplier.Name = supplier.Name.Trim();
        if (supplier.Id == 0)
        {
            db.Suppliers.Add(supplier);
            if (!await SaveChanges("A categoria mudou. Revê a seleção.")) return await Page("Supplier", supplier);
        }
        else
        {
            // The composite foreign key also protects against a concurrent budget assignment.
            var updated = await db.Suppliers.Where(x => x.Id == supplier.Id &&
                    (x.CategoryId == supplier.CategoryId || !x.Lines.Any()))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CategoryId, supplier.CategoryId)
                    .SetProperty(x => x.Name, supplier.Name).SetProperty(x => x.Contact, supplier.Contact)
                    .SetProperty(x => x.Email, supplier.Email).SetProperty(x => x.Website, supplier.Website)
                    .SetProperty(x => x.Status, supplier.Status).SetProperty(x => x.Price, supplier.Price)
                    .SetProperty(x => x.Notes, supplier.Notes));
            if (updated == 0)
            {
                if (!await db.Suppliers.AnyAsync(x => x.Id == supplier.Id)) return NotFound();
                ModelState.AddModelError("CategoryId", "Desassocia primeiro as linhas do orçamento para mudar a categoria.");
                return await Page("Supplier", supplier);
            }
        }
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpGet("Categorias/{id:int?}")]
    public async Task<IActionResult> Categories(int? id)
    {
        var category = id.HasValue ? await db.PlanningCategories.FindAsync(id.Value) : new PlanningCategory();
        return category == null ? NotFound() : await Page("Categories", category);
    }

    [HttpPost("Categorias/Guardar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory([Bind("Id,Name")] PlanningCategory input)
    {
        input.Name = Regex.Replace(input.Name?.Trim() ?? "", @"\s+", " ");
        input.NormalizedName = input.Name.NormalizeForSearch();
        if (await db.PlanningCategories.AnyAsync(x => x.Id != input.Id && x.NormalizedName == input.NormalizedName))
            ModelState.AddModelError("Name", "Esta categoria já existe. Usa a categoria da lista.");
        if (!ModelState.IsValid) return await Page("Categories", input);
        if (input.Id == 0) db.PlanningCategories.Add(input);
        else
        {
            var category = await db.PlanningCategories.FindAsync(input.Id);
            if (category == null) return NotFound();
            category.Name = input.Name;
            category.NormalizedName = input.NormalizedName;
        }
        if (!await SaveChanges("Não foi possível guardar. Verifica se já existe uma categoria com este nome."))
            return await Page("Categories", input);
        return RedirectToAction(nameof(Categories), new { id = (int?)null });
    }

    [HttpPost("Orcamento/Eliminar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLine(int id)
    {
        await db.BudgetLines.Where(x => x.Id == id).ExecuteDeleteAsync();
        return RedirectToAction(nameof(Budget));
    }

    [HttpPost("Fornecedores/Eliminar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        if (await db.Suppliers.Where(x => x.Id == id && !x.Lines.Any()).ExecuteDeleteAsync() == 0)
            TempData["Error"] = "Só podes eliminar um fornecedor sem linhas de orçamento associadas.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpPost("Categorias/Eliminar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (await db.PlanningCategories.Where(x => x.Id == id && !db.Suppliers.Any(s => s.CategoryId == x.Id)
                && !db.BudgetLines.Any(b => b.CategoryId == x.Id)).ExecuteDeleteAsync() == 0)
            TempData["Error"] = "Só podes eliminar uma categoria sem fornecedores nem linhas de orçamento.";
        return RedirectToAction(nameof(Categories));
    }

    private async Task ValidateCategory(int id)
    {
        if (!await db.PlanningCategories.AnyAsync(x => x.Id == id))
            ModelState.AddModelError("CategoryId", "Escolhe uma categoria da lista.");
    }

    private void ValidateAmount(string field, decimal? value)
    {
        if (value.HasValue && (value < 0 || value > 999999999999.99m || decimal.Round(value.Value, 2) != value))
            ModelState.AddModelError(field, "Indica um valor entre 0 e 999 999 999 999,99 €, com até duas casas decimais.");
    }

    private async Task<bool> SaveChanges(string error)
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { ModelState.AddModelError("", error); return false; }
    }

    private async Task<IActionResult> LineForm(BudgetLine line)
    {
        ViewBag.Suppliers = await db.Suppliers.AsNoTracking().Include(x => x.Category).OrderBy(x => x.Name).ToListAsync();
        return await Page("Line", line);
    }

    private async Task<ViewResult> Page(string name, object model, int? categoryId = null)
    {
        ViewBag.Categories = await db.PlanningCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        ViewBag.CategoryFilter = categoryId;
        return View($"~/Views/Admin/Planning/{name}.cshtml", model);
    }
}

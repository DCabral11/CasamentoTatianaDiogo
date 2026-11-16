using System.Linq.Expressions;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

// Build the filtered query before adding any relationships needed for display or export.
internal sealed class AdminQueryPage<T> where T : class
{
    internal const int Size = 50;
    private readonly Controller controller;
    private readonly IQueryable<T> source;
    private IQueryable<T> filtered;
    private bool hasPredicate;
    private readonly AdminFilterPanel panel;
    public CancellationToken Cancellation => controller.HttpContext.RequestAborted;
    public string Value(string name) => controller.Request.Query[name].ToString().Trim();

    public AdminQueryPage(Controller controller, IQueryable<T> source, string searchLabel)
    {
        this.controller = controller;
        this.source = filtered = source;
        panel = new() { SearchLabel = searchLabel, Search = Value("q"), Action = controller.Request.PathBase + controller.Request.Path };
        controller.ViewData["AdminFilters"] = panel;
        if (panel.Search.Length > 200) panel.Errors.Add("A pesquisa pode ter até 200 caracteres.");
    }

    public void Select(string name, string label, IEnumerable<AdminFilterOption> choices, Func<string, Expression<Func<T, bool>>> predicate)
    {
        var options = choices.ToList();
        var value = Value(name);
        panel.Fields.Add(new(name, label, value, options));
        if (value.Length == 0) return;
        if (!options.Any(o => o.Value == value)) panel.Errors.Add($"Escolhe uma opção válida em «{label}».");
        else Where(predicate(value));
    }

    public DateOnly? Date(string name, string label)
    {
        var value = Value(name);
        panel.Fields.Add(new(name, label, value, [], "date"));
        if (value.Length == 0) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)) return date;
        panel.Errors.Add($"Indica uma data válida em «{label}».");
        return null;
    }

    public void InvalidDates() => panel.Errors.Add("A data inicial não pode ser posterior à data final.");
    public void Where(Expression<Func<T, bool>> predicate)
    {
        filtered = filtered.Where(predicate);
        hasPredicate = true;
    }

    public async Task<IQueryable<T>> BuildAsync<TSearch>(Func<IQueryable<T>, IOrderedQueryable<T>> order,
        Expression<Func<T, TSearch>> projection, Func<TSearch, int> id, Func<TSearch, string> text,
        bool export = false)
    {
        var ordered = order(filtered);
        if (!export) panel.Total = await source.CountAsync(Cancellation);
        if (panel.Errors.Count != 0) return ordered.Where(x => false);
        int requested = int.TryParse(Value("page"), out var page) && page > 0 ? page : 1;
        if (panel.Search.Length == 0)
        {
            if (export) return ordered;
            panel.Count = hasPredicate ? await filtered.CountAsync(Cancellation) : panel.Total;
            SetPage(requested);
            return ordered.Skip((panel.Page - 1) * Size).Take(Size);
        }

        // Exact accent-insensitive matching, also for display-only text (e.g. phone
        // extensions). Scan just search fields after SQL filters, not entire entity graphs.
        var terms = panel.Search.NormalizeForSearch().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var selected = new List<int>();
        var lastPage = new List<int>(Size);
        await foreach (var row in ordered.Select(projection).AsAsyncEnumerable().WithCancellation(Cancellation))
        {
            var haystack = text(row).NormalizeForSearch();
            if (!terms.All(haystack.Contains)) continue;
            var currentPage = panel.Count / Size + 1;
            if (panel.Count % Size == 0) lastPage.Clear();
            var key = id(row);
            lastPage.Add(key);
            if (export || currentPage == requested) selected.Add(key);
            panel.Count++;
        }
        SetPage(requested);
        var ids = (!export && requested > panel.Pages ? lastPage : selected).ToArray();
        return ordered.Where(x => EF.Parameter(ids).Contains(EF.Property<int>(x, "Id")));
    }

    private void SetPage(int requested)
    {
        panel.Pages = panel.Count == 0 ? 1 : (panel.Count - 1) / Size + 1;
        panel.Page = Math.Min(requested, panel.Pages);
    }
}

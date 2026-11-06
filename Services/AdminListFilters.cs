using System.Globalization;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

// These administrative lists already load their records for rendering. Apply a shared,
// accent-insensitive search consistently across SQL Server, PostgreSQL and SQLite.
public sealed class AdminListFilters<T>
{
    private readonly Controller controller;
    private IEnumerable<T> rows;
    public AdminFilterPanel Panel { get; }
    private string Value(string name) => controller.Request.Query[name].ToString().Trim();

    public AdminListFilters(Controller controller, IReadOnlyList<T> rows, string label, Func<T, string?> text)
    {
        this.controller = controller;
        this.rows = rows;
        Panel = new() { Total = rows.Count, Search = Value("q"), SearchLabel = label,
            Action = controller.Request.PathBase + controller.Request.Path };
        controller.ViewData["AdminFilters"] = Panel;
        if (Panel.Search.Length > 200) Panel.Errors.Add("A pesquisa pode ter até 200 caracteres.");
        var terms = Panel.Search.NormalizeForSearch().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 0)
            this.rows = this.rows.Where(x => { var haystack = text(x).NormalizeForSearch(); return terms.All(haystack.Contains); });
    }

    public AdminListFilters<T> Select(string name, string label, IEnumerable<AdminFilterOption> options, Func<T, string> key)
    {
        var choices = options.ToList();
        var value = Value(name);
        Panel.Fields.Add(new(name, label, value, choices));
        if (value.Length > 0)
        {
            if (!choices.Any(x => x.Value == value)) Panel.Errors.Add($"Escolhe uma opção válida em «{label}».");
            rows = rows.Where(x => key(x) == value);
        }
        return this;
    }

    public AdminListFilters<T> Dates(string label, Func<T, DateOnly?> date)
    {
        var from = Date("from", label + " desde");
        var until = Date("until", label + " até");
        if (from > until) Panel.Errors.Add("A data inicial não pode ser posterior à data final.");
        if (from.HasValue) rows = rows.Where(x => date(x) >= from);
        if (until.HasValue) rows = rows.Where(x => date(x) <= until);
        return this;
    }

    private DateOnly? Date(string name, string label)
    {
        var value = Value(name);
        Panel.Fields.Add(new(name, label, value, [], "date"));
        if (value.Length == 0) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) return result;
        Panel.Errors.Add($"Indica uma data válida em «{label}».");
        return null;
    }

    public List<T> Results()
    {
        var result = Panel.Errors.Count == 0 ? rows.ToList() : [];
        Panel.Count = result.Count;
        return result;
    }
}

public static class AdminFilterOptions
{
    public static List<AdminFilterOption> Options(params (string Value, string Label)[] values) => values.Select(x => new AdminFilterOption(x.Value, x.Label)).ToList();
    public static List<AdminFilterOption> States(IEnumerable<string> states) => states.Select(x => new AdminFilterOption(x, x)).ToList();
    public static string Flag(bool value) => value ? "yes" : "no";
    public static List<AdminFilterOption> YesNo => Options(("yes", "Sim"), ("no", "Não"));
    public static List<AdminFilterOption> RsvpStates => Options(("Pending", "Por responder"), ("Attending", "Vai estar presente"), ("NotAttending", "Não vai estar presente"));

    public static async Task<List<Guest>> GuestsAsync(Controller controller, ApplicationDbContext db, List<Guest> rows)
    {
        var families = await db.Families.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
        return new AdminListFilters<Guest>(controller, rows, "Nome, email, telefone ou família", g =>
            $"{g.DisplayName} {g.FirstName} {g.LastName} {g.Email} {g.Phone} {g.Family?.Name} {g.Family?.GroupCode}")
            .Select("familyId", "Família", families.Select(x => new AdminFilterOption(x.Id.ToString(), $"{x.Name} — {x.GroupCode ?? "#" + x.Id}")), g => g.FamilyId.ToString())
            .Select("status", "Estado da resposta", RsvpStates, g => g.CurrentStatus.ToString())
            .Select("kind", "Tipo de convidado", Options(("guest", "Convidado principal"), ("plus-one", "Plus-One")), g => g.IsPlusOne ? "plus-one" : "guest")
            .Select("child", "É criança", YesNo, g => Flag(g.IsChild))
            .Select("allowPlusOne", "Pode levar acompanhante", YesNo, g => Flag(g.AllowPlusOne)).Results();
    }
}

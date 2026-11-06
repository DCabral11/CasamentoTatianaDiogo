namespace CasamentoTatianaDiogo.ViewModels.Admin;

public record AdminFilterOption(string Value, string Label);
public record AdminFilterField(string Name, string Label, string Value, IReadOnlyList<AdminFilterOption> Options, string Type = "select");
public class AdminFilterPanel
{
    public string Search { get; set; } = "";
    public string SearchLabel { get; set; } = "Pesquisar";
    public string Action { get; set; } = "";
    public List<AdminFilterField> Fields { get; } = [];
    public List<string> Errors { get; } = [];
    public int Total { get; set; }
    public int Count { get; set; }
    public bool Active => Search.Length > 0 || Fields.Any(x => x.Value.Length > 0);
}

namespace CasamentoTatianaDiogo.ViewModels.Admin;

public sealed class TranslationsViewModel
{
    public string Language { get; set; } = "en";
    public string Version { get; set; } = "";
    public IReadOnlyList<Services.TranslationSource> Sources { get; set; } = [];
    public Dictionary<string, string?> Values { get; set; } = [];
}

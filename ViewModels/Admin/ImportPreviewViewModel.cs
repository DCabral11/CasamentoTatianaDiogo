namespace CasamentoTatianaDiogo.ViewModels.Admin;

public class ImportInputRow
{
    public int Number { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
}

public class ImportPlanRow
{
    public int Number { get; set; }
    public int? TargetId { get; set; }
    public int FamilyId { get; set; }
    public string Action { get; set; } = "Criar";
    public Dictionary<string, string> Before { get; set; } = [];
    public Dictionary<string, string> After { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public class ImportPreviewViewModel
{
    public Guid Id { get; set; }
    public Guid Revision { get; set; }
    public string ImportType { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public List<ImportInputRow> Rows { get; set; } = [];
    public List<ImportPlanRow> Plan { get; set; } = [];
    public string[] Columns { get; set; } = [];
    public bool CanConfirm => Plan.Count > 0 && Plan.All(x => x.Errors.Count == 0);
}

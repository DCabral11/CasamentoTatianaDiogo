using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models;

public class AdminImportDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(450)] public string OwnerId { get; set; } = "";
    [MaxLength(20)] public string ImportType { get; set; } = "";
    public string RowsJson { get; set; } = "";
    public string PlanJson { get; set; } = "";
    public Guid Revision { get; set; } = Guid.NewGuid();
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

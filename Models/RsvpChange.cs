using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CasamentoTatianaDiogo.Models;

public sealed record RsvpFieldChange(string Field, string? Before, string? After);

// Names are captured at submission so the record survives later edits and removals.
public class RsvpChange
{
    public long Id { get; set; }
    public int GuestId { get; set; }
    [MaxLength(220)] public string GuestName { get; set; } = "";
    [MaxLength(150)] public string FamilyName { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    [MaxLength(30)] public string Operation { get; set; } = "";
    [MaxLength(320)] public string Actor { get; set; } = "";
    public string ChangesJson { get; set; } = "";
    [NotMapped] public List<RsvpFieldChange> Fields => JsonSerializer.Deserialize<List<RsvpFieldChange>>(ChangesJson)!;
}

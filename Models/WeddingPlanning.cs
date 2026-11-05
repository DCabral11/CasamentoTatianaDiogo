using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Models;

public class PlanningCategory
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(150)] public string NormalizedName { get; set; } = "";
}

public class Supplier
{
    public static readonly string[] States = ["Por contactar", "Contactado", "A aguardar orçamento", "Proposta recebida", "Selecionado", "Descartado"];
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public PlanningCategory? Category { get; set; }
    [Required(ErrorMessage = "Indica o nome do fornecedor."), StringLength(150)] public string Name { get; set; } = "";
    [StringLength(150)] public string? Contact { get; set; }
    [EmailAddress(ErrorMessage = "Indica um email válido."), StringLength(254)] public string? Email { get; set; }
    [Url(ErrorMessage = "Indica o endereço completo do site."), StringLength(500)] public string? Website { get; set; }
    [StringLength(40)] public string Status { get; set; } = States[0];
    [StringLength(2000)] public string? Notes { get; set; }
    [Precision(14, 2)] public decimal? Price { get; set; }
    public List<BudgetLine> Lines { get; set; } = [];
}

public class BudgetLine
{
    public static readonly string[] States = ["Por definir", "Por marcar", "A aguardar orçamento", "Por fechar", "Fechado"];
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public PlanningCategory? Category { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    [Precision(14, 2)] public decimal? RealCost { get; set; }
    [Precision(14, 2)] public decimal Paid { get; set; }
    [StringLength(40)] public string Status { get; set; } = States[0];
    public DateOnly? NextPayment { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

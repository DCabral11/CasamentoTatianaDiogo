#region References

using CasamentoTatianaDiogo.Models;

#endregion

namespace CasamentoTatianaDiogo.ViewModels.Admin
{
    public sealed record SupplierListRow(Supplier Supplier, string CategoryName, int BudgetLineCount);
}
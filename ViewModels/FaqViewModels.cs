#region References

using CasamentoTatianaDiogo.Models;

#endregion

namespace CasamentoTatianaDiogo.ViewModels
{
    public record FaqPageViewModel(FaqPageSettings Settings, IReadOnlyList<FaqItem> Items, bool IsPreview = false);
}

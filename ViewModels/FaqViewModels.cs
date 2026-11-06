using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.ViewModels;

public record FaqPageViewModel(FaqPageSettings Settings, IReadOnlyList<FaqItem> Items, bool IsPreview = false);

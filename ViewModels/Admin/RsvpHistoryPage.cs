using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.ViewModels.Admin;

public record RsvpHistoryPage(int? GuestId, string? GuestName, bool HasReceipt, int Page, int Pages, List<RsvpChange> Entries);

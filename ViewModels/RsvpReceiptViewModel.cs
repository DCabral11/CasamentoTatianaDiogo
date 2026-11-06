using CasamentoTatianaDiogo.Models.Enums;

namespace CasamentoTatianaDiogo.ViewModels;

public record RsvpReceiptPerson(string Name, RsvpStatus Status, string? DietaryRestrictions,
    string? Message, string? MusicRequest, RsvpReceiptPerson? Companion = null);

public record RsvpReceiptViewModel(string Reference, int GuestId, DateTime SavedAtUtc,
    string VerifiedContact, List<RsvpReceiptPerson> People)
{
    public int Attending => People.Count(x => x.Status == RsvpStatus.Attending) + People.Count(x => x.Companion != null);
    public int NotAttending => People.Count(x => x.Status == RsvpStatus.NotAttending);
}

namespace CasamentoTatianaDiogo.Models;

// One form/tab, bound to an authenticated verification challenge.
public class RsvpEditSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChallengeId { get; set; }
    public int GuestId { get; set; }
    public int FamilyId { get; set; }
    public Guid ExpectedVersion { get; set; }
    public DateTime LastSeenAt { get; set; }
}

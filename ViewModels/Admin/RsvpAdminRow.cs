using CasamentoTatianaDiogo.Models.Enums;

namespace CasamentoTatianaDiogo.ViewModels.Admin;

public record RsvpAdminRow(int Id, string DisplayName, string FamilyName, Guid FamilyVersion,
    RsvpStatus CurrentStatus, bool IsPlusOne, int? MainGuestId, bool HasResponse);

#region References

using CasamentoTatianaDiogo.Models.Enums;

#endregion

namespace CasamentoTatianaDiogo.ViewModels.Admin
{
    public record RsvpAdminRow(int Id, string DisplayName, string FamilyName, Guid FamilyVersion, RsvpStatus CurrentStatus, bool isPlusOne, int? MainGuestId, bool HasResponse);
}
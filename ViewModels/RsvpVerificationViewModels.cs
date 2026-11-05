using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.ViewModels;

public class RsvpContactViewModel
{
    public int GuestId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Indica o email para receber o código.")]
    [EmailAddress(ErrorMessage = "Indica um endereço de email válido."), StringLength(254)]
    public string Contact { get; set; } = string.Empty;
    public bool EmailAvailable { get; set; }
    public bool HasExistingContact { get; set; }
    public bool RequiresSupport { get; set; }
}

public class RsvpCodeViewModel
{
    [Required(ErrorMessage = "Introduz o código de verificação.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "O código deve ter 6 algarismos.")]
    public string Code { get; set; } = string.Empty;
    public int GuestId { get; set; }
    public string MaskedContact { get; set; } = string.Empty;
}

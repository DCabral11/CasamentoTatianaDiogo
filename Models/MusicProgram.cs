using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models;

public class MusicSection
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Indica o nome da secção."), StringLength(100), Display(Name = "Nome da secção")]
    public string Name { get; set; } = "";
    [Range(1, 9999, ErrorMessage = "Indica uma ordem entre 1 e 9999."), Display(Name = "Ordem")]
    public int DisplayOrder { get; set; } = 1;
    public List<MusicMoment> Moments { get; set; } = [];
}

public class MusicMoment
{
    public int Id { get; set; }
    [Display(Name = "Secção")]
    public int SectionId { get; set; }
    [Required(ErrorMessage = "Indica o momento."), StringLength(150), Display(Name = "Momento")]
    public string Name { get; set; } = "";
    [StringLength(250), Display(Name = "Música")]
    public string? Song { get; set; }
    [Range(1, 9999, ErrorMessage = "Indica uma ordem entre 1 e 9999."), Display(Name = "Ordem")]
    public int DisplayOrder { get; set; } = 1;
}

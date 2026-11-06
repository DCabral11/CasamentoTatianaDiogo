using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models;

public class FaqItem
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Escreve a pergunta."), StringLength(300, ErrorMessage = "A pergunta pode ter até 300 caracteres.")]
    public string Question { get; set; } = "";
    [Required(ErrorMessage = "Escreve a resposta."), StringLength(6000, ErrorMessage = "A resposta pode ter até 6000 caracteres.")]
    public string Answer { get; set; } = "";
    [Range(0, 10000, ErrorMessage = "A ordem deve estar entre 0 e 10000.")]
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; }
}

public class FaqPageSettings
{
    public int Id { get; set; } = 1;
    public bool IsEnabled { get; set; }
    [Required(ErrorMessage = "Indica o título da página."), StringLength(150)]
    public string Title { get; set; } = "Perguntas frequentes";
    [Required(ErrorMessage = "Indica o nome no menu."), StringLength(40)]
    public string NavigationLabel { get; set; } = "Perguntas frequentes";
    [StringLength(2000)] public string? Introduction { get; set; } = "Encontra aqui as respostas às dúvidas sobre o nosso dia.";
    [Required(ErrorMessage = "Indica a mensagem para quando não houver perguntas."), StringLength(500)]
    public string EmptyMessage { get; set; } = "Estamos a preparar as respostas às perguntas mais frequentes. Volta em breve!";
    [ConcurrencyCheck] public Guid Version { get; set; }
}

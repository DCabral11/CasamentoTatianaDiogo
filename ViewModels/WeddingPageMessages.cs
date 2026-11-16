namespace CasamentoTatianaDiogo.ViewModels;

public record WeddingMessageField(string Phase, string Label, string Key, string DefaultText);

public record WeddingPageMessages(string Controller, string Title, string During, string After)
{
    public IReadOnlyList<WeddingMessageField> Fields { get; } = [
        new("Before", "Antes", $"Wedding.{Controller}.Before", ""),
        new("During", "Durante", $"Wedding.{Controller}.During", During),
        new("After", "Depois", $"Wedding.{Controller}.After", After)
    ];

    public static readonly WeddingPageMessages[] Pages = [
        new("Home", "Página inicial", "Chegou o nosso dia! Obrigado por celebrarem connosco.", "Obrigado por fazerem parte do nosso casamento."),
        new("Information", "Informações", "Hoje celebramos juntos. Encontrem aqui o programa e as moradas da cerimónia e da festa.", "Guardamos com carinho os lugares onde celebrámos convosco."),
        new("Rsvp", "Confirmar presença", "Hoje é dia de celebrar! Se precisarem de falar connosco, os nossos contactos estão na página de informações.", "Obrigado por nos terem ajudado a preparar a lista de convidados."),
        new("Photos", "Enviar fotografias", "Queremos recordar o dia também pelo vosso olhar.", "As vossas fotografias ajudam-nos a reviver a festa. Obrigado pelas memórias!"),
        new("Faq", "Perguntas frequentes", "Reunimos aqui as respostas para as dúvidas que possam surgir hoje.", "Se ficou alguma dúvida depois da festa, consultem as respostas abaixo.")
    ];

    public static WeddingPageMessages? Find(string? controller) =>
        Pages.FirstOrDefault(p => string.Equals(p.Controller, controller, StringComparison.OrdinalIgnoreCase));
}

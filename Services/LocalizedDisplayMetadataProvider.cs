using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace CasamentoTatianaDiogo.Services;

public sealed class LocalizedDisplayMetadataProvider : IDisplayMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["Name"] = "Nome", ["FirstName"] = "Primeiro nome", ["LastName"] = "Apelido",
        ["DisplayName"] = "Nome apresentado", ["Phone"] = "Telefone", ["Email"] = "Email",
        ["Contact"] = "Contacto", ["Notes"] = "Notas", ["Title"] = "Título", ["Description"] = "Descrição",
        ["Question"] = "Pergunta", ["Answer"] = "Resposta", ["Message"] = "Mensagem",
        ["Price"] = "Preço", ["Paid"] = "Valor pago", ["RealCost"] = "Custo real",
        ["Quantity"] = "Quantidade", ["Capacity"] = "Capacidade", ["Status"] = "Estado",
        ["DietaryRestrictions"] = "Restrições alimentares", ["MusicRequest"] = "Sugestão musical",
        ["DisplayOrder"] = "Ordem de apresentação", ["File"] = "Ficheiro", ["Files"] = "Ficheiros"
    };

    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        if (context.Attributes.OfType<DisplayAttribute>().Any()) return;
        if (context.Key.Name is { } name && Labels.TryGetValue(name, out var label))
            context.DisplayMetadata.DisplayName = () => T(label);
    }
}

namespace CasamentoTatianaDiogo.ViewModels;

public sealed record WeddingTravelTopic(string Key, string Title, string DefaultText, string Icon);

public static class WeddingTravel
{
    public static readonly WeddingTravelTopic Parking = new("InformationParking", "Estacionamento",
        "Ainda não há indicações específicas sobre estacionamento.", "bi-p-square");
    public static readonly WeddingTravelTopic Transport = new("InformationTransport", "Transportes",
        "Os noivos ainda não publicaram indicações sobre transportes.", "bi-bus-front");
    public static readonly WeddingTravelTopic Lodging = new("InformationLodging", "Alojamento",
        "Ainda não há alojamentos recomendados pelos noivos.", "bi-house");

    public static IReadOnlyList<WeddingTravelTopic> Topics { get; } = [Parking, Transport, Lodging];

    public static string Address(string? name, string? address) =>
        string.Join(", ", new[] { name, address }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

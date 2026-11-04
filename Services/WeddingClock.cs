namespace CasamentoTatianaDiogo.Services;

public class WeddingClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(config["Wedding:TimeZone"] ?? "Europe/Lisbon");
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, zone);
}

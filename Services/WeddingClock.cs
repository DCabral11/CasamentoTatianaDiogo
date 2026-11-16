namespace CasamentoTatianaDiogo.Services;

public class WeddingClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(config["Wedding:TimeZone"] ?? "Europe/Lisbon");
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, zone);
    public string TimeZoneId => zone.Id;
    public DateTime? EventStartUtc(DateTime? local)
    {
        if (!local.HasValue) return null;
        var start = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
        return zone.IsInvalidTime(start) ? null : TimeZoneInfo.ConvertTimeToUtc(start, zone);
    }
    public DateTime StartOfDayUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
}

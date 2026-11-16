namespace CasamentoTatianaDiogo.Services;

public class WeddingClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(config["Wedding:TimeZone"] ?? "Europe/Lisbon");
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, zone);
    public string TimeZoneId => zone.Id;
    public WeddingPeriod GetPeriod(Models.WeddingSettings settings)
    {
        var start = EventStartUtc(settings.WeddingDateTime);
        DateTime? end = null;
        if (start.HasValue)
        {
            var localStart = settings.WeddingDateTime!.Value;
            var nextMidnight = localStart.Date == DateTime.MaxValue.Date ? DateTime.MaxValue : localStart.Date.AddDays(1);
            end = EventStartUtc(settings.WeddingEndDateTime ?? nextMidnight);
        }
        return new(time.GetUtcNow(), start, end);
    }
    public DateTime? EventStartUtc(DateTime? local)
    {
        if (!local.HasValue) return null;
        var start = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
        return zone.IsInvalidTime(start) ? null : TimeZoneInfo.ConvertTimeToUtc(start, zone);
    }
    public DateTime StartOfDayUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
}

public record WeddingPeriod(DateTimeOffset Now, DateTime? StartUtc, DateTime? EndUtc)
{
    public string Phase => StartUtc == null || Now.UtcDateTime < StartUtc ? "Before"
        : Now.UtcDateTime < EndUtc ? "During" : "After";
}

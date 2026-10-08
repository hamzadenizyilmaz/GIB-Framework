namespace GIBFramework.Base;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset TurkeyNow => TurkeyTime.ToTurkey(UtcNow);

    DateOnly TurkeyToday => DateOnly.FromDateTime(TurkeyNow.DateTime);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

public static class TurkeyTime
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    public static DateTimeOffset ToTurkey(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in (string[])["Europe/Istanbul", "Turkey Standard Time"])
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "Türkiye Saati", "TRT");
    }
}

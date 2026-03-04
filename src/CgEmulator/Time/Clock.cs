namespace CgEmulator.Time;

public static class Clock
{
    public static readonly TimeZoneInfo MskTimeZone = ResolveMskTimeZone();

    public static DateTimeOffset ToMsk(DateTimeOffset utcNow)
    {
        return TimeZoneInfo.ConvertTime(utcNow, MskTimeZone);
    }

    private static TimeZoneInfo ResolveMskTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");
            }
        }
    }
}

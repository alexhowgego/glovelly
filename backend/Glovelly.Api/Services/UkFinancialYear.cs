namespace Glovelly.Api.Services;

public static class UkFinancialYear
{
    private static readonly TimeZoneInfo LondonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static FinancialYearPeriod Current(TimeProvider timeProvider)
    {
        var londonNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), LondonTimeZone);
        return ForDate(DateOnly.FromDateTime(londonNow.DateTime));
    }

    public static DateOnly CurrentDate(TimeProvider timeProvider)
    {
        var londonNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), LondonTimeZone);
        return DateOnly.FromDateTime(londonNow.DateTime);
    }

    public static FinancialYearPeriod ForDate(DateOnly date)
    {
        var startYear = date.Month < 4 || (date.Month == 4 && date.Day < 6)
            ? date.Year - 1
            : date.Year;

        return new FinancialYearPeriod(
            new DateOnly(startYear, 4, 6),
            new DateOnly(startYear + 1, 4, 5));
    }
}

public sealed record FinancialYearPeriod(DateOnly Start, DateOnly End);

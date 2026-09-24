namespace InsuranceIntelligence.Domain.Scheduling;

/// <summary>
/// The default Ugandan public holiday calendar used as the working-day baseline for IRA service
/// standards (blueprint outcome B). This is an honest default, not a claim that every year's
/// calendar has been reconciled against the government gazette: fixed-date national holidays plus
/// Good Friday and Easter Monday (computed) are included; Eid al-Fitr and Eid al-Adha, which Uganda
/// also gazettes as public holidays, are lunar-calendar dependent and are not computed here, so a
/// tenant that needs them exact for a given year adds them as a configured holiday, not a bug in
/// this calculator. A per-tenant holiday override list is a later configuration surface; this class
/// is the default an unconfigured tenant runs on.
/// </summary>
public static class UgandaPublicHolidays
{
    public static IReadOnlySet<DateOnly> ForYear(int year)
    {
        var holidays = new HashSet<DateOnly>
        {
            new(year, 1, 1),   // New Year's Day
            new(year, 1, 26),  // NRM Anniversary / Liberation Day
            new(year, 2, 16),  // Archbishop Janani Luwum Day
            new(year, 3, 8),   // International Women's Day
            new(year, 5, 1),   // Labour Day
            new(year, 6, 3),   // Martyrs' Day
            new(year, 6, 9),   // Heroes' Day
            new(year, 10, 9),  // Independence Day
            new(year, 12, 25), // Christmas Day
            new(year, 12, 26), // Boxing Day
        };

        var easterSunday = ComputeEasterSunday(year);
        holidays.Add(easterSunday.AddDays(-2)); // Good Friday
        holidays.Add(easterSunday.AddDays(1));  // Easter Monday

        return holidays;
    }

    public static IReadOnlySet<DateOnly> ForYears(int fromYear, int toYear)
    {
        var holidays = new HashSet<DateOnly>();
        for (var year = fromYear; year <= toYear; year++)
        {
            foreach (var holiday in ForYear(year))
            {
                holidays.Add(holiday);
            }
        }

        return holidays;
    }

    /// <summary>Anonymous Gregorian algorithm (Meeus/Jones/Butcher) for the date of Easter Sunday.</summary>
    private static DateOnly ComputeEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}

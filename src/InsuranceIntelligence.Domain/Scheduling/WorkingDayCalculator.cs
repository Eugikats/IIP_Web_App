namespace InsuranceIntelligence.Domain.Scheduling;

/// <summary>
/// Counts and projects working days, excluding weekends and the supplied holiday set (blueprint
/// outcome B, IIP_NEXT_BUILD_PROMPT.md section 5). Every SLA figure in the product, the brief, the
/// nudges and the claim page must be counted this way, never in raw calendar days. The settlement
/// clock resolves to the correct value tier first, see ClaimStageDefinition.ResolveServiceStandardWorkingDays,
/// then this calculator projects the due date from that day count.
/// </summary>
public static class WorkingDayCalculator
{
    /// <summary>The working day <paramref name="workingDays"/> days after <paramref name="start"/>.
    /// Start itself never counts, matching "within N working days of X" (the clock starts the day
    /// after the triggering event).</summary>
    public static DateOnly AddWorkingDays(DateOnly start, int workingDays, IReadOnlySet<DateOnly> holidays)
    {
        if (workingDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workingDays), "Working days cannot be negative.");
        }

        var date = start;
        var remaining = workingDays;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (IsWorkingDay(date, holidays))
            {
                remaining--;
            }
        }

        return date;
    }

    /// <summary>Full working days elapsed strictly between two dates (exclusive of start, inclusive
    /// of end), used to show "X working days elapsed" against a stage's standard.</summary>
    public static int CountWorkingDaysBetween(DateOnly start, DateOnly end, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start)
        {
            return -CountWorkingDaysBetween(end, start, holidays);
        }

        var count = 0;
        var date = start;
        while (date < end)
        {
            date = date.AddDays(1);
            if (IsWorkingDay(date, holidays))
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> holidays) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date);
}

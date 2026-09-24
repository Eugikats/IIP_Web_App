using InsuranceIntelligence.Domain.Scheduling;

namespace InsuranceIntelligence.Domain.Tests.Scheduling;

public sealed class WorkingDayCalculatorTests
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void AddWorkingDays_SkipsWeekends()
    {
        // Friday 2026-09-25 + 1 working day should land on Monday 2026-09-28, not Saturday.
        var friday = new DateOnly(2026, 9, 25);

        var result = WorkingDayCalculator.AddWorkingDays(friday, 1, NoHolidays);

        Assert.Equal(new DateOnly(2026, 9, 28), result);
    }

    [Fact]
    public void AddWorkingDays_SkipsHolidays()
    {
        var monday = new DateOnly(2026, 12, 21);
        var holidays = new HashSet<DateOnly> { new(2026, 12, 24), new(2026, 12, 25) };

        // 3 working days from Mon 21 Dec: Tue 22, Wed 23, then skip holidays 24/25, weekend 26/27, land Mon 28.
        var result = WorkingDayCalculator.AddWorkingDays(monday, 3, holidays);

        Assert.Equal(new DateOnly(2026, 12, 28), result);
    }

    [Fact]
    public void AddWorkingDays_ZeroDays_ReturnsStartUnchanged()
    {
        var start = new DateOnly(2026, 9, 22);

        var result = WorkingDayCalculator.AddWorkingDays(start, 0, NoHolidays);

        Assert.Equal(start, result);
    }

    [Fact]
    public void AddWorkingDays_RejectsNegativeDays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkingDayCalculator.AddWorkingDays(new DateOnly(2026, 9, 22), -1, NoHolidays));
    }

    [Fact]
    public void CountWorkingDaysBetween_ExcludesWeekendsAndHolidays()
    {
        var start = new DateOnly(2026, 9, 21); // Monday
        var end = new DateOnly(2026, 9, 28);   // Following Monday
        var holidays = new HashSet<DateOnly> { new(2026, 9, 24) };

        var count = WorkingDayCalculator.CountWorkingDaysBetween(start, end, holidays);

        // Tue 22, Wed 23, (Thu 24 holiday), Fri 25, (weekend), Mon 28 = 4 working days.
        Assert.Equal(4, count);
    }

    [Fact]
    public void CountWorkingDaysBetween_HandlesReversedDates()
    {
        var start = new DateOnly(2026, 9, 28);
        var end = new DateOnly(2026, 9, 21);

        var count = WorkingDayCalculator.CountWorkingDaysBetween(start, end, NoHolidays);

        Assert.True(count < 0);
    }

    [Theory]
    [InlineData(2026, 9, 19, false)] // Saturday
    [InlineData(2026, 9, 20, false)] // Sunday
    [InlineData(2026, 9, 21, true)]  // Monday
    public void IsWorkingDay_TreatsWeekendsAsNonWorking(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, WorkingDayCalculator.IsWorkingDay(new DateOnly(year, month, day), NoHolidays));
    }
}

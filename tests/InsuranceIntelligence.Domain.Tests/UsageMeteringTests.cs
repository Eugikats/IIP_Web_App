using InsuranceIntelligence.Domain.Billing;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class UsageMeteringTests
{
    [Theory]
    [InlineData("2026-10-01T00:00:00+00:00", "2026-10")]
    [InlineData("2026-10-31T23:59:59+00:00", "2026-10")]
    [InlineData("2026-11-01T01:00:00+03:00", "2026-10")]
    [InlineData("2027-01-01T00:00:00+00:00", "2027-01")]
    public void AMonthIsKeyedByItsUtcCalendarMonth(string moment, string expected) =>
        Assert.Equal(expected, UsagePeriod.KeyFor(DateTimeOffset.Parse(moment, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-1", false)]
    [InlineData("2026-13", false)]
    [InlineData("2026-00", false)]
    [InlineData("26-10-1", false)]
    [InlineData("abcd-ef", false)]
    [InlineData(null, false)]
    public void OnlyARealMonthKeyIsValid(string? key, bool valid) => Assert.Equal(valid, UsagePeriod.IsValid(key));

    [Fact]
    public void ACostIsKnownOnlyWhenNothingWasUsedOrSomethingCouldBePriced()
    {
        Assert.Equal(0m, AiUsageTotals.None.CostUsd);
        Assert.Null(new AiUsageTotals(3, 100, 10, 0, 3).CostUsd);
        Assert.Equal(1.5m, new AiUsageTotals(3, 100, 10, 1_500_000, 1).CostUsd);
        Assert.Equal(110, new AiUsageTotals(1, 100, 10, 0, 0).TotalTokens);
    }
}

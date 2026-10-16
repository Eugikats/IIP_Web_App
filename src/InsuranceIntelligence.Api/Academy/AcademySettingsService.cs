using System.Text.Json;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

public sealed record PaymentSettings(decimal UgxPerUsd, string CardCurrency, decimal TaxRatePercent);

public sealed record ZenithSettings(int DailyLimit, long MonthlyTokens);

/// <summary>
/// The Academy's settings that the Academy team changes on screen: how dollars become shillings for mobile money, which
/// currency cards are charged in, the tax rate, and Zenith's allowances. A saved value wins; otherwise the configuration
/// default applies, and otherwise the built-in one.
/// </summary>
public sealed class AcademySettingsService(IAcademyRecordStore store, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Owner = "-";

    public const decimal DefaultUgxPerUsd = 4118m;
    public const int DefaultZenithDailyLimit = 10;
    public const long DefaultZenithMonthlyTokens = 1_000_000;

    public async Task<PaymentSettings> PaymentsAsync(CancellationToken ct)
    {
        var saved = await ReadAsync<PaymentSettings>("payments", ct);
        return saved ?? new PaymentSettings(
            configuration.GetValue("Academy:Payments:UgxPerUsd", DefaultUgxPerUsd),
            configuration.GetValue("Academy:Payments:CardCurrency", "USD")!,
            configuration.GetValue("Academy:Payments:TaxRatePercent", 0m));
    }

    public async Task<PaymentSettings> SavePaymentsAsync(decimal? ugxPerUsd, string? cardCurrency, decimal? taxRatePercent, CancellationToken ct)
    {
        var current = await PaymentsAsync(ct);
        var rate = ugxPerUsd ?? current.UgxPerUsd;
        var currency = (cardCurrency ?? current.CardCurrency).Trim().ToUpperInvariant();
        var tax = taxRatePercent ?? current.TaxRatePercent;
        if (rate is < 500m or > 20_000m)
        {
            throw new AcademyRuleException(400, "Enter the number of shillings to the dollar, between 500 and 20,000.");
        }

        if (currency is not ("USD" or "UGX"))
        {
            throw new AcademyRuleException(400, "Cards can be charged in USD or UGX.");
        }

        if (tax is < 0m or > 30m)
        {
            throw new AcademyRuleException(400, "Enter a tax rate between 0 and 30 percent. Use 0 if no tax is charged.");
        }

        var next = new PaymentSettings(decimal.Round(rate, 2), currency, decimal.Round(tax, 2));
        await store.SaveAsync(AcademyKinds.Setting, Owner, "payments", JsonSerializer.Serialize(next, Json), ct);
        return next;
    }

    public async Task<ZenithSettings> ZenithAsync(CancellationToken ct)
    {
        var saved = await ReadAsync<ZenithSettings>("zenith", ct);
        return saved ?? new ZenithSettings(
            Math.Max(1, configuration.GetValue("Academy:ZenithDailyLimit", DefaultZenithDailyLimit)),
            Math.Max(1000, configuration.GetValue("Academy:ZenithMonthlyTokens", DefaultZenithMonthlyTokens)));
    }

    public async Task<ZenithSettings> SaveZenithAsync(int? dailyLimit, long? monthlyTokens, CancellationToken ct)
    {
        var current = await ZenithAsync(ct);
        var daily = dailyLimit ?? current.DailyLimit;
        var monthly = monthlyTokens ?? current.MonthlyTokens;
        if (daily is < 1 or > 500)
        {
            throw new AcademyRuleException(400, "Give each person between 1 and 500 written explanations a day.");
        }

        if (monthly is < 10_000 or > 100_000_000)
        {
            throw new AcademyRuleException(400, "Set the monthly allowance between 10,000 and 100,000,000 tokens.");
        }

        var next = new ZenithSettings(daily, monthly);
        await store.SaveAsync(AcademyKinds.Setting, Owner, "zenith", JsonSerializer.Serialize(next, Json), ct);
        return next;
    }

    private async Task<T?> ReadAsync<T>(string key, CancellationToken ct) where T : class
    {
        var record = await store.GetAsync(AcademyKinds.Setting, Owner, key, ct);
        return record is null ? null : JsonSerializer.Deserialize<T>(record.Json, Json);
    }
}

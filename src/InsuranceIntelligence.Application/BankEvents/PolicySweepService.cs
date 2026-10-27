using System.Globalization;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.BankEvents;

/// <summary>
/// Turns dates already in the policy records into events, so renewals and premiums are handled by the same rules as anything
/// the bank reports. Run before the rules look at events. Each event has a fixed identity (this policy, this trigger, this
/// date), so looking again tomorrow adds nothing for a policy already noticed. Only customers with a phone number can be
/// reached, so only they get an event. A missed premium is not noticed here because no payment records exist to say it was missed.
/// </summary>
public sealed class PolicySweepService(IPolicyRepository policies, ICustomerRepository customers, EventService events, TimeProvider clock)
{
    public sealed record Result(int Noticed, int New);

    public async Task<Result> SweepAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var phones = (await customers.ListAsync(scope, cancellationToken))
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Phone))
            .ToDictionary(customer => customer.CustomerId, customer => customer.Phone!, StringComparer.OrdinalIgnoreCase);
        var noticed = 0;
        var created = 0;

        foreach (var policy in await policies.ListAsync(scope, cancellationToken))
        {
            if (policy.CustomerId is null || !phones.TryGetValue(policy.CustomerId, out var phone) || policy.EffectiveTo is not { } end)
            {
                continue;
            }

            var product = MicroinsuranceCatalog.Offered.FirstOrDefault(item => item.ProductLine == policy.ProductLine && item.PolicyPrefix == PrefixOf(policy.PolicyNumber))
                ?? MicroinsuranceCatalog.Offered.FirstOrDefault(item => item.ProductLine == policy.ProductLine);
            var name = product?.Name ?? policy.ProductLine;
            var left = end.DayNumber - today.DayNumber;
            var endText = end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

            async Task Emit(string type, string reference, IReadOnlyDictionary<string, string> extra)
            {
                noticed++;
                try
                {
                    var made = await events.RecordAsync(
                        scope, new EventInput(type, phone, now.AddMinutes(-1), $"{policy.PolicyId}:{type}:{reference}", Extra: extra),
                        EventSource.PolicyDates, $"policy {policy.PolicyNumber}", "system:policy-sweep", cancellationToken);
                    if (made.Created) created++;
                }
                catch (EventException)
                {
                    // A phone number that cannot be used cannot be reached; nothing to record.
                }
            }

            var common = new Dictionary<string, string>
            {
                ["policy_number"] = policy.PolicyNumber, ["product"] = name, ["product_line"] = policy.ProductLine, ["end_date"] = endText,
            };

            if (left is > 7 and <= 30)
            {
                await Emit("policy_ending_30", end.ToString("yyyy-MM-dd"), common);
            }
            else if (left is >= 0 and <= 7)
            {
                await Emit("policy_ending_7", end.ToString("yyyy-MM-dd"), common);
            }
            else if (left is < 0 and >= -14)
            {
                await Emit("policy_lapsed", end.ToString("yyyy-MM-dd"), common);
            }

            // A monthly premium falls due on the same day of the month the policy began, for life covers, which are paid monthly.
            if (product?.InsuranceClass == InsuranceClass.Life && policy.EffectiveFrom is { } start && left >= 0)
            {
                var due = NextDue(start, today);
                if (due.DayNumber - today.DayNumber is >= 0 and <= 3 && due <= end)
                {
                    var monthly = product.MonthlyPremium;
                    await Emit("premium_due", due.ToString("yyyy-MM-dd"), new Dictionary<string, string>(common)
                    {
                        ["due_date"] = due.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
                        ["amount"] = monthly.ToString("0.##", CultureInfo.InvariantCulture),
                    });
                }
            }
        }

        return new Result(noticed, created);
    }

    private static string PrefixOf(string policyNumber) => policyNumber.Split('/', 2)[0];

    private static DateOnly NextDue(DateOnly start, DateOnly today)
    {
        var day = Math.Min(start.Day, DateTime.DaysInMonth(today.Year, today.Month));
        var due = new DateOnly(today.Year, today.Month, day);
        if (due < today)
        {
            var next = today.AddMonths(1);
            due = new DateOnly(next.Year, next.Month, Math.Min(start.Day, DateTime.DaysInMonth(next.Year, next.Month)));
        }

        return due;
    }
}

using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Billing;

/// <summary>What one AI model costs per million tokens, in US dollars, as Azure bills it.</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion);

/// <summary>
/// How tokens turn into credits. A credit is a fixed small amount of list value (<see cref="CreditValueUsd"/>);
/// an AI call is worth its real token cost times <see cref="Markup"/>, rounded up to whole credits so the
/// smallest call still counts. The prices are settings, so changing one is not a code change.
/// </summary>
public sealed record AiCreditPricing(
    IReadOnlyDictionary<string, ModelPrice> Models,
    ModelPrice DefaultPrice,
    decimal Markup,
    decimal CreditValueUsd)
{
    /// <summary>Azure OpenAI gpt-5.4, Global Standard, as published 2026-10-09; a 3x markup; 1 credit = US$0.01.</summary>
    public static AiCreditPricing Default { get; } = new(
        new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5.4"] = new(2.50m, 15.00m),
            ["gpt-4.1-mini"] = new(0.40m, 1.60m),
            ["gpt-4o"] = new(2.50m, 10.00m),
        },
        new ModelPrice(2.50m, 15.00m),
        3m,
        0.01m);

    public ModelPrice PriceFor(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return DefaultPrice;
        }

        if (Models.TryGetValue(model, out var exact))
        {
            return exact;
        }

        // A deployment name often carries the model name inside it ("iip-chat-gpt-5.4-2026-03-05").
        var partial = Models.FirstOrDefault(pair => model.Contains(pair.Key, StringComparison.OrdinalIgnoreCase));
        return partial.Value ?? DefaultPrice;
    }

    /// <summary>The real cost in dollars and the credits it is worth. A call with no token counts is worth one credit.</summary>
    public (decimal CostUsd, int Credits) Calculate(string? model, int? promptTokens, int? completionTokens)
    {
        var price = PriceFor(model);
        var cost = ((promptTokens ?? 0) * price.InputPerMillion + (completionTokens ?? 0) * price.OutputPerMillion) / 1_000_000m;
        var credits = (int)Math.Max(1, Math.Ceiling(cost * Markup / CreditValueUsd));
        return (Math.Round(cost, 8), credits);
    }
}

/// <summary>One AI call that was made and what it was worth. Holds no prompt or answer text.</summary>
public sealed record AiUsageEntry(
    Guid UsageId,
    TenantScope Scope,
    string? UserId,
    string Feature,
    string Provider,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens,
    decimal CostUsd,
    int Credits,
    DateTimeOffset OccurredAt);

/// <summary>Where credits stand for an organisation this month.</summary>
public sealed record CreditStatus(bool Metered, string PlanId, string PlanName, int Used, int Included, bool Exhausted, bool NearLimit)
{
    public int Remaining => Metered ? Math.Max(0, Included - Used) : int.MaxValue;
}

public interface IAiUsageRepository
{
    Task SaveAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiUsageEntry>> ListAsync(TenantScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Where the AI call happened, so spend can be shown by feature: set by the code that starts the work.</summary>
public interface IAiUsageContext
{
    string Feature { get; }

    TenantScope? Scope { get; }

    string? UserId { get; }

    IDisposable Use(string feature, TenantScope? scope = null, string? userId = null);
}

public sealed class AiUsageContext : IAiUsageContext
{
    private readonly Stack<(string Feature, TenantScope? Scope, string? UserId)> stack = new();

    public string Feature => stack.Count > 0 ? stack.Peek().Feature : "other";

    public TenantScope? Scope => stack.Count > 0 ? stack.Peek().Scope : null;

    public string? UserId => stack.Count > 0 ? stack.Peek().UserId : null;

    public IDisposable Use(string feature, TenantScope? scope = null, string? userId = null)
    {
        // An inner call keeps the outer call's scope and person unless it names its own.
        var outer = stack.Count > 0 ? stack.Peek() : default;
        stack.Push((feature, scope ?? outer.Scope, userId ?? outer.UserId));
        return new Pop(stack);
    }

    private sealed class Pop(Stack<(string, TenantScope?, string?)> target) : IDisposable
    {
        private bool done;

        public void Dispose()
        {
            if (!done && target.Count > 0)
            {
                target.Pop();
            }

            done = true;
        }
    }
}

public interface IAiCreditGate
{
    Task<CreditStatus> StatusAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IAiUsageRecorder
{
    /// <summary>Records one finished AI call and adds its credits to the month's total.</summary>
    Task RecordAsync(string provider, string? model, int? promptTokens, int? completionTokens, string? feature = null, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="AiCreditsExhaustedException"/> when the organisation has used its credits for the month.</summary>
    Task EnsureCreditsAsync(CancellationToken cancellationToken = default);
}

/// <summary>The organisation's AI credits for the month are used up. Callers fall back to work that needs no AI,
/// or tell the person plainly; the platform never fakes an answer.</summary>
public sealed class AiCreditsExhaustedException() : InvalidOperationException("AI credits for this month are used up.");

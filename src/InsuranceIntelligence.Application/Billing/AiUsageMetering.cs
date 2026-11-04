using System.Text.Json;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Billing;

/// <summary>What one call to a model used. No prompt and no answer: only counts and which model.</summary>
public sealed record WorkspaceAiUsageEntry(
    TenantScope Scope,
    string Operation,
    string Provider,
    string? Deployment,
    string? ModelVersion,
    int? PromptTokens,
    int? CompletionTokens,
    string? CorrelationId,
    Guid? CaseId = null);

/// <summary>Records one model call against the workspace that caused it. Never throws: a metering fault must not fail the work.</summary>
public interface IWorkspaceAiUsageRecorder
{
    Task RecordAsync(WorkspaceAiUsageEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>The price of a model in US dollars per 1,000 tokens.</summary>
public sealed record AiTokenPrice(decimal PromptPer1K, decimal CompletionPer1K);

/// <summary>
/// Prices come from the deployment's own configuration, keyed by the model deployment name (or a model version prefix),
/// so a different model is priced by its own entry and not by a number fixed in code. A model with no entry is not
/// priced: the cost shows as not available rather than as a guess.
/// </summary>
public sealed class AiPricingOptions
{
    public IReadOnlyDictionary<string, AiTokenPrice> Prices { get; init; } = new Dictionary<string, AiTokenPrice>(StringComparer.OrdinalIgnoreCase);

    public static AiPricingOptions None { get; } = new();

    /// <summary>Reads <c>INSURANCEINTELLIGENCE_AI_PRICING</c>: JSON such as {"gpt-4o":{"promptPer1K":0.0025,"completionPer1K":0.01}}. Anything malformed means no prices.</summary>
    public static AiPricingOptions Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return None;
            }

            var prices = new Dictionary<string, AiTokenPrice>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind == JsonValueKind.Object
                    && entry.Value.TryGetProperty("promptPer1K", out var prompt) && prompt.ValueKind == JsonValueKind.Number && prompt.TryGetDecimal(out var promptPrice) && promptPrice >= 0
                    && entry.Value.TryGetProperty("completionPer1K", out var completion) && completion.ValueKind == JsonValueKind.Number && completion.TryGetDecimal(out var completionPrice) && completionPrice >= 0)
                {
                    prices[entry.Name.Trim()] = new AiTokenPrice(promptPrice, completionPrice);
                }
            }

            return new AiPricingOptions { Prices = prices };
        }
        catch (JsonException)
        {
            return None;
        }
    }
}

public interface IAiCostModel
{
    /// <summary>Estimated cost in US dollars, or null when the tokens are unknown or the model has no configured price.</summary>
    decimal? Estimate(string? provider, string? deployment, string? modelVersion, int? promptTokens, int? completionTokens);
}

public sealed class ConfiguredAiCostModel(AiPricingOptions options) : IAiCostModel
{
    public decimal? Estimate(string? provider, string? deployment, string? modelVersion, int? promptTokens, int? completionTokens)
    {
        if (promptTokens is null && completionTokens is null)
        {
            return null;
        }

        // A mock model is free. It is only ever present in developer and demonstration runtimes.
        if (string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var price = Find(deployment) ?? FindByVersion(modelVersion);
        if (price is null)
        {
            return null;
        }

        return Math.Round(((promptTokens ?? 0) / 1000m * price.PromptPer1K) + ((completionTokens ?? 0) / 1000m * price.CompletionPer1K), 8);
    }

    private AiTokenPrice? Find(string? name) =>
        !string.IsNullOrWhiteSpace(name) && options.Prices.TryGetValue(name.Trim(), out var price) ? price : null;

    private AiTokenPrice? FindByVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        // The longest configured name the reported model version starts with ("gpt-4o-2024-08-06" matches "gpt-4o").
        return options.Prices
            .Where(entry => version.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Key.Length)
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }
}

using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Application.Ai;

// AgentKey: which AgentCatalog entry this completion is for, resolves the tenant's
// AgentBehaviourProfile (blueprint §6.3). Null means "no tenant customization applies", which is
// still safe: the platform preamble and the caller-supplied default system prompt still compose
// normally, just with no tenant-configured section layered on top.
public sealed record ChatCompletionRequest(
    string Prompt,
    string TenantId,
    string? WorkspaceId = null,
    string? CorrelationId = null,
    string? AgentKey = null);

public sealed record ChatCompletionResult(
    string Content,
    string Provider,
    string? Deployment,
    DateTimeOffset CompletedAt,
    string? ModelVersion = null,
    int? PromptTokens = null,
    int? CompletionTokens = null);

public interface IChatCompletionProvider
{
    Task<ChatCompletionResult> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>A configured AI capability is required for this operation, but none is available.</summary>
public class CapabilityNotConfiguredException(string message) : InvalidOperationException(message) { }

public sealed record ValidatedAiOutput(
    string Preview,
    bool RequiresHumanReview,
    IReadOnlyList<string> GuardrailsApplied);

public interface IAiOutputValidator
{
    Task<ValidatedAiOutput> ValidateCustomerSafeDraftAsync(
        string content,
        TenantContext context,
        CancellationToken cancellationToken = default);

    Task<ValidatedAiOutput> ValidateManagementSummaryAsync(
        string content,
        TenantContext context,
        CancellationToken cancellationToken = default);
}

public sealed class DeterministicAiOutputValidator : IAiOutputValidator
{
    private static readonly string[] ConsequentialDecisionPhrases =
    [
        "claim is denied",
        "claim denied",
        "settlement approved",
        "approve settlement",
        "payment approved",
        "authorize payment",
        "fraudulent claim",
        "fraud accusation",
        "bind policy",
        "policy is cancelled"
    ];

    private static readonly string[] CustomerUnsafeFragments =
    [
        "fraud flag",
        "reserve",
        "internal note",
        "system prompt",
        "hidden prompt",
        "confidence score",
        "risk score"
    ];

    public Task<ValidatedAiOutput> ValidateCustomerSafeDraftAsync(
        string content,
        TenantContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(content, includeCustomerSafety: true));

    public Task<ValidatedAiOutput> ValidateManagementSummaryAsync(
        string content,
        TenantContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(content, includeCustomerSafety: false));

    private static ValidatedAiOutput Validate(string content, bool includeCustomerSafety)
    {
        var guardrails = new List<string>();
        var preview = Normalize(content);

        foreach (var phrase in ConsequentialDecisionPhrases)
        {
            if (preview.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                preview = preview.Replace(phrase, "requires human review", StringComparison.OrdinalIgnoreCase);
                guardrails.Add("consequential_decision_language_removed");
            }
        }

        if (includeCustomerSafety)
        {
            foreach (var fragment in CustomerUnsafeFragments)
            {
                if (preview.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    preview = preview.Replace(fragment, "internal detail removed", StringComparison.OrdinalIgnoreCase);
                    guardrails.Add("customer_unsafe_internal_detail_removed");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(preview))
        {
            preview = "AI output was empty after validation. Human review is required.";
            guardrails.Add("empty_output_replaced");
        }

        return new ValidatedAiOutput(
            preview,
            RequiresHumanReview: true,
            guardrails.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string Normalize(string value) =>
        string.Join(' ', (value ?? string.Empty)
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
}

namespace InsuranceIntelligence.Application.CommandCenter;

public enum IntentType
{
    ShowSlaBreaches,
    ShowAttentionClaims,
    ShowMissingDocuments,
    ShowPendingApprovals,
    GenerateManagementSummary,
    PrepareCustomerUpdateDraft,
    Unsupported
}

public sealed record IntentClassificationResult(
    IntentType Intent,
    IntentParameters Parameters,
    decimal Confidence,
    bool UsedAiClassifier);

public sealed class IntentParameters
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public IntentParameters(IReadOnlyDictionary<string, string?>? values = null)
    {
        _values = values ?? new Dictionary<string, string?>();
    }

    public bool TryGetString(string key, out string? value) => _values.TryGetValue(key, out value);

    public string? GetStringOrDefault(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyDictionary<string, string?> AsReadOnly() => _values;
}

public interface IIntentClassifier
{
    Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed class DeterministicThenAiIntentClassifier : IIntentClassifier
{
    public Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassifyDeterministically(message));

    private static IntentClassificationResult ClassifyDeterministically(string message)
    {
        var normalized = (message ?? string.Empty).Trim().ToLowerInvariant();

        if (Mentions(normalized, "customer update", "customer communication", "draft update", "draft email", "draft message"))
        {
            return Result(IntentType.PrepareCustomerUpdateDraft, 0.88m, ExtractGuidParameter(message));
        }

        if (Mentions(normalized, "management summary", "executive summary", "management report", "operations summary"))
        {
            return Result(IntentType.GenerateManagementSummary, 0.88m);
        }

        if (Mentions(normalized, "approval", "approve", "sign off", "sign-off"))
        {
            return Result(IntentType.ShowPendingApprovals, 0.9m);
        }

        if (Mentions(normalized, "document", "missing", "incomplete", "checklist", "evidence pack"))
        {
            return Result(IntentType.ShowMissingDocuments, 0.9m);
        }

        if (Mentions(normalized, "sla", "breach", "overdue", "at risk", "at-risk", "due", "deadline"))
        {
            return Result(IntentType.ShowSlaBreaches, 0.9m);
        }

        if (Mentions(normalized, "claim", "case", "workload", "queue", "attention", "today"))
        {
            return Result(IntentType.ShowAttentionClaims, 0.85m);
        }

        return Result(IntentType.Unsupported, 0m);
    }

    private static IntentClassificationResult Result(
        IntentType intent,
        decimal confidence,
        IReadOnlyDictionary<string, string?>? parameters = null) =>
        new(intent, new IntentParameters(parameters), confidence, UsedAiClassifier: false);

    private static bool Mentions(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static IReadOnlyDictionary<string, string?> ExtractGuidParameter(string? message)
    {
        foreach (var token in (message ?? string.Empty)
            .Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var value))
            {
                return new Dictionary<string, string?> { ["claimId"] = value.ToString() };
            }
        }

        return new Dictionary<string, string?>();
    }
}

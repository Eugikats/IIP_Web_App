using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class MockAiAssistantProvider : IAiAssistantProvider
{
    private static readonly string[] FinalDecisionPhrases =
    [
        "claim is denied",
        "claim denied",
        "settlement approved",
        "approve settlement",
        "fraudulent claim",
        "fraud accusation"
    ];

    public Task<AssistantAnswer> AnswerPolicyQuestionAsync(
        AssistantQuestion question,
        RetrievalResult retrievalResult,
        CancellationToken cancellationToken = default)
    {
        if (retrievalResult.Chunks.Count == 0)
        {
            return Task.FromResult(new AssistantAnswer(
                "I cannot answer this from approved knowledge. Escalate this question to a human reviewer or knowledge manager before using it in a claim workflow.",
                AiConfidenceScore.Create(0.1m),
                EscalationRequired: true,
                Array.Empty<SourceCitation>(),
                "No approved source was found. The assistant did not use model memory.",
                "no_source",
                "LocalTest",
                null,
                "local-test-policy-v1",
                Visibility(question.Audience)));
        }

        var sourceSummary = string.Join(" ", retrievalResult.Chunks.Select(chunk => chunk.Content).Take(2));
        var customerSafePrefix = string.Equals(question.Audience, "Customer", StringComparison.OrdinalIgnoreCase)
            ? "Based on approved policy information available to the insurer, "
            : "Based on approved policy knowledge, ";
        var answerText = $"{customerSafePrefix}{Summarize(sourceSummary)} This is a grounded assistant response for human review and is not a final claim decision.";
        var sanitized = SanitizeDecisionLanguage(answerText);
        var confidence = retrievalResult.Chunks.Count >= 2 ? 0.82m : 0.78m;
        var escalate = confidence < 0.75m;

        return Task.FromResult(new AssistantAnswer(
            sanitized,
            AiConfidenceScore.Create(confidence),
            EscalationRequired: escalate,
            retrievalResult.Citations,
            "Answer generated only from approved retrieved knowledge. Final claim decisions remain human-supervised.",
            escalate ? "needs_review" : "grounded",
            "LocalTest",
            null,
            "local-test-policy-v1",
            Visibility(question.Audience)));
    }

    private static string Visibility(string audience) =>
        string.Equals(audience, "Customer", StringComparison.OrdinalIgnoreCase)
            ? "CustomerSafe"
            : "Internal";

    private static string Summarize(string sourceText)
    {
        var normalized = string.Join(' ', sourceText.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (normalized.Length == 0)
        {
            return "the retrieved source did not include enough plain text to summarize.";
        }

        return normalized.Length <= 420 ? normalized : $"{normalized[..420]}...";
    }

    private static string SanitizeDecisionLanguage(string answerText)
    {
        var sanitized = answerText;
        foreach (var phrase in FinalDecisionPhrases)
        {
            sanitized = sanitized.Replace(phrase, "requires human review", StringComparison.OrdinalIgnoreCase);
        }

        return sanitized;
    }
}

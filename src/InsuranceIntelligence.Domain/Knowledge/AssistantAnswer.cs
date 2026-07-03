namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record AssistantAnswer(
    string AnswerText,
    AiConfidenceScore ConfidenceScore,
    bool EscalationRequired,
    IReadOnlyCollection<SourceCitation> Sources,
    string GuardrailMessage,
    string RiskFlag,
    string ModelProvider = "Unknown",
    string? ModelDeployment = null,
    string PromptVersion = "unversioned",
    string Visibility = "Internal",
    bool CanTriggerDecision = false);

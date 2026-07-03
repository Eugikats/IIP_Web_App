namespace InsuranceIntelligence.Domain.Workflow;

public sealed record RecommendedNextAction(
    RecommendedNextActionType ActionType,
    string Label,
    string Summary,
    bool RequiresHumanApproval,
    string Priority,
    IReadOnlyCollection<string> Evidence);

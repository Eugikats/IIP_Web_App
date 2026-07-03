namespace InsuranceIntelligence.Api.Reports;

public sealed record OperationsSummaryResponse(
    int OpenClaims,
    int NewClaimsToday,
    int IncompleteClaims,
    decimal AverageCompletenessScore,
    int PendingApprovals,
    int OpenTasks,
    int CustomerUpdatesPending,
    int AssistantQuestionsAnswered,
    int EscalationsRequired,
    int SlaAtRisk,
    int SlaBreached,
    IReadOnlyCollection<StatusCountResponse> ClaimsByStatus,
    IReadOnlyCollection<StatusCountResponse> WorkQueueSummary);

public sealed record StatusCountResponse(string Label, int Count);

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
    // Same distinction as CommandCenterMetricsResponse: closed claims are excluded from
    // SlaBreached (an operational "needs attention" count), but a closed claim that breached SLA
    // before closing is a real, separately-tracked fact.
    int ClosedClaimsWithSlaBreach,
    IReadOnlyCollection<StatusCountResponse> ClaimsByStatus,
    IReadOnlyCollection<StatusCountResponse> WorkQueueSummary,
    DateTimeOffset DataAsOf,
    string SnapshotId);

public sealed record StatusCountResponse(string Label, int Count);

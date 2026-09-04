namespace InsuranceIntelligence.Application.CommandCenter;

// Metric snapshot DTO consumed by the chat-first query planner to build metric_pills
// blocks. The superseded prose command-center DTOs (CommandCenterQuestionRequest /
// CommandCenterAssistantResponse) were dropped in the v2 migration — the chat-first
// /query path replaces the prose /assistant path.
public sealed record CommandCenterMetricsResponse(
    int TotalCases,
    int TotalDocuments,
    decimal AverageCompletenessScore,
    int KnowledgeDocumentsApproved,
    int AssistantQuestionsAnswered,
    int EscalationsRequired,
    int OpenClaims,
    int NewClaimsToday,
    int IncompleteClaims,
    int PendingApprovals,
    int OpenTasks,
    int CustomerUpdatesPending,
    int SlaAtRisk,
    int SlaBreached,
    // Closed claims are excluded from SlaBreached (a closed claim isn't something today's team
    // needs to act on), but a closed claim that breached SLA before it closed is a real fact —
    // this keeps that fact visible as its own count instead of silently dropping it. Distinct
    // from SlaBreached deliberately: the two must never be summed into one "total breaches"
    // number, since they answer different questions (what needs attention now vs. what happened).
    int ClosedClaimsWithSlaBreach);

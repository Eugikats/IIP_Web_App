namespace InsuranceIntelligence.Api.CommandCenter;

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
    int SlaBreached);

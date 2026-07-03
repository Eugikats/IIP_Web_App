using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Compliance;

public sealed record AuditPack(
    Guid CaseId,
    string Status,
    IReadOnlyCollection<AuditEvent> AuditEvents,
    IReadOnlyCollection<CaseDocument> Documents,
    IReadOnlyCollection<AgentActionLog> AssistantActions,
    IReadOnlyCollection<ApprovalRequest> Approvals,
    IReadOnlyCollection<CustomerUpdateDraft> CustomerUpdates)
{
    private static readonly string[] BlockedTerms =
    [
        "raw prompt",
        "system message",
        "internal note",
        "fraud flag",
        "reserve",
        "hidden risk",
        "secret"
    ];

    public bool IsCustomerSafe() =>
        AuditEvents.All(IsSafe)
        && AssistantActions.All(action => IsSafe(action.InputSummary) && IsSafe(action.ModelOutput))
        && CustomerUpdates.All(update => IsSafe(update.MessageBody));

    public static AuditPack Build(
        ClaimCase claimCase,
        IReadOnlyCollection<AuditEvent> auditEvents,
        IReadOnlyCollection<CaseDocument> documents,
        IReadOnlyCollection<AgentActionLog> assistantActions,
        IReadOnlyCollection<ApprovalRequest> approvals,
        IReadOnlyCollection<CustomerUpdateDraft> customerUpdates) =>
        new(
            claimCase.CaseId,
            claimCase.Status.ToString(),
            auditEvents.Where(IsSafe).ToArray(),
            documents,
            assistantActions.Where(action => IsSafe(action.InputSummary) && IsSafe(action.ModelOutput)).ToArray(),
            approvals,
            customerUpdates.Where(update => IsSafe(update.MessageBody)).ToArray());

    private static bool IsSafe(AuditEvent auditEvent) =>
        IsSafe(auditEvent.ActionName)
        && IsSafe(auditEvent.ValidationResult)
        && IsSafe(auditEvent.AuditReason)
        && auditEvent.EvidenceIds.All(IsSafe);

    private static bool IsSafe(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || !BlockedTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}

using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Application.CommandCenter;

/// <summary>
/// Claim status in the words staff read on screen, from iip_workflow_legibility_contract.md
/// section 10.1. Used wherever the platform itself writes a sentence about a claim (assistant
/// replies, agent prompts), so a model is never handed "MissingDocuments" and asked to be natural.
/// </summary>
public static class ClaimStatusLabels
{
    public static string For(CaseStatus status) => status switch
    {
        CaseStatus.New => "Received",
        CaseStatus.Assigned => "Assigned",
        CaseStatus.InReview => "Under review",
        CaseStatus.MissingDocuments => "Waiting on documents",
        CaseStatus.ReadyForReview => "Ready for review",
        CaseStatus.PendingApproval => "Awaiting approval",
        CaseStatus.ReadyForExport => "Ready for export",
        CaseStatus.Escalated => "Escalated",
        CaseStatus.Settled => "Settled",
        CaseStatus.Closed => "Closed",
        _ => status.ToString(),
    };

    public static string For(string? status) =>
        Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var parsed) ? For(parsed) : status ?? "Unknown";

    public static bool IsOpen(CaseStatus status) => status is not (CaseStatus.Settled or CaseStatus.Closed);
}

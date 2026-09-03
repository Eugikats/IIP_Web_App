using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>
/// One claim's state as loaded for tool consumption — the same aggregation
/// CommandCenterQueryService.LoadQueryDataAsync performs today. Shared here so
/// claims.attention.read, claims.sla.read, and documents.gaps.read (three separately permissioned,
/// separately auditable tools per doc §9) don't each re-walk cases/documents/checklists — the
/// business rule (what a claim's SLA/completeness state is) lives in one place.
/// </summary>
public sealed record ClaimSnapshot(
    Guid CaseId,
    string CustomerName,
    string ProductLine,
    string Status,
    SlaStatus SlaStatus,
    decimal CompletenessScore,
    string AssignedTo,
    IReadOnlyCollection<string> MissingDocuments);

public sealed class ClaimSnapshotLoader(
    ICaseRepository caseRepository,
    ICaseDocumentRepository caseDocumentRepository,
    IDocumentChecklistProvider checklistProvider,
    ISlaCalculator slaCalculator)
{
    public async Task<IReadOnlyList<ClaimSnapshot>> LoadAsync(TenantScope scope, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var snapshots = new List<ClaimSnapshot>(cases.Count);

        foreach (var claimCase in cases)
        {
            var documents = await caseDocumentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
            var slaStatus = slaCalculator.Calculate(claimCase, now);

            snapshots.Add(new ClaimSnapshot(
                claimCase.CaseId,
                claimCase.CustomerName,
                claimCase.ProductLine,
                claimCase.Status.ToString(),
                slaStatus,
                claimCase.CompletenessScore,
                claimCase.AssignedUserId ?? claimCase.AssignedTeam ?? "Unassigned",
                checklist.MissingRequiredDocuments.Select(item => item.Label).ToArray()));
        }

        return snapshots;
    }
}

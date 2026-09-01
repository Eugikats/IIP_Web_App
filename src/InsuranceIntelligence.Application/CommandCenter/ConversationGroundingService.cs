using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.CommandCenter;

public interface IConversationGroundingService
{
    Task<(ConversationFocusReference? Focus, ConversationEvidenceSnapshot Snapshot)> CaptureAsync(
        CommandCenterQueryResponse response,
        TenantScope scope,
        CancellationToken cancellationToken = default);

    Task<ConversationGroundingSummary> CompareAsync(
        CommandCenterConversation conversation,
        CancellationToken cancellationToken = default);
}

public sealed class ConversationGroundingService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository) : IConversationGroundingService
{
    public async Task<(ConversationFocusReference? Focus, ConversationEvidenceSnapshot Snapshot)> CaptureAsync(
        CommandCenterQueryResponse response,
        TenantScope scope,
        CancellationToken cancellationToken = default)
    {
        var caseId = FindCaseId(response.Blocks);
        if (caseId is null)
        {
            return (null, new ConversationEvidenceSnapshot(response.DataAsOf, response.SnapshotId));
        }

        var current = await ReadCaseSnapshotAsync(caseId.Value, scope, cancellationToken);
        if (current is null)
        {
            return (null, new ConversationEvidenceSnapshot(response.DataAsOf, response.SnapshotId));
        }

        return (
            new ConversationFocusReference("claim", caseId.Value.ToString(), $"Claim {caseId.Value}", $"/app/claims/{caseId.Value}"),
            current);
    }

    public async Task<ConversationGroundingSummary> CompareAsync(
        CommandCenterConversation conversation,
        CancellationToken cancellationToken = default)
    {
        var historical = conversation.EvidenceSnapshot
            ?? new ConversationEvidenceSnapshot(conversation.LastActivityAt, conversation.Turns.LastOrDefault()?.Response.SnapshotId ?? "conversation-version-unavailable");

        if (conversation.PrimaryFocus is not { Type: "claim" } focus
            || !Guid.TryParse(focus.Reference, out var caseId))
        {
            return Unavailable(historical, "Current evidence could not be checked for this saved workspace answer.");
        }

        var current = await ReadCaseSnapshotAsync(caseId, conversation.Scope, cancellationToken);
        if (current is null)
        {
            return Unavailable(historical, "The related claim is unavailable or no longer accessible in this workspace.");
        }

        var changed = !string.Equals(historical.Version, current.Version, StringComparison.Ordinal);
        return new ConversationGroundingSummary(
            historical.Timestamp,
            historical.Version,
            current.Timestamp,
            current.Version,
            changed ? "changed" : "current",
            changed
                ? "Claim or document evidence changed after this answer was saved. Recheck cited evidence before acting."
                : "No recorded claim or document changes were found since this answer was saved.");
    }

    private async Task<ConversationEvidenceSnapshot?> ReadCaseSnapshotAsync(
        Guid caseId,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || !claim.Scope.Matches(scope)) return null;

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var timestamp = documents
            .SelectMany(document => new DateTimeOffset?[]
            {
                document.UploadedAt,
                document.ProcessedAt,
                document.Extraction.ExtractedAt,
                document.Extraction.ApprovedAt,
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Concat(documents.SelectMany(document => (document.Extraction.FieldCorrections ?? new Dictionary<string, DocumentFieldCorrection>()).Values.Select(correction => correction.CorrectedAt)))
            .Append(claim.UpdatedAt)
            .Max();

        var material = new StringBuilder()
            .Append(claim.CaseId).Append('|')
            .Append(claim.UpdatedAt.ToUnixTimeMilliseconds()).Append('|')
            .Append(claim.Status).Append('|')
            .Append(claim.CompletenessScore);
        foreach (var document in documents.OrderBy(document => document.DocumentId))
        {
            material.Append('|').Append(document.DocumentId)
                .Append(':').Append(document.ProcessingStatus)
                .Append(':').Append(document.ProcessedAt?.ToUnixTimeMilliseconds())
                .Append(':').Append(document.Extraction.ExtractedAt.ToUnixTimeMilliseconds())
                .Append(':').Append(document.Extraction.ApprovedAt?.ToUnixTimeMilliseconds());
            foreach (var correction in (document.Extraction.FieldCorrections ?? new Dictionary<string, DocumentFieldCorrection>()).OrderBy(item => item.Key))
            {
                material.Append(':').Append(correction.Key)
                    .Append('=').Append(correction.Value.CorrectedValue)
                    .Append('@').Append(correction.Value.CorrectedAt.ToUnixTimeMilliseconds());
            }
        }

        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()))).ToLowerInvariant()[..16];
        return new ConversationEvidenceSnapshot(timestamp, $"claim-{version}");
    }

    private static Guid? FindCaseId(IReadOnlyList<CommandCenterResponseBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var value = block switch
            {
                ClaimsTableBlock { Rows.Count: > 0 } claims => claims.Rows[0].CaseId,
                SlaSummaryBlock { Items.Count: > 0 } sla => sla.Items[0].CaseId,
                DocumentGapSummaryBlock { Items.Count: > 0 } gaps => gaps.Items[0].CaseId,
                ApprovalQueueBlock { Items.Count: > 0 } approvals => approvals.Items[0].CaseId,
                _ => null,
            };
            if (Guid.TryParse(value, out var caseId)) return caseId;
        }
        return null;
    }

    private static ConversationGroundingSummary Unavailable(ConversationEvidenceSnapshot historical, string summary) =>
        new(historical.Timestamp, historical.Version, null, null, "unavailable", summary);
}

using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Knowledge;

public static class KnowledgeMapper
{
    public static KnowledgeDocumentResponse ToResponse(KnowledgeDocument document) =>
        new(
            document.KnowledgeDocumentId,
            document.DocumentName,
            document.ProductLine,
            document.DocumentType.ToString(),
            document.Version,
            document.EffectiveDate,
            document.ExpiryDate,
            document.OriginalFileName,
            document.ContentType,
            document.FileSizeBytes,
            document.ApprovalStatus.ToString(),
            document.IndexedStatus.ToString(),
            document.Sensitivity.ToString(),
            document.GovernanceReason,
            document.Chunks.Count,
            document.CreatedAt,
            document.UpdatedAt,
            Allowed(document),
            Blocked(document));

    public static KnowledgeDocumentDetailResponse ToDetailResponse(KnowledgeDocument document) =>
        new(
            ToResponse(document),
            document.Chunks
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(ToResponse)
                .ToArray());

    private static KnowledgeChunkResponse ToResponse(KnowledgeChunk chunk)
    {
        var excerpt = chunk.Content.Length <= 240 ? chunk.Content : $"{chunk.Content[..240]}...";
        return new KnowledgeChunkResponse(
            chunk.ChunkId,
            chunk.ChunkIndex,
            excerpt,
            $"{chunk.DocumentName} v{chunk.Version}, chunk {chunk.ChunkIndex + 1}");
    }

    public static IReadOnlyCollection<ActionStateResponse> Allowed(KnowledgeDocument document)
    {
        var actions = new List<ActionStateResponse>();

        if (ApproveBlockedReason(document) is null)
        {
            actions.Add(Action("approveKnowledgeDocument", "Approve knowledge document", Permission.ManageKnowledgeBase));
        }

        if (ArchiveBlockedReason(document) is null)
        {
            actions.Add(Action("archiveKnowledgeDocument", "Archive knowledge document", Permission.ManageKnowledgeBase));
        }

        return actions;
    }

    public static IReadOnlyCollection<ActionStateResponse> Blocked(KnowledgeDocument document)
    {
        var actions = new List<ActionStateResponse>();
        var approveReason = ApproveBlockedReason(document);
        if (approveReason is not null)
        {
            actions.Add(Action("approveKnowledgeDocument", "Approve knowledge document", Permission.ManageKnowledgeBase, approveReason));
        }

        var archiveReason = ArchiveBlockedReason(document);
        if (archiveReason is not null)
        {
            actions.Add(Action("archiveKnowledgeDocument", "Archive knowledge document", Permission.ManageKnowledgeBase, archiveReason));
        }

        return actions;
    }

    public static string? ApproveBlockedReason(KnowledgeDocument document)
    {
        if (document.Chunks.Count == 0)
        {
            return "Knowledge without extractable source text cannot be approved for AI grounding.";
        }

        return document.ApprovalStatus switch
        {
            KnowledgeDocumentStatus.Draft => null,
            KnowledgeDocumentStatus.Approved when document.IndexedStatus == KnowledgeIndexedStatus.Failed =>
                "Approved knowledge with failed indexing should be reindexed, not approved again.",
            KnowledgeDocumentStatus.Approved when document.IndexedStatus == KnowledgeIndexedStatus.NotIndexed =>
                "Approved knowledge should be reindexed, not approved again.",
            KnowledgeDocumentStatus.Approved => "This knowledge document is already approved.",
            KnowledgeDocumentStatus.Archived => "Archived knowledge cannot be approved again. Upload a new version instead.",
            _ => "This knowledge document cannot be approved from its current state."
        };
    }

    public static string? ArchiveBlockedReason(KnowledgeDocument document) =>
        document.ApprovalStatus == KnowledgeDocumentStatus.Archived
            ? "This knowledge document is already archived."
            : null;

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);
}

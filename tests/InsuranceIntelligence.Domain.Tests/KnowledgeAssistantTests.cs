using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class KnowledgeAssistantTests
{
    [Fact]
    public void ApprovedIndexedDocumentIsRetrievalEligible()
    {
        var document = ValidDocument() with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed
        };

        Assert.True(document.IsActiveOn(new DateOnly(2026, 5, 24)));
    }

    [Fact]
    public void ArchivedDocumentIsNotRetrievalEligible()
    {
        var document = ValidDocument() with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Archived,
            IndexedStatus = KnowledgeIndexedStatus.Indexed
        };

        Assert.False(document.IsActiveOn(new DateOnly(2026, 5, 24)));
    }

    [Fact]
    public void SourceReferencesAreAttachedToAssistantAnswers()
    {
        var citation = new SourceCitation(
            Guid.NewGuid(),
            "chunk-1",
            "Motor Policy Wording",
            "Motor",
            "1.0",
            "Windscreen damage is covered subject to policy terms.",
            "Motor Policy Wording v1.0, chunk 1");
        var answer = new AssistantAnswer(
            "Based on approved policy knowledge, windscreen damage is covered subject to policy terms.",
            AiConfidenceScore.Create(0.82m),
            EscalationRequired: false,
            [citation],
            "Answer generated only from approved retrieved knowledge.",
            "grounded");

        Assert.Single(answer.Sources);
        Assert.Equal("Motor Policy Wording", answer.Sources.Single().DocumentName);
    }

    [Fact]
    public void NoSourceAnswerEscalates()
    {
        var answer = new AssistantAnswer(
            "I cannot answer this from approved knowledge.",
            AiConfidenceScore.Create(0.1m),
            EscalationRequired: true,
            Array.Empty<SourceCitation>(),
            "No approved source was found.",
            "no_source");

        Assert.True(answer.EscalationRequired);
        Assert.Empty(answer.Sources);
    }

    [Fact]
    public void PolicyQuestionAnswerDoesNotProduceFinalClaimDecision()
    {
        var answer = new AssistantAnswer(
            "This is a grounded assistant response for human review and is not a final claim decision.",
            AiConfidenceScore.Create(0.82m),
            EscalationRequired: false,
            Array.Empty<SourceCitation>(),
            "Final claim decisions remain human-supervised.",
            "grounded");

        Assert.DoesNotContain("claim is denied", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settlement approved", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fraud", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeDocument ValidDocument()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var documentId = Guid.NewGuid();
        var chunk = new KnowledgeChunk
        {
            ChunkId = "chunk-1",
            KnowledgeDocumentId = documentId,
            Scope = scope,
            DocumentName = "Motor Policy Wording",
            ProductLine = "Motor",
            Version = "1.0",
            ChunkIndex = 0,
            Content = "Windscreen damage is covered subject to policy terms.",
            SourceReference = new KnowledgeSourceReference(documentId, "chunk-1", "Motor Policy Wording", "Motor", "1.0", "tenant/workspace/knowledge/file.md")
        };

        return new KnowledgeDocument
        {
            KnowledgeDocumentId = documentId,
            Scope = scope,
            DocumentName = "Motor Policy Wording",
            ProductLine = "Motor",
            DocumentType = KnowledgeDocumentType.PolicyWording,
            Version = "1.0",
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpiryDate = null,
            OriginalFileName = "motor-policy.md",
            ContentType = "text/markdown",
            FileSizeBytes = 128,
            ObjectKey = "tenant/workspace/knowledge/file.md",
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            Chunks = [chunk],
            UploadedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-001"
        };
    }
}

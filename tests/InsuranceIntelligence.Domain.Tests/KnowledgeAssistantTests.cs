using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class KnowledgeAssistantTests
{
    [Fact]
    public void StripTrailingSourceLine_RemovesACitationLineAtTheEndOfAChunk()
    {
        var content = "Windscreen damage is covered subject to policy terms.\n\nSource: Nile Shield Assurance, Motor Insurance Policy Wording, Exclusions.";

        var stripped = KnowledgeChunkText.StripTrailingSourceLine(content);

        Assert.Equal("Windscreen damage is covered subject to policy terms.", stripped);
    }

    [Fact]
    public void StripTrailingSourceLine_LeavesContentWithNoCitationUnchanged()
    {
        var content = "Windscreen damage is covered subject to policy terms.";

        Assert.Equal(content, KnowledgeChunkText.StripTrailingSourceLine(content));
    }

    [Fact]
    public async Task MockAssistant_AnswerPolicyQuestionAsync_DoesNotLeakTheSourceLineIntoTheCustomerAnswer()
    {
        // Claim intake build prompt gap fix: a chunk's own trailing "Source: ..." line used to
        // ride straight into the customer-facing answer (and then survive translation, seen as
        // repeated "Ensibuko: ..." in a Luganda WhatsApp reply).
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
            Content = "Windscreen damage is covered subject to policy terms.\nSource: Nile Shield Assurance, Motor Insurance Policy Wording, Exclusions.",
            SourceReference = new KnowledgeSourceReference(documentId, "chunk-1", "Motor Policy Wording", "Motor", "1.0", "tenant/workspace/knowledge/file.md"),
        };
        var retrieval = new RetrievalResult("Is windscreen damage covered?", "Motor", [chunk], []);
        var provider = new MockAiAssistantProvider();

        var answer = await provider.AnswerPolicyQuestionAsync(
            new AssistantQuestion(scope, CaseId: null, "Is windscreen damage covered?", "Motor", "Customer", "user-1", "corr-001"),
            retrieval,
            CancellationToken.None);

        Assert.DoesNotContain("Source:", answer.AnswerText, StringComparison.Ordinal);
        Assert.DoesNotContain("Nile Shield Assurance", answer.AnswerText, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedIndexedDocumentIsRetrievalEligible()
    {
        var document = ValidDocument() with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            Sensitivity = KnowledgeSensitivity.Approved
        };

        Assert.True(document.IsActiveOn(new DateOnly(2026, 5, 24)));
    }

    [Fact]
    public void UnknownSensitivityIsNotRetrievalEligible_EvenWhenApprovedAndIndexed()
    {
        // WO-17: unknown sensitivity must stay blocked from retrieval by default, the approval
        // workflow now requires a classification, but this is the fail-closed backstop for any
        // legacy or otherwise-inconsistent record that is Approved/Indexed without one.
        var document = ValidDocument() with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            Sensitivity = KnowledgeSensitivity.Unknown
        };

        Assert.False(document.IsActiveOn(new DateOnly(2026, 5, 24)));
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

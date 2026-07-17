using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class ProviderNotConfiguredException(string message) : InvalidOperationException(message);

public sealed class NotConfiguredDocumentProcessingProvider : IDocumentProcessingProvider
{
    public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Document processing provider is not configured.");
}

public sealed class NotConfiguredKnowledgeIndexProvider : IKnowledgeIndexProvider
{
    public Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Knowledge indexing provider is not configured.");

    public Task RemoveAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Knowledge indexing provider is not configured.");

    public Task RebuildAsync(TenantScope scope, IReadOnlyCollection<KnowledgeDocument> approvedDocuments, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Knowledge indexing provider is not configured.");

    public Task<IReadOnlyCollection<KnowledgeChunk>> SearchAsync(
        TenantScope scope,
        string query,
        string? productLine,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<KnowledgeChunk>>(Array.Empty<KnowledgeChunk>());
}

public sealed class NotConfiguredRetrievalProvider : IRetrievalProvider
{
    public Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RetrievalResult(
            question.Question,
            question.ProductLine,
            Array.Empty<KnowledgeChunk>(),
            Array.Empty<SourceCitation>()));
}

public sealed class NotConfiguredAiAssistantProvider : IAiAssistantProvider
{
    public Task<AssistantAnswer> AnswerPolicyQuestionAsync(
        AssistantQuestion question,
        RetrievalResult retrievalResult,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssistantAnswer(
            "Policy assistant is not configured. Escalate this question to a human reviewer.",
            AiConfidenceScore.Create(0m),
            EscalationRequired: true,
            Array.Empty<SourceCitation>(),
            "No AI provider is configured; the assistant did not generate a policy answer.",
            "provider_not_configured",
            "NotConfigured",
            null,
            "not-configured",
            string.Equals(question.Audience, "Customer", StringComparison.OrdinalIgnoreCase) ? "CustomerSafe" : "Internal"));
}

public sealed class NotConfiguredCustomerUpdateDraftingProvider : ICustomerUpdateDraftingProvider
{
    public Task<CustomerUpdateDraft> DraftAsync(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        CustomerUpdateReasonCategory reasonCategory,
        CommunicationChannel channel,
        string createdByUserId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("Customer update drafting provider is not configured.");
}

public sealed class NotConfiguredCommunicationProvider : ICommunicationProvider
{
    public Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("Customer communication provider is not configured.");
}

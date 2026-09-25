using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

// Capability is a stable machine-readable identifier for the missing provider (e.g.
// "DocumentStorage"), distinct from the human-readable message. The exception boundary
// (see ProviderNotConfiguredExceptionHandler) uses it to build a structured, honest
// response instead of letting a raw provider error reach the client (AGENTS.md I9).
public sealed class ProviderNotConfiguredException(string capability, string message) : InvalidOperationException(message)
{
    public string Capability { get; } = capability;
}

public sealed class NotConfiguredDocumentProcessingProvider : IDocumentProcessingProvider
{
    public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("DocumentProcessing", "Document processing provider is not configured.");
}

public sealed class NotConfiguredKnowledgeIndexProvider : IKnowledgeIndexProvider
{
    public Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("KnowledgeIndexing", "Knowledge indexing provider is not configured.");

    public Task RemoveAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("KnowledgeIndexing", "Knowledge indexing provider is not configured.");

    public Task RebuildAsync(TenantScope scope, IReadOnlyCollection<KnowledgeDocument> approvedDocuments, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("KnowledgeIndexing", "Knowledge indexing provider is not configured.");

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
        throw new ProviderNotConfiguredException("CustomerUpdateDrafting", "Customer update drafting provider is not configured.");
}

public sealed class NotConfiguredCommunicationProvider : ICommunicationProvider
{
    public Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("CustomerCommunication", "Customer communication provider is not configured.");
}

public sealed class NotConfiguredDocumentStorageProvider : IDocumentStorageProvider
{
    public Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("DocumentStorage", "Document storage provider is not configured.");
}

public sealed class NotConfiguredKnowledgeStorageProvider : IKnowledgeStorageProvider
{
    public Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("KnowledgeStorage", "Knowledge storage provider is not configured.");
}

/// <summary>TranslationService checks IsConfigured before ever calling TranslateAsync and falls
/// back to English on false, so this throwing is a defensive second layer, matching every other
/// NotConfigured* provider here, not the expected path.</summary>
public sealed class NotConfiguredTranslationProvider : ITranslationProvider
{
    public bool IsConfigured => false;

    public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("Translation", "Translation provider is not configured.");
}

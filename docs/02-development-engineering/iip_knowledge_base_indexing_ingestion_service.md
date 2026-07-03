# IIP Knowledge Base Indexing and Ingestion Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready Application-layer service output  
**Area:** Knowledge Base ingestion, sensitivity gating, chunking, embedding, and Azure AI Search indexing  
**Status:** New-repository implementation specification  
**Date:** July 2026  

---

## Shared rules used by this service output

This output follows the same implementation pattern used by the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, Audit, Claim Intake/Workflow, Write-Back, and Notification Send.

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any read/write.
3. Apply tenantId and workspaceId filters in the repository/query boundary.
4. Never trust client-supplied tenant/workspace IDs, roles, permissions, or provider status.
5. Write durable audit events for state-changing operations and sensitive reads.
6. Never fabricate records when no data exists.
7. Never treat provider failure as fake success.
8. Surface missing providers as ProviderNotConfiguredException or ProviderUnavailableException.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, and provider SDK dependencies.
11. Test doubles may exist only in automated tests or isolated LocalTest assemblies.
12. Staging, Production, and ManagedApplicationReviewer must never resolve Mock, InMemory, Simulated, Placeholder, Demo, DevelopmentAuth, SeededDemoBusinessData, or FallbackToMock implementations.
13. AI/system actors must never be final actors for consequential insurance decisions.
14. Knowledge used by AI must be tenant-scoped, workspace-aware where applicable, approved for retrieval, version-aware, and auditable.
15. Never index documents containing secrets, disallowed PII, customer-confidential content, staff-only content, or unapproved sensitive material into AI retrieval indexes.
```

This document supplies the missing ingestion/indexing side that feeds the already-existing retrieval consumer side:

```text
IRetrievalProvider.SearchAsync(RetrievalRequest request, CancellationToken ct) -> RetrievalResult
```

The retrieval consumer contract is fixed and must not be changed by this service.

---

## 1. Scope of this pass

This pass implements one new Application service:

```text
IKnowledgeIngestionService
```

Implemented capabilities:

```text
- Ingest a knowledge document into the IIP retrieval index.
- Enforce knowledge.manage before ingestion.
- Load the knowledge document from the existing IKnowledgeDocumentRepository / EfKnowledgeDocumentRepository source of truth.
- Check azure_ai_search provider health before indexing.
- Check azure_openai provider health before embedding generation.
- Reject documents that are not explicitly approved for AI retrieval.
- Run a pre-index sensitivity classification gate.
- Chunk document content using a deterministic fixed-size-with-overlap strategy.
- Generate embeddings using AzureOpenAiOptions.EmbeddingDeployment through an embedding provider port.
- Push chunks and embeddings to Azure AI Search using AzureAiSearchOptions.IndexName through an indexing provider port.
- Update the knowledge document index status to indexing, indexed, index_failed, or indexing_blocked.
- Write a knowledge.document.indexed audit event with document ID and chunk count only.
- Write safe failure audit events without raw content, embeddings, secrets, or provider payloads.
- Return a stable ingestion result for callers and background workers.
```

Not implemented in this pass:

```text
- IRetrievalProvider.SearchAsync consumer-side retrieval contract.
- Azure AI Search concrete adapter internals.
- Azure OpenAI embedding concrete adapter internals.
- Knowledge upload UI.
- Knowledge approval workflow UI.
- Re-indexing orchestration for large batches.
- Retry/backoff policies.
- Full semantic chunking, heading-aware chunking, table-aware chunking, or OCR extraction.
- Claim, policy, or customer business decisions.
```

Important:

```text
This service is not a chat service and does not answer questions.
It prepares approved knowledge for retrieval.
It must never index unsafe or unapproved documents just because a document exists in storage.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create parallel architecture if equivalent files already exist.

Recommended files:

```text
src/InsuranceIntelligence.Application/Knowledge/IKnowledgeIngestionService.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionService.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionModels.cs
src/InsuranceIntelligence.Application/Knowledge/IKnowledgeDocumentRepository.cs
src/InsuranceIntelligence.Application/Knowledge/IKnowledgeSensitivityClassifier.cs
src/InsuranceIntelligence.Application/Knowledge/IKnowledgeChunker.cs
src/InsuranceIntelligence.Application/Knowledge/FixedSizeKnowledgeChunker.cs
src/InsuranceIntelligence.Application/Knowledge/IEmbeddingProvider.cs
src/InsuranceIntelligence.Application/Knowledge/IKnowledgeSearchIndexWriter.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionPermissions.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionMessage.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIndexingFunctionContract.md
src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeIngestionServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeChunkerTests.cs
src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeIngestionContractTests.cs
```

Infrastructure files expected later, but not implemented in this pass:

```text
src/InsuranceIntelligence.Infrastructure/Knowledge/EfKnowledgeDocumentRepository.cs
src/InsuranceIntelligence.Infrastructure/Providers/OpenAi/AzureOpenAiEmbeddingProvider.cs
src/InsuranceIntelligence.Infrastructure/Providers/Search/AzureAiSearchKnowledgeIndexWriter.cs
src/InsuranceIntelligence.Functions/KnowledgeIndexing/KnowledgeIndexingFunction.cs
```

---

## 3. Dependencies expected

This service expects the following shared abstractions to already exist from the core ports and Application service specs:

```csharp
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
Permissions
TenantContext
AuditEventCreateRequest
AuditEventTypeValues
ForbiddenException
ValidationException
NotFoundOrNotVisibleException
ProviderNotConfiguredException
ProviderUnavailableException
WorkflowConflictException
```

Provider codes already expected:

```text
azure_ai_search
azure_openai
```

If `ProviderCodes.AzureAiSearch` and `ProviderCodes.AzureOpenAi` exist, use those constants instead of string literals.

---

## 4. Permission constant

Add this permission to the existing canonical `Permissions` class. Do not create a second competing permission class if `Permissions` already exists.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string KnowledgeManage = "knowledge.manage";
}
```

If the repository does not use partial classes, add this line directly to the existing `Permissions` class:

```csharp
public const string KnowledgeManage = "knowledge.manage";
```

Rules:

```text
- knowledge.manage is required to ingest, approve-for-indexing, re-index, or remove knowledge documents from retrieval.
- knowledge.read may be added later for read-only Knowledge Base screens, but this pass requires only knowledge.manage.
```

---

## 5. Service contract

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> IngestDocumentAsync(
        string knowledgeDocumentId,
        CancellationToken cancellationToken);
}
```

The service returns an operational result, not raw provider output.

---

## 6. Models

### 6.1 Ingestion result

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeIngestionResult(
    string KnowledgeDocumentId,
    string IndexStatus,
    int ChunkCount,
    string IndexedAt,
    string? FailureReason);
```

Allowed `IndexStatus` values:

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public static class KnowledgeIndexStatusValues
{
    public const string NotIndexed = "not_indexed";
    public const string Indexing = "indexing";
    public const string Indexed = "indexed";
    public const string IndexFailed = "index_failed";
    public const string IndexingBlocked = "indexing_blocked";
}
```

### 6.2 Knowledge document read model

The current repo already has `IKnowledgeDocumentRepository` / `EfKnowledgeDocumentRepository`. Reuse it as the source of truth. If its current method names differ, adapt the repository implementation while preserving this Application service contract.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeDocumentForIngestion(
    string KnowledgeDocumentId,
    string TenantId,
    string? WorkspaceId,
    string Title,
    string DocumentType,
    string Content,
    string SourceUri,
    string ApprovalStatus,
    string SensitivityLabel,
    bool IsApprovedForAiRetrieval,
    string Version,
    DateTimeOffset UpdatedAt);
```

Allowed source document examples:

```text
policy_wording
underwriting_guideline
claims_procedure
approved_communication_template
customer_faq
broker_faq
regulatory_guidance
complaint_handling_procedure
```

### 6.3 Chunk model

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeChunk(
    string ChunkId,
    string KnowledgeDocumentId,
    string TenantId,
    string? WorkspaceId,
    string Title,
    string DocumentType,
    string Version,
    int Sequence,
    string Content,
    string SourceUri,
    DateTimeOffset SourceUpdatedAt);
```

### 6.4 Embedding request/result

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record EmbeddingRequest(
    string TenantId,
    string? WorkspaceId,
    string DeploymentName,
    IReadOnlyList<string> Inputs,
    string CorrelationId);

public sealed record EmbeddingResult(
    IReadOnlyList<EmbeddingVector> Vectors);

public sealed record EmbeddingVector(
    int Sequence,
    IReadOnlyList<float> Values);
```

### 6.5 Search index document

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeSearchIndexDocument(
    string Id,
    string TenantId,
    string? WorkspaceId,
    string KnowledgeDocumentId,
    string Title,
    string DocumentType,
    string Version,
    int Sequence,
    string Content,
    string SourceUri,
    DateTimeOffset SourceUpdatedAt,
    IReadOnlyList<float> Embedding);
```

### 6.6 Sensitivity classification

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeSensitivityResult(
    bool ApprovedForAiRetrieval,
    string Classification,
    IReadOnlyList<string> Reasons);
```

Allowed classification values for this pass:

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public static class KnowledgeSensitivityClassificationValues
{
    public const string Approved = "approved";
    public const string CustomerConfidential = "customer_confidential";
    public const string StaffOnly = "staff_only";
    public const string ContainsSecrets = "contains_secrets";
    public const string ContainsUnapprovedPii = "contains_unapproved_pii";
    public const string Unknown = "unknown";
}
```

---

## 7. Repository and provider ports

### 7.1 Knowledge document repository

Reuse the existing `IKnowledgeDocumentRepository` / `EfKnowledgeDocumentRepository` in the current repo. If it does not yet include the methods below, extend it rather than introducing a second repository for the same aggregate.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocumentForIngestion?> GetForIngestionAsync(
        string tenantId,
        string? workspaceId,
        string knowledgeDocumentId,
        CancellationToken cancellationToken);

    Task MarkIndexingAsync(
        string tenantId,
        string? workspaceId,
        string knowledgeDocumentId,
        DateTimeOffset startedAt,
        string correlationId,
        CancellationToken cancellationToken);

    Task MarkIndexedAsync(
        string tenantId,
        string? workspaceId,
        string knowledgeDocumentId,
        int chunkCount,
        DateTimeOffset indexedAt,
        string correlationId,
        CancellationToken cancellationToken);

    Task MarkIndexFailedAsync(
        string tenantId,
        string? workspaceId,
        string knowledgeDocumentId,
        string safeFailureReason,
        DateTimeOffset failedAt,
        string correlationId,
        CancellationToken cancellationToken);

    Task MarkIndexingBlockedAsync(
        string tenantId,
        string? workspaceId,
        string knowledgeDocumentId,
        string safeBlockedReason,
        DateTimeOffset blockedAt,
        string correlationId,
        CancellationToken cancellationToken);
}
```

### 7.2 Sensitivity classifier

If the current repo has no equivalent, define this Application-layer interface and provide a real production implementation backed by deterministic rules and, later, approved content safety tooling. It is not optional.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IKnowledgeSensitivityClassifier
{
    Task<KnowledgeSensitivityResult> ClassifyAsync(
        KnowledgeDocumentForIngestion document,
        CancellationToken cancellationToken);
}
```

Minimum deterministic production implementation:

```text
- Reject if IsApprovedForAiRetrieval is false.
- Reject if SensitivityLabel is customer_confidential.
- Reject if SensitivityLabel is staff_only.
- Reject if SensitivityLabel is contains_secrets.
- Reject if SensitivityLabel is contains_unapproved_pii.
- Reject unknown/unclassified documents by default.
- Approve only explicit approved labels/statuses.
```

### 7.3 Chunker

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IKnowledgeChunker
{
    IReadOnlyList<KnowledgeChunk> Chunk(KnowledgeDocumentForIngestion document, TenantContext context);
}
```

### 7.4 Embedding provider

This is an Application-layer port. The concrete Azure OpenAI adapter belongs in Infrastructure and must use the configured `AzureOpenAiOptions.EmbeddingDeployment`.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IEmbeddingProvider
{
    Task<EmbeddingResult> GenerateEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}
```

### 7.5 Knowledge search index writer

This is an Application-layer port. The concrete Azure AI Search adapter belongs in Infrastructure and must write to `AzureAiSearchOptions.IndexName`.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public interface IKnowledgeSearchIndexWriter
{
    Task UpsertChunksAsync(
        string tenantId,
        string? workspaceId,
        string indexName,
        IReadOnlyList<KnowledgeSearchIndexDocument> documents,
        CancellationToken cancellationToken);
}
```

---

## 8. Configuration dependencies

The service needs the following configuration values from the already-defined `InfrastructureOptions` tree. Do not create a second configuration system.

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeIndexingOptions(
    string AzureAiSearchIndexName,
    string AzureOpenAiEmbeddingDeployment,
    int ChunkSizeCharacters = 1800,
    int ChunkOverlapCharacters = 250,
    int MaxChunksPerDocument = 1000);
```

Recommended registration mapping:

```csharp
services.AddSingleton(new KnowledgeIndexingOptions(
    AzureAiSearchIndexName: infrastructureOptions.AzureAiSearch.IndexName,
    AzureOpenAiEmbeddingDeployment: infrastructureOptions.AzureOpenAI.EmbeddingDeployment,
    ChunkSizeCharacters: configuration.GetValue<int?>("KnowledgeIndexing:ChunkSizeCharacters") ?? 1800,
    ChunkOverlapCharacters: configuration.GetValue<int?>("KnowledgeIndexing:ChunkOverlapCharacters") ?? 250,
    MaxChunksPerDocument: configuration.GetValue<int?>("KnowledgeIndexing:MaxChunksPerDocument") ?? 1000));
```

Rules:

```text
- AzureAiSearchIndexName is required.
- AzureOpenAiEmbeddingDeployment is required.
- In protected runtimes, missing settings must fail startup through AzureProviderStartupValidator or explicit options validation before the service is invoked.
- Do not default to a fake or test index in protected runtimes.
```

---

## 9. Business rules

### 9.1 Indexability

A knowledge document is indexable only when all of the following are true:

```text
- Document exists in the current tenant/workspace scope.
- User has knowledge.manage.
- Document ApprovalStatus is approved.
- Document IsApprovedForAiRetrieval is true.
- Sensitivity classifier returns ApprovedForAiRetrieval = true.
- azure_ai_search provider is Connected.
- azure_openai provider is Connected.
- Azure AI Search index name is configured.
- Azure OpenAI embedding deployment is configured.
- Content is non-empty after normalization.
```

### 9.2 Non-indexable sensitivity labels

Never index documents with any of these classifications:

```text
customer_confidential
staff_only
contains_secrets
contains_unapproved_pii
unknown
```

The service must fail closed. Unknown or unclassified content is blocked.

### 9.3 Chunking strategy

Use a deterministic fixed-size chunker for this pass:

```text
- Normalize CRLF to LF.
- Trim leading/trailing whitespace.
- Collapse excessive whitespace only where it does not change meaning.
- Chunk by character count, not tokens, for MVP simplicity.
- Default chunk size: 1800 characters.
- Default overlap: 250 characters.
- Preserve sequence numbers starting at 0.
- Use stable chunk IDs based on knowledgeDocumentId, version, and sequence.
- Stop and fail validation if chunk size <= overlap.
- Stop and fail validation if MaxChunksPerDocument is exceeded.
```

`// TODO: confirm`: exact chunk size, overlap, tokenization strategy, heading-aware splitting, table handling, and language-specific chunking.

### 9.4 Index status rules

```text
- Before provider calls, mark status = indexing.
- On success, mark status = indexed with chunk count.
- On provider unavailability or index writer failure, mark status = index_failed.
- On sensitivity rejection, mark status = indexing_blocked.
- On validation failure for empty content or unsafe config, mark status = index_failed unless it is a sensitivity block.
```

### 9.5 Audit rules

Audit event types/actions:

```text
EventType: knowledge.document.indexed
Action: indexing_started
Action: indexing_completed
Action: indexing_failed
Action: indexing_blocked
```

Audit summaries may include:

```text
- knowledgeDocumentId
- title truncated to a safe length
- chunk count
- safe status
- safe reason
```

Audit summaries must not include:

```text
- raw document content
- raw chunks
- embeddings
- prompt text
- secrets
- SAS URLs
- raw provider exceptions
- full policy wording
- personal identifiers
- raw customer records
```

---

## 10. Full C# implementation

### 10.1 Fixed-size chunker

```csharp
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.Knowledge;

public sealed class FixedSizeKnowledgeChunker : IKnowledgeChunker
{
    private readonly KnowledgeIndexingOptions _options;

    public FixedSizeKnowledgeChunker(KnowledgeIndexingOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<KnowledgeChunk> Chunk(KnowledgeDocumentForIngestion document, TenantContext context)
    {
        if (_options.ChunkSizeCharacters <= 0)
        {
            throw new ValidationException("ChunkSizeCharacters must be greater than zero.");
        }

        if (_options.ChunkOverlapCharacters < 0)
        {
            throw new ValidationException("ChunkOverlapCharacters must not be negative.");
        }

        if (_options.ChunkOverlapCharacters >= _options.ChunkSizeCharacters)
        {
            throw new ValidationException("ChunkOverlapCharacters must be smaller than ChunkSizeCharacters.");
        }

        var normalized = NormalizeContent(document.Content);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException("Knowledge document content is empty and cannot be indexed.");
        }

        var chunks = new List<KnowledgeChunk>();
        var start = 0;
        var sequence = 0;

        while (start < normalized.Length)
        {
            if (sequence >= _options.MaxChunksPerDocument)
            {
                throw new ValidationException("Knowledge document exceeds the maximum allowed chunk count.");
            }

            var length = Math.Min(_options.ChunkSizeCharacters, normalized.Length - start);
            var content = normalized.Substring(start, length).Trim();

            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new KnowledgeChunk(
                    ChunkId: BuildStableChunkId(document.KnowledgeDocumentId, document.Version, sequence),
                    KnowledgeDocumentId: document.KnowledgeDocumentId,
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    Title: document.Title,
                    DocumentType: document.DocumentType,
                    Version: document.Version,
                    Sequence: sequence,
                    Content: content,
                    SourceUri: document.SourceUri,
                    SourceUpdatedAt: document.UpdatedAt));
            }

            if (start + length >= normalized.Length)
            {
                break;
            }

            start += _options.ChunkSizeCharacters - _options.ChunkOverlapCharacters;
            sequence++;
        }

        return chunks;
    }

    private static string NormalizeContent(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string BuildStableChunkId(string knowledgeDocumentId, string version, int sequence)
    {
        var input = $"{knowledgeDocumentId}:{version}:{sequence}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### 10.2 Deterministic sensitivity classifier

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed class KnowledgeSensitivityClassifier : IKnowledgeSensitivityClassifier
{
    public Task<KnowledgeSensitivityResult> ClassifyAsync(
        KnowledgeDocumentForIngestion document,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();

        if (!document.IsApprovedForAiRetrieval)
        {
            reasons.Add("Document is not approved for AI retrieval.");
        }

        if (!string.Equals(document.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Document approval status is not approved.");
        }

        var label = document.SensitivityLabel?.Trim().ToLowerInvariant() ?? KnowledgeSensitivityClassificationValues.Unknown;

        var blocked = label is KnowledgeSensitivityClassificationValues.CustomerConfidential
            or KnowledgeSensitivityClassificationValues.StaffOnly
            or KnowledgeSensitivityClassificationValues.ContainsSecrets
            or KnowledgeSensitivityClassificationValues.ContainsUnapprovedPii
            or KnowledgeSensitivityClassificationValues.Unknown;

        if (blocked)
        {
            reasons.Add($"Sensitivity label '{label}' is not approved for AI retrieval.");
        }

        var approved = reasons.Count == 0 && label == KnowledgeSensitivityClassificationValues.Approved;

        return Task.FromResult(new KnowledgeSensitivityResult(
            ApprovedForAiRetrieval: approved,
            Classification: label,
            Reasons: reasons));
    }
}
```

### 10.3 Knowledge ingestion service

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Application.Knowledge;

internal sealed class KnowledgeIngestionService : IKnowledgeIngestionService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IProviderHealthCheck _providerHealthCheck;
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IKnowledgeSensitivityClassifier _sensitivityClassifier;
    private readonly IKnowledgeChunker _chunker;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IKnowledgeSearchIndexWriter _indexWriter;
    private readonly IAuditWriter _auditWriter;
    private readonly KnowledgeIndexingOptions _options;
    private readonly ILogger<KnowledgeIngestionService> _logger;

    public KnowledgeIngestionService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IProviderHealthCheck providerHealthCheck,
        IKnowledgeDocumentRepository documentRepository,
        IKnowledgeSensitivityClassifier sensitivityClassifier,
        IKnowledgeChunker chunker,
        IEmbeddingProvider embeddingProvider,
        IKnowledgeSearchIndexWriter indexWriter,
        IAuditWriter auditWriter,
        KnowledgeIndexingOptions options,
        ILogger<KnowledgeIngestionService> logger)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _providerHealthCheck = providerHealthCheck;
        _documentRepository = documentRepository;
        _sensitivityClassifier = sensitivityClassifier;
        _chunker = chunker;
        _embeddingProvider = embeddingProvider;
        _indexWriter = indexWriter;
        _auditWriter = auditWriter;
        _options = options;
        _logger = logger;
    }

    public async Task<KnowledgeIngestionResult> IngestDocumentAsync(
        string knowledgeDocumentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(knowledgeDocumentId))
        {
            throw new ValidationException("knowledgeDocumentId is required.");
        }

        ValidateOptions();

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.KnowledgeManage, cancellationToken);

        var document = await _documentRepository.GetForIngestionAsync(
            context.TenantId,
            context.WorkspaceId,
            knowledgeDocumentId,
            cancellationToken);

        if (document is null)
        {
            throw new NotFoundOrNotVisibleException("Knowledge document was not found or is not visible.");
        }

        var startedAt = DateTimeOffset.UtcNow;

        await _documentRepository.MarkIndexingAsync(
            context.TenantId,
            context.WorkspaceId,
            document.KnowledgeDocumentId,
            startedAt,
            context.CorrelationId,
            cancellationToken);

        await WriteAuditAsync(
            context,
            document,
            action: "indexing_started",
            summary: SafeSummary(document, "Knowledge document indexing started."),
            cancellationToken);

        var sensitivity = await _sensitivityClassifier.ClassifyAsync(document, cancellationToken);
        if (!sensitivity.ApprovedForAiRetrieval)
        {
            var safeReason = BuildSafeReason(sensitivity.Reasons);

            await _documentRepository.MarkIndexingBlockedAsync(
                context.TenantId,
                context.WorkspaceId,
                document.KnowledgeDocumentId,
                safeReason,
                DateTimeOffset.UtcNow,
                context.CorrelationId,
                cancellationToken);

            await WriteAuditAsync(
                context,
                document,
                action: "indexing_blocked",
                summary: SafeSummary(document, $"Knowledge document indexing blocked. Reason: {safeReason}"),
                cancellationToken);

            throw new WorkflowConflictException("Knowledge document is not approved for AI retrieval and cannot be indexed.");
        }

        await EnsureProviderConnectedAsync(ProviderCodes.AzureAiSearch, "Azure AI Search", cancellationToken);
        await EnsureProviderConnectedAsync(ProviderCodes.AzureOpenAi, "Azure OpenAI", cancellationToken);

        try
        {
            var chunks = _chunker.Chunk(document, context);
            if (chunks.Count == 0)
            {
                throw new ValidationException("Knowledge document produced no indexable chunks.");
            }

            var embeddings = await _embeddingProvider.GenerateEmbeddingsAsync(
                new EmbeddingRequest(
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    DeploymentName: _options.AzureOpenAiEmbeddingDeployment,
                    Inputs: chunks.Select(c => c.Content).ToList(),
                    CorrelationId: context.CorrelationId),
                cancellationToken);

            if (embeddings.Vectors.Count != chunks.Count)
            {
                throw new ProviderUnavailableException("Embedding provider returned an unexpected number of vectors.");
            }

            var indexDocuments = BuildIndexDocuments(chunks, embeddings);

            await _indexWriter.UpsertChunksAsync(
                context.TenantId,
                context.WorkspaceId,
                _options.AzureAiSearchIndexName,
                indexDocuments,
                cancellationToken);

            var indexedAt = DateTimeOffset.UtcNow;

            await _documentRepository.MarkIndexedAsync(
                context.TenantId,
                context.WorkspaceId,
                document.KnowledgeDocumentId,
                chunks.Count,
                indexedAt,
                context.CorrelationId,
                cancellationToken);

            await WriteAuditAsync(
                context,
                document,
                action: "indexing_completed",
                summary: SafeSummary(document, $"Knowledge document indexed. Chunk count: {chunks.Count}."),
                cancellationToken);

            return new KnowledgeIngestionResult(
                KnowledgeDocumentId: document.KnowledgeDocumentId,
                IndexStatus: KnowledgeIndexStatusValues.Indexed,
                ChunkCount: chunks.Count,
                IndexedAt: indexedAt.ToString("O"),
                FailureReason: null);
        }
        catch (Exception ex) when (ex is ProviderNotConfiguredException or ProviderUnavailableException or ValidationException)
        {
            var safeFailure = SafeFailureReason(ex);

            await _documentRepository.MarkIndexFailedAsync(
                context.TenantId,
                context.WorkspaceId,
                document.KnowledgeDocumentId,
                safeFailure,
                DateTimeOffset.UtcNow,
                context.CorrelationId,
                cancellationToken);

            await WriteAuditAsync(
                context,
                document,
                action: "indexing_failed",
                summary: SafeSummary(document, $"Knowledge document indexing failed. Reason: {safeFailure}"),
                cancellationToken);

            _logger.LogWarning(ex,
                "Knowledge document indexing failed. KnowledgeDocumentId: {KnowledgeDocumentId}. CorrelationId: {CorrelationId}",
                document.KnowledgeDocumentId,
                context.CorrelationId);

            throw;
        }
    }

    private async Task EnsureProviderConnectedAsync(
        string providerCode,
        string displayName,
        CancellationToken cancellationToken)
    {
        var health = await _providerHealthCheck.CheckAsync(providerCode, cancellationToken);

        if (health.Status == ProviderStatus.NotConfigured)
        {
            throw new ProviderNotConfiguredException($"{displayName} is not configured for knowledge indexing.");
        }

        if (health.Status == ProviderStatus.Unavailable)
        {
            throw new ProviderUnavailableException($"{displayName} is currently unavailable for knowledge indexing.");
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.AzureAiSearchIndexName))
        {
            throw new ProviderNotConfiguredException("Azure AI Search index name is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AzureOpenAiEmbeddingDeployment))
        {
            throw new ProviderNotConfiguredException("Azure OpenAI embedding deployment is not configured.");
        }
    }

    private static IReadOnlyList<KnowledgeSearchIndexDocument> BuildIndexDocuments(
        IReadOnlyList<KnowledgeChunk> chunks,
        EmbeddingResult embeddings)
    {
        var vectorsBySequence = embeddings.Vectors.ToDictionary(x => x.Sequence);
        var documents = new List<KnowledgeSearchIndexDocument>();

        foreach (var chunk in chunks)
        {
            if (!vectorsBySequence.TryGetValue(chunk.Sequence, out var vector))
            {
                throw new ProviderUnavailableException("Embedding provider response is missing a chunk vector.");
            }

            documents.Add(new KnowledgeSearchIndexDocument(
                Id: chunk.ChunkId,
                TenantId: chunk.TenantId,
                WorkspaceId: chunk.WorkspaceId,
                KnowledgeDocumentId: chunk.KnowledgeDocumentId,
                Title: chunk.Title,
                DocumentType: chunk.DocumentType,
                Version: chunk.Version,
                Sequence: chunk.Sequence,
                Content: chunk.Content,
                SourceUri: chunk.SourceUri,
                SourceUpdatedAt: chunk.SourceUpdatedAt,
                Embedding: vector.Values));
        }

        return documents;
    }

    private Task WriteAuditAsync(
        TenantContext context,
        KnowledgeDocumentForIngestion document,
        string action,
        string summary,
        CancellationToken cancellationToken)
    {
        return _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: "knowledge.document.indexed",
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "knowledge_document",
            EntityId: document.KnowledgeDocumentId,
            Summary: summary,
            Reason: null,
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), cancellationToken);
    }

    private static string SafeSummary(KnowledgeDocumentForIngestion document, string message)
    {
        var safeTitle = RedactAndTruncate(document.Title, 120);
        return $"{message} DocumentId: {document.KnowledgeDocumentId}. Title: {safeTitle}.";
    }

    private static string BuildSafeReason(IReadOnlyList<string> reasons)
    {
        if (reasons.Count == 0)
        {
            return "Document is not approved for AI retrieval.";
        }

        return RedactAndTruncate(string.Join(" ", reasons), 300);
    }

    private static string SafeFailureReason(Exception ex)
    {
        return ex switch
        {
            ProviderNotConfiguredException => "Required provider is not configured.",
            ProviderUnavailableException => "Required provider is unavailable.",
            ValidationException validation => RedactAndTruncate(validation.Message, 300),
            _ => "Knowledge indexing failed."
        };
    }

    private static string RedactAndTruncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
```

---

## 11. Background trigger contract

Large knowledge documents and re-indexing jobs should not rely on a synchronous HTTP request. This service method should be callable from both an admin/API path and a background Function.

Recommended worker location:

```text
src/InsuranceIntelligence.Functions/KnowledgeIndexing/KnowledgeIndexingFunction.cs
```

Queue message contract:

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public sealed record KnowledgeIngestionMessage(
    string TenantId,
    string? WorkspaceId,
    string KnowledgeDocumentId,
    string RequestedByUserId,
    string CorrelationId,
    DateTimeOffset RequestedAt);
```

Function behavior:

```text
1. Receive KnowledgeIngestionMessage from Azure Service Bus.
2. Resolve a server-side TenantContext from the message only after validating the message came from an internal trusted workflow.
3. Call IKnowledgeIngestionService.IngestDocumentAsync(knowledgeDocumentId, ct).
4. Let ProviderNotConfiguredException / ProviderUnavailableException surface to Function retry/dead-letter policy only when an explicit retry design exists.
5. For this pass, retries are out of scope; failures must update index_failed in the repository.
```

Important:

```text
Do not expose a public unauthenticated endpoint that accepts TenantId/WorkspaceId and indexes a document.
TenantId and WorkspaceId in the queue message are internal routing data, not user authority.
```

---

## 12. Dependency injection registration

Application registration:

```csharp
using InsuranceIntelligence.Application.Knowledge;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Application.DependencyInjection;

public static class KnowledgeApplicationRegistration
{
    public static IServiceCollection AddKnowledgeApplicationServices(
        this IServiceCollection services,
        KnowledgeIndexingOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IKnowledgeSensitivityClassifier, KnowledgeSensitivityClassifier>();
        services.AddScoped<IKnowledgeChunker, FixedSizeKnowledgeChunker>();
        services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();
        return services;
    }
}
```

Infrastructure registration expected later:

```csharp
services.AddScoped<IKnowledgeDocumentRepository, EfKnowledgeDocumentRepository>();
services.AddScoped<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>();
services.AddScoped<IKnowledgeSearchIndexWriter, AzureAiSearchKnowledgeIndexWriter>();
```

Protected runtime rule:

```text
In Staging, Production, and ManagedApplicationReviewer, these three registrations must be real provider-backed implementations. No LocalTest, Mock, InMemory, Simulated, Placeholder, Demo, or FallbackToMock implementation is allowed.
```

---

## 13. Tests to add

### 13.1 Permission enforcement

```text
KnowledgeIngestionServiceTests
- IngestDocumentAsync_RequiresKnowledgeManagePermission
```

Test intent:

```csharp
[Fact]
public async Task IngestDocumentAsync_RequiresKnowledgeManagePermission()
{
    var permissionService = new Mock<IPermissionService>();
    permissionService.Setup(x => x.EnsureAsync(Permissions.KnowledgeManage, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Permission denied."));

    var service = BuildService(permissionService: permissionService.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.IngestDocumentAsync("kb-1", CancellationToken.None));

    Mock.Get(DocumentRepository).Verify(x => x.GetForIngestionAsync(
        It.IsAny<string>(),
        It.IsAny<string?>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

### 13.2 Provider not configured — Azure AI Search

```text
- IngestDocumentAsync_ThrowsProviderNotConfigured_WhenAzureAiSearchNotConfigured
```

Expected behavior:

```text
- MarkIndexingAsync is called.
- Sensitivity gate is called first and passes.
- azure_ai_search health returns NotConfigured.
- MarkIndexFailedAsync is called with a safe failure reason.
- ProviderNotConfiguredException is thrown.
- No index writer call occurs.
```

### 13.3 Provider not configured — Azure OpenAI

```text
- IngestDocumentAsync_ThrowsProviderNotConfigured_WhenAzureOpenAiNotConfigured
```

Expected behavior:

```text
- azure_ai_search health passes.
- azure_openai health returns NotConfigured.
- MarkIndexFailedAsync is called.
- ProviderNotConfiguredException is thrown.
- No embedding or index writer call occurs.
```

### 13.4 Index status updated on success

```text
- IngestDocumentAsync_MarksIndexedAndWritesAuditOnSuccess
```

Must prove:

```text
- MarkIndexingAsync called before provider work.
- MarkIndexedAsync called with exact chunk count.
- Audit writer receives indexing_started and indexing_completed.
- Audit summary contains document ID and chunk count.
- Audit summary does not contain raw content or embeddings.
```

### 13.5 Index status updated on failure

```text
- IngestDocumentAsync_MarksIndexFailed_WhenEmbeddingProviderFails
- IngestDocumentAsync_MarksIndexFailed_WhenSearchIndexWriterFails
```

Must prove:

```text
- Failure status is persisted.
- Audit writer receives indexing_failed.
- Exception is surfaced; fake success is not returned.
- There is no silent retry in this pass.
```

### 13.6 Sensitivity gate blocks unapproved document

```text
- IngestDocumentAsync_BlocksCustomerConfidentialDocument
- IngestDocumentAsync_BlocksStaffOnlyDocument
- IngestDocumentAsync_BlocksDocumentContainingSecrets
- IngestDocumentAsync_BlocksUnknownClassification
```

Must prove:

```text
- MarkIndexingBlockedAsync is called.
- WorkflowConflictException is thrown.
- Embedding provider is not called.
- Search index writer is not called.
```

### 13.7 Audit does not contain raw content

```text
- IngestDocumentAsync_AuditDoesNotContainRawDocumentContent
- IngestDocumentAsync_AuditDoesNotContainEmbeddingValues
- IngestDocumentAsync_AuditDoesNotContainProviderPayloads
```

Test intent:

```csharp
[Fact]
public async Task IngestDocumentAsync_AuditDoesNotContainRawDocumentContent()
{
    var rawContent = "This is the raw policy wording body that must never appear in audit summaries.";
    var document = TestKnowledgeDocuments.Approved(content: rawContent);
    var auditWriter = new Mock<IAuditWriter>();

    var service = BuildService(document: document, auditWriter: auditWriter.Object);

    await service.IngestDocumentAsync(document.KnowledgeDocumentId, CancellationToken.None);

    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(evt => !evt.Summary.Contains(rawContent, StringComparison.Ordinal)),
        It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}
```

### 13.8 Chunker tests

```text
FixedSizeKnowledgeChunkerTests
- Chunk_CreatesStableSequentialChunkIds
- Chunk_UsesConfiguredOverlap
- Chunk_RejectsChunkSizeLessThanOrEqualToOverlap
- Chunk_RejectsEmptyContent
- Chunk_RejectsDocumentsExceedingMaxChunks
```

### 13.9 Contract test for ingestion result

```text
KnowledgeIngestionContractTests
- KnowledgeIngestionResult serializes to knowledgeDocumentId, indexStatus, chunkCount, indexedAt, failureReason
- indexStatus values are not_indexed, indexing, indexed, index_failed, indexing_blocked
- indexedAt uses ISO 8601 round-trip string
```

### 13.10 Protected runtime implementation test

```text
KnowledgeProviderRegistrationTests
- ProtectedRuntime_DoesNotRegisterMockEmbeddingProvider
- ProtectedRuntime_DoesNotRegisterMockSearchIndexWriter
- ProtectedRuntime_DoesNotRegisterInMemoryKnowledgeDocumentRepository
```

---

## 14. TODOs and confirmation points

| Area | Current decision | TODO / confirmation needed |
|---|---|---|
| Chunking | Fixed-size character chunks with overlap | Confirm exact chunk size, overlap, token-based splitting, heading/table-aware parsing, and language handling. |
| Sensitivity labels | Fail-closed deterministic labels | Confirm final data classification taxonomy and whether Microsoft Purview/content safety integration is required. |
| Re-indexing | Upsert by stable chunk ID | Confirm versioning strategy when a document is updated and old chunks must be deleted. |
| Large documents | Service can be called by Function | Confirm whether ingestion is always queue-triggered for files above a threshold. |
| Retry/backoff | Out of scope | Confirm retry policy, dead-letter policy, and idempotency strategy. |
| Idempotency | Stable chunk IDs reduce duplicates | Confirm idempotency key and write-once/run history requirements. |
| Index schema | Uses KnowledgeSearchIndexDocument | Confirm final Azure AI Search index fields, vector dimensions, analyzers, filters, semantic config, and scoring profiles. |
| Embedding deployment | Uses AzureOpenAiOptions.EmbeddingDeployment | Confirm deployment name, dimensions, model version, and token limits. |
| Source of truth | Existing IKnowledgeDocumentRepository | Confirm exact current repository method names and EF entity names. |
| Audit taxonomy | Uses knowledge.document.indexed | Confirm whether this should be added to canonical AuditEventTypeValues or kept as an action-specific event type. |
```

---

## 15. Completion report template

```text
Service implemented: IKnowledgeIngestionService.

Files to create or modify:
- src/InsuranceIntelligence.Application/Knowledge/IKnowledgeIngestionService.cs
- src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionService.cs
- src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionModels.cs
- src/InsuranceIntelligence.Application/Knowledge/IKnowledgeDocumentRepository.cs
- src/InsuranceIntelligence.Application/Knowledge/IKnowledgeSensitivityClassifier.cs
- src/InsuranceIntelligence.Application/Knowledge/IKnowledgeChunker.cs
- src/InsuranceIntelligence.Application/Knowledge/FixedSizeKnowledgeChunker.cs
- src/InsuranceIntelligence.Application/Knowledge/IEmbeddingProvider.cs
- src/InsuranceIntelligence.Application/Knowledge/IKnowledgeSearchIndexWriter.cs
- src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionMessage.cs
- src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeIngestionServiceTests.cs
- src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeChunkerTests.cs
- src/InsuranceIntelligence.Tests/Application/Knowledge/KnowledgeIngestionContractTests.cs

Logic implemented:
- Tenant/workspace-scoped knowledge ingestion.
- knowledge.manage permission enforcement.
- Sensitivity classification gate before indexing.
- azure_ai_search and azure_openai provider-health gates.
- Fixed-size chunking with overlap.
- Embedding generation through IEmbeddingProvider.
- Azure AI Search upsert through IKnowledgeSearchIndexWriter.
- Index status updates for indexing, indexed, index_failed, and indexing_blocked.
- Audit events for started/completed/failed/blocked indexing.

Security impact:
- Prevents unapproved, staff-only, customer-confidential, secret-containing, or unknown-classification documents from entering AI retrieval.
- Prevents raw document content and embeddings from being written to audit events.
- Prevents fake indexing success when Azure AI Search or Azure OpenAI is not configured.

Tenant/workspace isolation impact:
- Every document load, status update, embedding request, and index write carries TenantId and WorkspaceId.
- Search index documents include TenantId and WorkspaceId for retrieval-time filtering.

Authentication/authorization impact:
- Requires knowledge.manage before any ingestion or indexing action.
- Does not trust caller-supplied tenant/workspace IDs.

Provider impact:
- Requires azure_ai_search and azure_openai provider health to be Connected.
- Requires AzureAiSearchOptions.IndexName.
- Requires AzureOpenAiOptions.EmbeddingDeployment.

Data model or migration impact:
- Knowledge document records need IndexStatus, LastIndexedAt, LastIndexFailureReason, IndexedChunkCount, Version, ApprovalStatus, SensitivityLabel, and IsApprovedForAiRetrieval fields if not already present.
- Azure AI Search index must support tenant/workspace filters and vector fields.

AI/human approval impact:
- No consequential insurance decision is performed.
- The service only prepares approved knowledge for retrieval.
- Sensitive and unapproved documents are blocked before AI retrieval.

Tests added or required:
- Provider-not-configured tests for Azure AI Search and Azure OpenAI.
- Permission enforcement test.
- Index-status success and failure tests.
- Sensitivity-gate blocking tests.
- Audit-does-not-contain-raw-content test.
- Chunker behavior tests.
- Ingestion result contract test.
- Protected-runtime no-mock provider registration tests.

TODOs:
- Confirm exact chunking size and overlap.
- Confirm re-indexing/version deletion strategy.
- Confirm background queue trigger threshold.
- Confirm retry/backoff and dead-letter policy.
- Confirm idempotency key strategy.
- Confirm final Azure AI Search index schema and vector dimensions.
- Confirm final sensitivity taxonomy and approval workflow.

Missing dependencies:
- Concrete AzureOpenAiEmbeddingProvider.
- Concrete AzureAiSearchKnowledgeIndexWriter.
- Existing IKnowledgeDocumentRepository may need method additions for index status transitions.
- Knowledge indexing Function/Service Bus trigger if ingestion must be asynchronous for large documents.

No fake data confirmation:
- No mock, in-memory, simulated, placeholder, demo, fallback, fake indexing, or fake embedding path is introduced into deployable runtime.
- Provider failures mark index_failed or block indexing and surface real safe exceptions.

Recommended next step:
- Implement Infrastructure adapters for IEmbeddingProvider and IKnowledgeSearchIndexWriter, then add the KnowledgeIndexing Function trigger for asynchronous ingestion.
```

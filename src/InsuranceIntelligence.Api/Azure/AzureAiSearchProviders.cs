using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public sealed class AzureAiSearchKnowledgeIndexProvider(InfrastructureOptions options, ILogger<AzureAiSearchKnowledgeIndexProvider> logger) : IKnowledgeIndexProvider
{
    /// <summary>
    /// Chunks are section-sized (roughly 500-800 characters), so a top-5 keyword retrieval
    /// routinely missed the specific section a question was about — e.g. returning a policy's
    /// cover tiers but not its exclusions list, leaving the model to correctly but unhelpfully
    /// report that the sources did not contain the answer. Retrieving a wider set keeps recall
    /// high while staying comfortably inside the model's context budget at this chunk size.
    /// </summary>
    private const int MaxRetrievedChunks = 12;

    public async Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        // WO-17: unclassified (or never-eligible) sensitivity must never reach the search index,
        // not just be filtered at query time — fail closed at the point of publication.
        if (document.ApprovalStatus != KnowledgeDocumentStatus.Approved
            || document.Sensitivity == KnowledgeSensitivity.Unknown)
        {
            return;
        }

        var client = SearchClient();
        var searchDocuments = document.Chunks.Select(chunk => new
        {
            id = SafeKey(chunk.ChunkId),
            tenantId = chunk.Scope.TenantId,
            workspaceId = chunk.Scope.WorkspaceId,
            knowledgeDocId = chunk.KnowledgeDocumentId.ToString(),
            documentName = chunk.DocumentName,
            productLine = chunk.ProductLine,
            documentType = document.DocumentType.ToString(),
            version = chunk.Version,
            effectiveDate = document.EffectiveDate?.ToString("yyyy-MM-dd"),
            expiryDate = document.ExpiryDate?.ToString("yyyy-MM-dd"),
            approvalStatus = document.ApprovalStatus.ToString(),
            indexedStatus = KnowledgeIndexedStatus.Indexed.ToString(),
            chunkId = chunk.ChunkId,
            chunkText = chunk.Content,
            sourceUri = document.ObjectKey,
            pageNumber = 0,
            sectionTitle = chunk.SourceReference.ObjectKey
        });

        logger.LogInformation("Indexing approved knowledge document into Azure AI Search. Tenant={TenantId} Workspace={WorkspaceId} KnowledgeDocumentId={KnowledgeDocumentId}",
            document.Scope.TenantId, document.Scope.WorkspaceId, document.KnowledgeDocumentId);
        await client.MergeOrUploadDocumentsAsync(searchDocuments, cancellationToken: cancellationToken);
    }

    public async Task RemoveAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var client = SearchClient();
        var chunks = await SearchAsync(scope, knowledgeDocumentId.ToString(), null, cancellationToken);
        if (chunks.Count == 0)
        {
            return;
        }

        await client.DeleteDocumentsAsync("id", chunks.Select(chunk => SafeKey(chunk.ChunkId)), cancellationToken: cancellationToken);
    }

    public async Task RebuildAsync(TenantScope scope, IReadOnlyCollection<KnowledgeDocument> approvedDocuments, CancellationToken cancellationToken = default)
    {
        foreach (var document in approvedDocuments.Where(document =>
            document.Scope.Matches(scope)
            && document.ApprovalStatus == KnowledgeDocumentStatus.Approved
            && document.Sensitivity != KnowledgeSensitivity.Unknown))
        {
            await IndexAsync(document, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<KnowledgeChunk>> SearchAsync(TenantScope scope, string query, string? productLine, CancellationToken cancellationToken = default)
    {
        var client = SearchClient();
        var options = new SearchOptions
        {
            Filter = BuildSearchFilter(scope, productLine),
            Size = MaxRetrievedChunks
        };
        options.Select.Add("chunkId");
        options.Select.Add("knowledgeDocId");
        options.Select.Add("documentName");
        options.Select.Add("productLine");
        options.Select.Add("version");
        options.Select.Add("chunkText");
        options.Select.Add("sourceUri");

        var response = await client.SearchAsync<SearchDocument>(query, options, cancellationToken);
        var chunks = new List<KnowledgeChunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;
            var chunkId = document.TryGetValue("chunkId", out var chunkIdValue) ? chunkIdValue?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var knowledgeDocId = document.TryGetValue("knowledgeDocId", out var docIdValue) && Guid.TryParse(docIdValue?.ToString(), out var parsedDocId)
                ? parsedDocId
                : Guid.Empty;
            var documentName = Value(document, "documentName", "Azure AI Search result");
            var product = Value(document, "productLine", productLine ?? "Unknown");
            var version = Value(document, "version", "unknown");
            var content = Value(document, "chunkText", string.Empty);
            var sourceUri = Value(document, "sourceUri", string.Empty);
            chunks.Add(new KnowledgeChunk
            {
                ChunkId = chunkId,
                KnowledgeDocumentId = knowledgeDocId,
                Scope = scope,
                DocumentName = documentName,
                ProductLine = product,
                Version = version,
                ChunkIndex = chunks.Count,
                Content = content,
                SourceReference = new KnowledgeSourceReference(knowledgeDocId, chunkId, documentName, product, version, sourceUri)
            });
        }

        return chunks;
    }

    private SearchClient SearchClient()
    {
        if (AzureProviderSupport.Missing(options.AzureAiSearch.Endpoint) || AzureProviderSupport.Missing(options.AzureAiSearch.IndexName))
        {
            throw new InvalidOperationException("Azure AI Search is selected but AzureAiSearch:Endpoint or AzureAiSearch:IndexName is not configured.");
        }

        return new SearchClient(new Uri(options.AzureAiSearch.Endpoint!), options.AzureAiSearch.IndexName!, AzureProviderSupport.Credential());
    }

    public static string BuildSearchFilter(TenantScope scope, string? productLine)
    {
        var filter = $"tenantId eq '{Escape(scope.TenantId)}' and workspaceId eq '{Escape(scope.WorkspaceId)}' and approvalStatus eq 'Approved' and indexedStatus eq 'Indexed'";
        if (!string.IsNullOrWhiteSpace(productLine))
        {
            filter += $" and productLine eq '{Escape(productLine)}'";
        }

        return filter;
    }

    private static string Value(SearchDocument document, string key, string fallback) =>
        document.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;

    private static string SafeKey(string value) => value.Replace('/', '_').Replace('\\', '_');

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

public sealed class AzureAiSearchRetrievalProvider(
    IKnowledgeIndexProvider indexProvider,
    ISourceCitationFormatter citationFormatter,
    ILogger<AzureAiSearchRetrievalProvider> logger) : IRetrievalProvider
{
    public async Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default)
    {
        var chunks = await indexProvider.SearchAsync(question.Scope, question.Question, question.ProductLine, cancellationToken);
        var citations = chunks.Select(citationFormatter.Format).ToArray();
        logger.LogInformation("Retrieved {SourceCount} source chunks from Azure AI Search. Tenant={TenantId} Workspace={WorkspaceId}",
            chunks.Count, question.Scope.TenantId, question.Scope.WorkspaceId);
        return new RetrievalResult(question.Question, question.ProductLine, chunks, citations);
    }
}

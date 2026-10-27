using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProductWordingExtractionTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");
    private static readonly TenantScope OtherScope = new("tenant-b", "workspace-a");
    private const string Quote = "The policy covers accidental death up to UGX 5,000,000.";

    [Fact]
    public async Task ExtractionKeepsOnlyItemsWithExactSourceQuotes_AndStoresTheDraftInItsWorkspace()
    {
        var store = new Records();
        var audit = new Audit();
        var provider = new Chat("""{"items":[{"category":"cover","label":"Accidental death","value":"Up to UGX 5,000,000","sourceChunkId":"chunk-1","sourceQuote":"The policy covers accidental death up to UGX 5,000,000.","confidence":0.91},{"category":"premium","label":"Monthly premium","value":"UGX 10,000","sourceChunkId":"chunk-1","sourceQuote":"Premium is UGX 10,000 monthly.","confidence":0.99},{"category":"exclusion","label":"Suicide","value":"Excluded","sourceChunkId":"missing","sourceQuote":"Suicide is excluded.","confidence":0.8}]}""");
        var service = Service(Scope, provider, store, audit);

        var proposal = await service.ExtractAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        var item = Assert.Single(proposal.Items);
        Assert.Equal("cover", item.Category);
        Assert.Equal(Quote, item.SourceQuote);
        Assert.Equal(0.91m, item.Confidence);
        Assert.Contains("Do not use general knowledge", provider.LastRequest!.Prompt);
        Assert.Equal(1, await store.CountAsync(Scope, ProductWordingExtractionService.RecordKind));
        Assert.Equal(0, await store.CountAsync(OtherScope, ProductWordingExtractionService.RecordKind));
        Assert.Equal("product.wording_extraction.prepared", Assert.Single(audit.Events).ActionName);
    }

    [Fact]
    public async Task ADocumentFromAnotherWorkspaceCannotBeRead()
    {
        var service = Service(OtherScope, new Chat("""{"items":[]}"""), new Records(), new Audit());

        var error = await Assert.ThrowsAsync<ProductWordingExtractionException>(() => service.ExtractAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));

        Assert.Equal("not_found", error.Code);
    }

    [Fact]
    public async Task AConfiguredModelIsRequired_AndNoProposalIsStoredWhenItIsMissing()
    {
        var store = new Records();
        var service = Service(Scope, new MissingChat(), store, new Audit());

        var error = await Assert.ThrowsAsync<ProductWordingExtractionException>(() => service.ExtractAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));

        Assert.Equal("not_configured", error.Code);
        Assert.Equal(0, await store.CountAsync(Scope, ProductWordingExtractionService.RecordKind));
    }

    private static ProductWordingExtractionService Service(TenantScope scope, IChatCompletionProvider provider, IWorkspaceRecordStore records, IAuditWriter audit)
    {
        var documentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var docs = new InMemoryKnowledgeDocumentRepository();
        docs.SaveAsync(new KnowledgeDocument
        {
            KnowledgeDocumentId = documentId,
            Scope = Scope,
            DocumentName = "Loan protection wording",
            ProductLine = "Life",
            DocumentType = KnowledgeDocumentType.PolicyWording,
            Version = "1.0",
            OriginalFileName = "loan-protection.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            ObjectKey = "source-key",
            ApprovalStatus = KnowledgeDocumentStatus.Draft,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            Chunks = [new KnowledgeChunk
            {
                ChunkId = "chunk-1", KnowledgeDocumentId = documentId, Scope = Scope, DocumentName = "Loan protection wording",
                ProductLine = "Life", Version = "1.0", ChunkIndex = 0, Content = Quote,
                SourceReference = new KnowledgeSourceReference(documentId, "chunk-1", "Loan protection wording", "Life", "1.0", "source-key"),
            }],
            UploadedByUserId = "admin", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "upload",
        }).GetAwaiter().GetResult();
        var context = new Context(scope);
        return new ProductWordingExtractionService(context, new AllowPermissions(), docs, records, provider, audit, TimeProvider.System);
    }

    private sealed class Context(TenantScope scope) : ITenantContextAccessor
    {
        public TenantContext Current => new(scope.TenantId, scope.WorkspaceId, "admin-user", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class AllowPermissions : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class Chat(string content) : IChatCompletionProvider
    {
        public ChatCompletionRequest? LastRequest { get; private set; }
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ChatCompletionResult(content, "TestModel", "test", DateTimeOffset.UtcNow));
        }
    }

    private sealed class MissingChat : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            throw new CapabilityNotConfiguredException("Chat model unavailable.");
    }

    private sealed class Records : IWorkspaceRecordStore
    {
        private readonly Dictionary<(string Tenant, string Workspace, string Kind, string Key), WorkspaceRecord> records = [];
        public Task<WorkspaceRecord?> GetAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.GetValueOrDefault((scope.TenantId, scope.WorkspaceId, kind, key)));
        public Task SaveAsync(TenantScope scope, string kind, string key, string payloadJson, CancellationToken cancellationToken = default)
        {
            records[(scope.TenantId, scope.WorkspaceId, kind, key)] = new WorkspaceRecord(kind, key, payloadJson, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<WorkspaceRecord>> ListAsync(TenantScope scope, string kind, int take = 500, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceRecord>>(records.Where(pair => pair.Key.Tenant == scope.TenantId && pair.Key.Workspace == scope.WorkspaceId && pair.Key.Kind == kind).Select(pair => pair.Value).Take(take).ToArray());
        public Task DeleteAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default)
        {
            records.Remove((scope.TenantId, scope.WorkspaceId, kind, key));
            return Task.CompletedTask;
        }
        public Task<int> CountAsync(TenantScope scope, string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.Keys.Count(key => key.Tenant == scope.TenantId && key.Workspace == scope.WorkspaceId && key.Kind == kind));
    }

    private sealed class Audit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}

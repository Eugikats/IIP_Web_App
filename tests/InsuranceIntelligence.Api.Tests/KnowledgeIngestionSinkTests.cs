using System.Text;
using InsuranceIntelligence.Application.Knowledge;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Whatever brings a file into the Data Hub, upload or a connector, it arrives as a pending Draft with unknown
/// sensitivity. These pin that nothing here can approve, classify or index it.
/// </summary>
public sealed class KnowledgeIngestionSinkTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");

    [Fact]
    public async Task IngestedFile_IsAPendingDraftWithUnknownSensitivity_NotIndexed_AndAudited()
    {
        var repository = new InMemoryKnowledgeDocumentRepository();
        var audit = new InMemoryAuditRepository();
        var sink = new KnowledgeIngestionService(repository, new TextStorage(), audit);

        var document = await sink.IngestPendingAsync(Request("Motor cover pays for repairs.\n\nExcess applies."));

        Assert.Equal(KnowledgeDocumentStatus.Draft, document.ApprovalStatus);
        Assert.Equal(KnowledgeSensitivity.Unknown, document.Sensitivity);
        Assert.Equal(KnowledgeIndexedStatus.NotIndexed, document.IndexedStatus);
        Assert.NotEmpty(document.Chunks);
        Assert.NotNull(await repository.GetAsync(document.KnowledgeDocumentId, Scope));
        var events = await audit.ListAsync(Scope);
        Assert.Contains(events, e => e.ActionName == "knowledge.uploaded");
    }

    [Fact]
    public async Task IngestedFile_IsInvisibleToAnotherWorkspace()
    {
        var repository = new InMemoryKnowledgeDocumentRepository();
        var sink = new KnowledgeIngestionService(repository, new TextStorage(), new InMemoryAuditRepository());

        var document = await sink.IngestPendingAsync(Request("Anything."));

        Assert.Null(await repository.GetAsync(document.KnowledgeDocumentId, new TenantScope("tenant-b", "workspace-b")));
    }

    [Theory]
    [InlineData("notes.exe", 100, "Unsupported")]
    [InlineData("notes.txt", 0, "empty")]
    [InlineData("notes.txt", 11 * 1024 * 1024, "10 MB")]
    public void LimitsRefuseWhatTheDataHubCannotTake(string name, long length, string reasonContains) =>
        Assert.Contains(reasonContains, KnowledgeIngestionLimits.WhyNot(name, length), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void LimitsAcceptASupportedFile() =>
        Assert.Null(KnowledgeIngestionLimits.WhyNot("Policy wording.PDF", 2048));

    private static KnowledgeIngestionRequest Request(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new KnowledgeIngestionRequest(
            Scope, "connector-user", "Motor wording", "Motor", KnowledgeDocumentType.PolicyWording, "synced",
            null, null, "motor.txt", "text/plain", bytes.Length, new MemoryStream(bytes), "corr-1");
    }

    private sealed class TextStorage : IKnowledgeStorageProvider
    {
        public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return new KnowledgeStorageResult($"{request.Scope.TenantId}/k/{request.KnowledgeDocumentId}", request.ContentType, request.FileSizeBytes, text);
        }
    }
}

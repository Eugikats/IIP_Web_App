using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The claims list used to ask the database for each case's documents one case at a time. These pin the single-read
/// replacement: every asked-for case gets an entry, documents land under the right case, and another organisation's
/// documents never come back.
/// </summary>
public sealed class CaseListBatchReadTests
{
    private static readonly TenantScope Ours = new("tenant-a", "workspace-a");
    private static readonly TenantScope Theirs = new("tenant-b", "workspace-b");

    [Fact]
    public async Task DocumentsForManyCases_AreGroupedUnderTheirOwnCase_WithAnEmptyEntryForACaseWithNone()
    {
        await using var db = Db();
        var repository = new EfCaseDocumentRepository(db);
        var withTwo = Guid.NewGuid();
        var withOne = Guid.NewGuid();
        var withNone = Guid.NewGuid();
        await repository.SaveAsync(Document(Ours, withTwo, "a.pdf"));
        await repository.SaveAsync(Document(Ours, withTwo, "b.pdf"));
        await repository.SaveAsync(Document(Ours, withOne, "c.pdf"));

        var result = await repository.ListForCasesAsync([withTwo, withOne, withNone], Ours);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[withTwo].Count);
        Assert.Single(result[withOne]);
        Assert.Empty(result[withNone]);
    }

    [Fact]
    public async Task DocumentsForManyCases_NeverIncludeAnotherOrganisationsDocuments()
    {
        await using var db = Db();
        var repository = new EfCaseDocumentRepository(db);
        var sharedLookingCase = Guid.NewGuid();
        await repository.SaveAsync(Document(Ours, sharedLookingCase, "ours.pdf"));
        await repository.SaveAsync(Document(Theirs, sharedLookingCase, "theirs.pdf"));

        var result = await repository.ListForCasesAsync([sharedLookingCase], Ours);

        var only = Assert.Single(result[sharedLookingCase]);
        Assert.Equal("ours.pdf", only.OriginalFileName);
    }

    [Fact]
    public async Task DocumentsForManyCases_GivesAnEmptyAnswerForNoCases()
    {
        await using var db = Db();
        var result = await new EfCaseDocumentRepository(db).ListForCasesAsync([], Ours);
        Assert.Empty(result);
    }

    private static InsuranceIntelligenceDbContext Db() =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CaseDocument Document(TenantScope scope, Guid caseId, string fileName)
    {
        var now = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);
        return new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = caseId,
            Scope = scope,
            OriginalFileName = fileName,
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            ObjectKey = $"test/{Guid.NewGuid():N}",
            DocumentType = DocumentType.GarageEstimate,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(DocumentType.GarageEstimate, 0.9m, "test", now),
            Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "test", now, false, null, null),
            ValidationWarnings = [],
            UploadedByUserId = "claims-user-1",
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
    }
}

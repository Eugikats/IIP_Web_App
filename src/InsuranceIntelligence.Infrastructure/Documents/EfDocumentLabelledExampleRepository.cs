using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Documents;

/// <summary>Append-only labelled-data store for outcome K, layer 3. Never overwritten: each
/// correction is its own row, tenant-scoped, never queried or aggregated across tenants.</summary>
public sealed class EfDocumentLabelledExampleRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IDocumentLabelledExampleRepository
{
    public Task SaveAsync(DocumentLabelledExample example, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.DocumentLabelledExamples,
            example.ExampleId,
            example.Scope,
            Serialize(example),
            record => record.CaseId = example.CaseId,
            cancellationToken);

    public async Task<IReadOnlyCollection<DocumentLabelledExample>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.DocumentLabelledExamples.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records.Select(record => Deserialize<DocumentLabelledExample>(record.PayloadJson)).ToArray();
    }
}

using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Readings of a policy wording awaiting or past human review. Scoped to the workspace on
/// every read; a draft from another workspace is never returned, whatever its id.</summary>
public sealed class EfWordingDraftRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWordingDraftRepository
{
    public Task SaveAsync(WordingDraft draft, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.WordingDrafts, draft.DraftId, draft.Scope, Serialize(draft), record => record.ProductId = draft.ProductId, cancellationToken);

    public async Task<WordingDraft?> GetAsync(TenantScope scope, Guid draftId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.WordingDrafts.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        return record is null ? null : Deserialize<WordingDraft>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<WordingDraft>> ListForProductAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.WordingDrafts.AsNoTracking(), scope)
            .Where(item => item.ProductId == productId)
            .ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<WordingDraft>(record.PayloadJson)).ToArray();
    }
}

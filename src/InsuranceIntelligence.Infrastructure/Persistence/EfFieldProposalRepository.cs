using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Suggestions read from a claim's documents, scoped to the claim's workspace on every read.</summary>
public sealed class EfFieldProposalRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IFieldProposalRepository
{
    public Task SaveAsync(FieldProposal proposal, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.FieldProposals, proposal.ProposalId, proposal.Scope, Serialize(proposal), record => record.CaseId = proposal.CaseId, cancellationToken);

    public async Task<FieldProposal?> GetAsync(TenantScope scope, Guid caseId, Guid proposalId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.FieldProposals.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == proposalId && item.CaseId == caseId, cancellationToken);
        return record is null ? null : Deserialize<FieldProposal>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<FieldProposal>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.FieldProposals.AsNoTracking(), scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<FieldProposal>(record.PayloadJson)).ToArray();
    }
}

using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>The review score and recommendation prepared for a claim, newest last. Every read is
/// limited to the caller's workspace.</summary>
public sealed class EfClaimAssessmentRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimAssessmentRepository
{
    public Task SaveAsync(ClaimAssessment assessment, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ClaimAssessments,
            assessment.AssessmentId,
            assessment.Scope,
            Serialize(assessment),
            record =>
            {
                record.CaseId = assessment.CaseId;
                record.GeneratedAt = assessment.GeneratedAt;
            },
            cancellationToken);

    public async Task<ClaimAssessment?> GetLatestAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ClaimAssessments.AsNoTracking(), scope)
            .Where(item => item.CaseId == caseId)
            .ToListAsync(cancellationToken);
        var latest = records.OrderByDescending(item => item.GeneratedAt).FirstOrDefault();
        return latest is null ? null : Deserialize<ClaimAssessment>(latest.PayloadJson);
    }
}

using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Authenticity findings for stored pictures. Every read is limited to the caller's
/// workspace, including the search for a picture used before, which never looks across workspaces.</summary>
public sealed class EfAuthenticityReportRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAuthenticityReportRepository
{
    public Task SaveAsync(AuthenticityReport report, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AuthenticityReports,
            report.DocumentId,
            report.Scope,
            Serialize(report),
            record =>
            {
                record.CaseId = report.CaseId;
                record.PerceptualHash = report.PerceptualHash;
            },
            cancellationToken);

    public async Task<AuthenticityReport?> GetAsync(TenantScope scope, Guid caseId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AuthenticityReports.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == documentId && item.CaseId == caseId, cancellationToken);
        return record is null ? null : Deserialize<AuthenticityReport>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<AuthenticityReport>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.AuthenticityReports.AsNoTracking(), scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<AuthenticityReport>(record.PayloadJson)).ToArray();
    }

    public async Task<IReadOnlyList<AuthenticityReport>> ListWithHashAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.AuthenticityReports.AsNoTracking(), scope).Where(item => item.PerceptualHash != null).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<AuthenticityReport>(record.PayloadJson)).ToArray();
    }
}

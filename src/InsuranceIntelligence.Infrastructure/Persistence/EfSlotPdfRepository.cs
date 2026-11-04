using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>One current PDF per document slot of a claim, in the claim's workspace. Saving a slot's
/// PDF again replaces the record (the earlier PDF object stays in storage as history).</summary>
public sealed class EfSlotPdfRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ISlotPdfRepository
{
    public async Task<SlotPdf?> GetAsync(TenantScope scope, Guid caseId, string slotKey, CancellationToken cancellationToken = default)
    {
        var id = DeterministicId(scope, caseId, slotKey);
        var record = await Scoped(DbContext.SlotPdfs.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<SlotPdf>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<SlotPdf>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.SlotPdfs.AsNoTracking(), scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<SlotPdf>(record.PayloadJson)).ToArray();
    }

    public Task SaveAsync(SlotPdf pdf, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.SlotPdfs, DeterministicId(pdf.Scope, pdf.CaseId, pdf.SlotKey), pdf.Scope, Serialize(pdf), record => record.CaseId = pdf.CaseId, cancellationToken);

    public async Task RemoveAsync(TenantScope scope, Guid caseId, string slotKey, CancellationToken cancellationToken = default)
    {
        var id = DeterministicId(scope, caseId, slotKey);
        var record = await Scoped(DbContext.SlotPdfs, scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is not null)
        {
            DbContext.SlotPdfs.Remove(record);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static Guid DeterministicId(TenantScope scope, Guid caseId, string slotKey) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"slot-pdf:{scope.TenantId}:{scope.WorkspaceId}:{caseId:N}:{slotKey.ToLowerInvariant()}")));
}

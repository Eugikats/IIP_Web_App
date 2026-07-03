using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class AuditWriterEf(
    InsuranceIntelligenceDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor)
    : JsonRepositoryBase(dbContext), IAuditWriter
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var currentScope = tenantContextAccessor.Current.Scope;
        if (!auditEvent.Scope.Matches(currentScope))
        {
            throw new AuditScopeException("Audit event scope does not match the current tenant workspace.");
        }

        return UpsertAsync(
            DbContext.AuditEvents,
            auditEvent.EventId,
            auditEvent.Scope,
            Serialize(auditEvent),
            record => record.CaseId = auditEvent.CaseId,
            cancellationToken);
    }
}

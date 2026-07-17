using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Core;

public interface IAuditEventRepository
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed class AuditScopeException(string message) : InvalidOperationException(message);

public sealed class AuditWriter(
    ITenantContextAccessor tenantContextAccessor,
    IAuditEventRepository auditEventRepository,
    IOperationalTelemetry operationalTelemetry) : IAuditWriter
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var currentScope = tenantContextAccessor.Current.Scope;
        if (!auditEvent.Scope.Matches(currentScope))
        {
            throw new AuditScopeException("Audit event scope does not match the current tenant workspace.");
        }

        await auditEventRepository.WriteAsync(auditEvent, cancellationToken);
        operationalTelemetry.TrackAuditEventCreated(auditEvent);
    }
}

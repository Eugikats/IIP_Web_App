using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Local;

public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly ConcurrentBag<AuditEvent> events = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var matches = events
            .Where(auditEvent => auditEvent.CaseId == caseId)
            .OrderBy(auditEvent => auditEvent.Timestamp)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AuditEvent>>(matches);
    }

    public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = events
            .Where(auditEvent => auditEvent.Scope.Matches(scope))
            .OrderByDescending(auditEvent => auditEvent.Timestamp)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AuditEvent>>(matches);
    }
}

using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.Core;

public interface IOperationalTelemetry
{
    void TrackAuditEventCreated(AuditEvent auditEvent);

    void TrackAgentRegistryRun(string tenantId, string workspaceId, string agentKey, string action, string result);
}

public sealed class NoOpOperationalTelemetry : IOperationalTelemetry
{
    public static readonly NoOpOperationalTelemetry Instance = new();

    private NoOpOperationalTelemetry() { }

    public void TrackAuditEventCreated(AuditEvent auditEvent) { }

    public void TrackAgentRegistryRun(string tenantId, string workspaceId, string agentKey, string action, string result) { }
}


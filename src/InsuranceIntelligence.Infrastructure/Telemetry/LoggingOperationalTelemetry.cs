using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Audit;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Telemetry;

public sealed class LoggingOperationalTelemetry(ILogger<LoggingOperationalTelemetry> logger) : IOperationalTelemetry
{
    public void TrackAuditEventCreated(AuditEvent auditEvent)
    {
        logger.LogInformation(
            "IIP telemetry event {TelemetryEvent} tenant {TenantId} workspace {WorkspaceId} action {ActionName} actorType {ActorType} targetCase {CaseId} targetDocument {DocumentId} correlation {CorrelationId}",
            "AuditEventCreated",
            auditEvent.Scope.TenantId,
            auditEvent.Scope.WorkspaceId,
            auditEvent.ActionName,
            auditEvent.ActorType.ToString(),
            auditEvent.CaseId,
            auditEvent.DocumentId,
            auditEvent.CorrelationId);
    }

    public void TrackAgentRegistryRun(string tenantId, string workspaceId, string agentKey, string action, string result)
    {
        logger.LogInformation(
            "IIP telemetry event {TelemetryEvent} tenant {TenantId} workspace {WorkspaceId} agent {AgentKey} action {Action} result {Result}",
            "AgentRegistryRun",
            tenantId,
            workspaceId,
            agentKey,
            action,
            result);
    }
}


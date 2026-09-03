using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>knowledge.search — must return citations and source IDs (docs §9). Requires the
/// "query" parameter; an empty query returns a denied result rather than searching blindly.</summary>
public sealed class KnowledgeSearchTool(IRetrievalProvider retrievalProvider, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "knowledge.search",
        ["command-center", "policy-intelligence", "knowledge-assistant", "claims-summary", "underwriting-intelligence"],
        [Permission.ViewCommandCenter],
        RequiredProviderCode: "azure_ai_search",
        Guardrail: "Must return citations and source IDs; never fabricate a knowledge answer.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var query = invocation.Parameter("query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return Denied("The \"query\" parameter is required.");
        }

        var question = new AssistantQuestion(
            invocation.Scope,
            CaseId: null,
            query.Trim(),
            invocation.Parameter("productLine"),
            "InternalUser",
            invocation.UserId ?? "unknown",
            invocation.Parameter("correlationId") ?? Guid.NewGuid().ToString("N"));

        var result = await retrievalProvider.RetrieveAsync(question, cancellationToken);
        var evidenceRefs = result.Citations.Select(c => $"knowledge:{c.KnowledgeDocumentId}/{c.ChunkId}").ToArray();
        return Ok(result, evidenceRefs);
    }
}

/// <summary>audit.read — sensitive; scope by tenant/workspace/entity (docs §9).</summary>
public sealed class AuditReadTool(IAuditRepository auditRepository, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "audit.read",
        ["risk-fraud-signal", "report-insight", "claims-summary"],
        [Permission.ViewAuditLog],
        RequiredProviderCode: null,
        Guardrail: "Sensitive; scope by tenant/workspace/entity.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var events = await auditRepository.ListAsync(invocation.Scope, cancellationToken);
        return Ok(events, events.Select(e => $"audit:{e.EventId}").ToArray());
    }
}

/// <summary>provider_health.read — shows connected/degraded/not_configured/error; no secrets
/// (docs §9). Requires the "providerCode" parameter naming which provider to check.</summary>
public sealed class ProviderHealthReadTool(IProviderHealthCheck providerHealthCheck, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "provider_health.read",
        ["command-center", "document-checker", "knowledge-assistant", "report-insight"],
        [Permission.ViewCommandCenter],
        RequiredProviderCode: null,
        Guardrail: "Shows connected/degraded/not_configured/error; no secrets.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var providerCode = invocation.Parameter("providerCode");
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return Denied("The \"providerCode\" parameter is required.");
        }

        var status = await providerHealthCheck.CheckAsync(providerCode.Trim(), cancellationToken);
        return Ok(status, [$"provider:{status.ProviderCode}"]);
    }
}

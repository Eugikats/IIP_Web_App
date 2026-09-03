using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>claims.attention.read — full claim snapshot list (docs §9).</summary>
public sealed class ClaimsAttentionReadTool(ClaimSnapshotLoader loader, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "claims.attention.read",
        ["command-center", "claims-summary", "routing"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "Applies role, assignment, branch/product filters via the caller's tenant scope.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var snapshots = await loader.LoadAsync(invocation.Scope, DateTimeOffset.UtcNow, cancellationToken);
        return Ok(snapshots, snapshots.Select(s => $"case:{s.CaseId}").ToArray());
    }
}

/// <summary>claims.sla.read — SLA-focused projection; values are backend-calculated (docs §9).</summary>
public sealed class ClaimsSlaReadTool(ClaimSnapshotLoader loader, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "claims.sla.read",
        ["command-center", "routing", "report-insight"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "SLA values must be backend-calculated, never inferred.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var snapshots = await loader.LoadAsync(invocation.Scope, DateTimeOffset.UtcNow, cancellationToken);
        var atRiskOrBreached = snapshots.Where(s => s.SlaStatus is SlaStatus.AtRisk or SlaStatus.Breached).ToArray();
        return Ok(atRiskOrBreached, atRiskOrBreached.Select(s => $"case:{s.CaseId}").ToArray());
    }
}

/// <summary>documents.gaps.read — uses the configured claim-type/product checklist only (docs §9).</summary>
public sealed class DocumentsGapsReadTool(ClaimSnapshotLoader loader, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "documents.gaps.read",
        ["command-center", "document-checker"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "Uses the configured claim-type/product checklist only.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var snapshots = await loader.LoadAsync(invocation.Scope, DateTimeOffset.UtcNow, cancellationToken);
        var withGaps = snapshots.Where(s => s.MissingDocuments.Count > 0).ToArray();
        return Ok(withGaps, withGaps.Select(s => $"case:{s.CaseId}").ToArray());
    }
}

/// <summary>approvals.queue.read — only approvals visible to the current user/role (docs §9).</summary>
public sealed class ApprovalsQueueReadTool(
    InsuranceIntelligence.Domain.Workflow.IApprovalRepository approvalRepository,
    IPermissionService permissionService) : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "approvals.queue.read",
        ["command-center"],
        [Permission.ApproveSettlementWorkflowStep],
        RequiredProviderCode: null,
        Guardrail: "Only returns approvals visible to current user/role.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var approvals = (await approvalRepository.ListAsync(invocation.Scope, cancellationToken))
            .Where(a => a.Status == InsuranceIntelligence.Domain.Workflow.ApprovalStatus.Pending)
            .ToArray();
        return Ok(approvals, approvals.Select(a => $"approval:{a.ApprovalId}").ToArray());
    }
}

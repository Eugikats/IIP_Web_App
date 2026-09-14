using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>claim.read, single claim, tenant/workspace filter mandatory (docs §9). Requires the
/// "caseId" parameter. Added for WO-10 (claims-summary), the first real consumer.</summary>
public sealed class ClaimReadTool(ICaseRepository caseRepository, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "claim.read",
        ["claims-summary", "risk-fraud-signal", "routing", "customer-update-drafting"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "Tenant/workspace filter mandatory.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var caseIdParam = invocation.Parameter("caseId");
        if (!Guid.TryParse(caseIdParam, out var caseId))
        {
            return Denied("The \"caseId\" parameter (a GUID) is required.");
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(invocation.Scope))
        {
            return Denied("Case was not found for the requested tenant workspace.");
        }

        return Ok(claimCase, [$"case:{claimCase.CaseId}"]);
    }
}

/// <summary>document.read, no cross-claim access unless authorized (docs §9). Requires the
/// "caseId" parameter.</summary>
public sealed class DocumentReadTool(ICaseDocumentRepository documentRepository, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "document.read",
        ["claims-summary", "document-checker", "risk-fraud-signal", "underwriting-intelligence"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "No cross-claim access unless authorized.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var caseIdParam = invocation.Parameter("caseId");
        if (!Guid.TryParse(caseIdParam, out var caseId))
        {
            return Denied("The \"caseId\" parameter (a GUID) is required.");
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, invocation.Scope, cancellationToken);
        return Ok(documents, documents.Select(d => $"document:{d.DocumentId}").ToArray());
    }
}

/// <summary>policy.read, do not infer policy terms from memory (docs §9). Requires the
/// "policyNumber" parameter.</summary>
public sealed class PolicyReadTool(IPolicyRepository policyRepository, IPermissionService permissionService)
    : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "policy.read",
        ["policy-intelligence", "claims-summary", "underwriting-intelligence"],
        [Permission.ViewAssignedClaims, Permission.ViewAllClaims],
        RequiredProviderCode: null,
        Guardrail: "Do not infer policy terms from memory.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var policyNumber = invocation.Parameter("policyNumber");
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return Denied("The \"policyNumber\" parameter is required.");
        }

        // IPolicyRepository has no lookup by PolicyNumber (only by PolicyId), filter the
        // tenant-scoped list. Acceptable at current data volumes; revisit if this becomes a
        // hot path once real customer books are loaded.
        var policies = await policyRepository.ListAsync(invocation.Scope, cancellationToken);
        var match = policies.FirstOrDefault(p => string.Equals(p.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? Denied($"No policy found for policy number \"{policyNumber}\" in this workspace.")
            : Ok(match, [$"policy:{match.PolicyId}"]);
    }
}

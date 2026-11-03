namespace InsuranceIntelligence.Domain.Administration;

public static class RolePermissionMap
{
    private static readonly IReadOnlyDictionary<PlatformRole, Permission[]> Map =
        new Dictionary<PlatformRole, Permission[]>
        {
            // Tenant administrators cannot operate the publisher's cross-tenant queue.
            [PlatformRole.Admin] = Enum.GetValues<Permission>()
                .Where(permission => permission != Permission.ManageTenantOnboarding).ToArray(),
            [PlatformRole.ITAdmin] =
            [
                Permission.ViewCommandCenter,
                Permission.ViewAuditLog,
                Permission.ExportEvidencePack,
                Permission.ConfigureIntegrations,
                Permission.ManageTenantSettings,
                Permission.ManageAgents,
                Permission.ViewAgentWorkflows,
                Permission.RunAgentWorkflows,
                Permission.ManageAgentWorkflows,
                Permission.ReviewAgentWorkItems,
                Permission.ViewBilling,
                Permission.ManageProducts
            ],
            [PlatformRole.ClaimsOfficer] =
            [
                Permission.ViewCommandCenter,
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAssignedClaims,
                Permission.ViewAiOutputs,
                Permission.ManageCustomerCommunications,
                Permission.SendCustomerUpdate,
                Permission.ViewChannels,
                Permission.ViewCustomers,
                Permission.ViewAgentWorkflows,
                Permission.RunAgentWorkflows,
                Permission.ReviewAgentWorkItems
            ],
            [PlatformRole.ClaimsSupervisor] =
            [
                Permission.ViewCommandCenter,
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAllClaims,
                Permission.ViewAssignedClaims,
                Permission.ApproveSettlementWorkflowStep,
                Permission.ViewAuditLog,
                Permission.ExportEvidencePack,
                Permission.ViewAiOutputs,
                Permission.OverrideAiRecommendation,
                Permission.ManageCustomerCommunications,
                Permission.SendCustomerUpdate,
                Permission.ViewChannels,
                Permission.ViewCustomers,
                Permission.ViewFraudSignals,
                Permission.ViewUnderwriting,
                Permission.ViewAgentWorkflows,
                Permission.RunAgentWorkflows,
                Permission.ReviewAgentWorkItems,
                Permission.ManageProducts
            ],
            // Customer service handles claim intake, customer communication and sales leads. It
            // holds no administrative permission (products, tenant settings, onboarding, billing,
            // agents, integrations, audit or evidence export) and cannot approve settlements.
            [PlatformRole.CustomerService] =
            [
                Permission.ViewCommandCenter,
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAssignedClaims,
                Permission.ViewAiOutputs,
                Permission.ManageCustomerCommunications,
                Permission.SendCustomerUpdate,
                Permission.ViewChannels,
                Permission.ViewCustomers,
                Permission.ViewSalesLeads,
                Permission.ManageSalesLeads,
                Permission.ViewAgentWorkflows,
                Permission.RunAgentWorkflows,
                Permission.ReviewAgentWorkItems
            ],
            // Demo-readiness pass: a broker submits claims on behalf of a client but should not
            // see ongoing claim status/detail (ViewAssignedClaims removed).
            [PlatformRole.Broker] =
            [
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAiOutputs
            ],
            [PlatformRole.Compliance] =
            [
                Permission.ViewCommandCenter,
                Permission.ViewAllClaims,
                Permission.ViewAuditLog,
                Permission.ExportEvidencePack,
                Permission.ManageKnowledgeBase,
                Permission.ViewAiOutputs,
                Permission.ApproveSettlementWorkflowStep,
                Permission.OverrideAiRecommendation,
                Permission.ViewCustomers,
                Permission.ViewFraudSignals,
                Permission.ViewUnderwriting,
                Permission.ViewAgentWorkflows,
                Permission.RunAgentWorkflows,
                Permission.ReviewAgentWorkItems
            ],
            [PlatformRole.Client] =
            [
                Permission.ViewClientPortal
            ],
            [PlatformRole.KnowledgeManager] =
            [
                Permission.ManageKnowledgeBase,
                Permission.ViewAiOutputs
            ],
            // An insurer's own system: it can file and read claims and take the claims register, and
            // nothing else. It cannot change settings, people, connections or other keys.
            [PlatformRole.Integration] =
            [
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAllClaims,
                Permission.ViewCustomers,
                Permission.ExportEvidencePack
            ],
            [PlatformRole.PublisherSupport] =
            [
                Permission.ViewCommandCenter,
                Permission.ViewAuditLog,
                Permission.ManageTenantOnboarding
            ]
        };

    public static IReadOnlyCollection<Permission> ForRole(PlatformRole role) => Map[role];

    public static bool HasPermission(PlatformRole role, Permission permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}

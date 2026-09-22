namespace InsuranceIntelligence.Domain.Administration;

public static class RolePermissionMap
{
    private static readonly IReadOnlyDictionary<PlatformRole, Permission[]> Map =
        new Dictionary<PlatformRole, Permission[]>
        {
            [PlatformRole.Admin] = Enum.GetValues<Permission>(),
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
            [PlatformRole.Broker] =
            [
                Permission.CreateClaim,
                Permission.UploadDocument,
                Permission.ViewAssignedClaims,
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

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
                Permission.ConfigureIntegrations,
                Permission.ManageTenantSettings,
                Permission.ManageAgents,
                Permission.ViewBilling
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
                Permission.ViewCustomers
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
                Permission.ViewAiOutputs,
                Permission.OverrideAiRecommendation,
                Permission.ManageCustomerCommunications,
                Permission.SendCustomerUpdate,
                Permission.ViewChannels,
                Permission.ViewCustomers,
                Permission.ViewFraudSignals,
                Permission.ViewUnderwriting
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
                Permission.ManageSalesLeads
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
                Permission.ManageKnowledgeBase,
                Permission.ViewAiOutputs,
                Permission.ApproveSettlementWorkflowStep,
                Permission.OverrideAiRecommendation,
                Permission.ViewCustomers,
                Permission.ViewFraudSignals,
                Permission.ViewUnderwriting
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

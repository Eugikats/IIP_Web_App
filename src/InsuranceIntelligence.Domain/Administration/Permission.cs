namespace InsuranceIntelligence.Domain.Administration;

public enum Permission
{
    ViewCommandCenter,
    CreateClaim,
    UploadDocument,
    ViewAllClaims,
    ViewAssignedClaims,
    ApproveSettlementWorkflowStep,
    ViewAuditLog,
    /// <summary>
    /// Consequential evidence-pack export (distinct from read-only audit viewing).
    /// Publisher/support read paths must not silently inherit this permission.
    /// </summary>
    ExportEvidencePack,
    ManageKnowledgeBase,
    ConfigureIntegrations,
    ViewAiOutputs,
    OverrideAiRecommendation,
    ManageCustomerCommunications,
    SendCustomerUpdate,
    ManageTenantSettings,
    ViewClientPortal,
    ViewChannels,
    ViewCustomers,
    ViewSalesLeads,
    ManageSalesLeads,
    ViewFraudSignals,
    ViewUnderwriting,
    ManageAgents,
    ViewAgentWorkflows,
    RunAgentWorkflows,
    ManageAgentWorkflows,
    ReviewAgentWorkItems,
    ViewBilling,
    ManageTenantOnboarding
}

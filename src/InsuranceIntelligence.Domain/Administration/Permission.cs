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
    ManageTenantOnboarding,
    ManageProducts,

    /// <summary>Invite people, and manage the links between a broker and an insurer.</summary>
    ManageOrganisation,

    /// <summary>Open the organisation platform: workspaces, automations, the Intelligence Inbox, intents and platform reports.</summary>
    ViewPlatform,

    /// <summary>Create and change workspaces and the platform access mode. Not needed to use a workspace.</summary>
    ManagePlatform,

    /// <summary>Approve, reject, activate and resume an automation. An automation never runs until a person with this permission approves it.</summary>
    ApprovePlatformAutomations
}

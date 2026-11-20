namespace InsuranceIntelligence.Application.Platform;

public sealed record WorkspaceTemplate(
    string Key,
    string Name,
    string Department,
    string Category,
    string Purpose,
    IReadOnlyList<string> Kpis,
    IReadOnlyList<string> Connectors,
    IReadOnlyList<string> Reports,
    IReadOnlyList<string> Automations,
    bool IsLiveTemplate = false);

public sealed record AutomationField(string Key, string Label, string Kind, object Default, string? Unit = null, string? Help = null);

public sealed record AutomationTemplate(
    string Key,
    string Name,
    string Category,
    string Description,
    string Objective,
    string DefaultTrigger,
    string DefaultSchedule,
    IReadOnlyList<AutomationField> Fields,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> DataUsed,
    bool HasRunner);

public sealed record ConnectorDefinition(string Key, string Name, string Description);

/// <summary>
/// The templates an organisation builds workspaces and automations from. The Insurance Intelligence template is the
/// existing IIP workspace (claims, acquisition and the assistant); every other workspace starts awaiting a data
/// connection. A template is a suggestion, never an approval: nothing runs until a person tests and approves it.
/// </summary>
public static class PlatformTemplates
{
    public const string InsuranceIntelligence = "insurance_intelligence";

    public static readonly IReadOnlyList<ConnectorDefinition> Connectors =
    [
        new("iip_core", "IIP claims and policy data", "Claims, policies, customers and approvals already in IIP."),
        new("channels", "WhatsApp and email", "Customer conversations and leads from IIP channels."),
        new("knowledge", "Knowledge library", "Approved policy wording, procedures and product documents in IIP."),
        new("policy_admin", "Policy administration system", "Policies, premiums and endorsements from the insurer's own system."),
        new("core_banking", "Core banking system", "Bank customers and accounts, for bancassurance."),
        new("crm", "Customer relationship system", "Accounts, leads and interactions from a CRM."),
        new("microsoft_365", "Microsoft 365", "Outlook, Teams, SharePoint and Excel files."),
        new("google_workspace", "Google Workspace", "Gmail, Drive and Sheets."),
        new("finance_system", "Finance and general ledger", "Premium production, receivables and claims paid.")
    ];

    public static readonly IReadOnlyList<WorkspaceTemplate> Workspaces =
    [
        new(InsuranceIntelligence, "Insurance Intelligence", "Insurance", "Core",
            "Claims, acquisition and the Command Center for the whole insurance team.",
            ["Open claims", "Claims past their deadline", "Claims missing documents", "Policies expiring soon", "Approvals waiting"],
            ["iip_core", "channels", "knowledge"],
            ["Operations summary", "Claims register"],
            ["claims_sla", "claims_missing_documents", "daily_briefing"], IsLiveTemplate: true),
        new("claims_management", "Claims Management", "Claims", "Operations",
            "Claims intake and the case lifecycle from first notice to settlement.",
            ["Open claims", "Average completeness", "Claims past their deadline", "Unassigned claims"],
            ["iip_core", "channels"],
            ["Claims processing", "Claims register"],
            ["claims_sla", "claims_stalled", "claims_unassigned", "claims_missing_documents"]),
        new("claims_review", "Claims Review and Investigation", "Claims", "Operations",
            "Exceptions, flagged claims and the supporting evidence for review.",
            ["Claims flagged high risk", "Claims in review", "Approvals waiting"],
            ["iip_core"],
            ["Flagged claims", "Review queue"],
            ["claims_high_risk", "approvals_waiting"]),
        new("renewals_retention", "Renewals and Retention", "Renewals", "Growth",
            "The expiry pipeline, lapse risk and follow-up with policyholders.",
            ["Policies expiring in 30 days", "Premium due for renewal", "Renewal follow-ups"],
            ["iip_core", "policy_admin", "channels"],
            ["Renewal pipeline", "Retention and lapse analysis"],
            ["policies_expiring"]),
        new("bancassurance", "Bancassurance", "Distribution", "Growth",
            "Insurance distributed through bank branches and relationship managers.",
            ["Policies sold by branch", "Leads to policies", "Policies expiring soon"],
            ["iip_core", "core_banking", "channels", "crm"],
            ["Bancassurance performance", "Branch scorecard"],
            ["policies_expiring"]),
        new("customer_service", "Customer Service", "Customer Service", "Operations",
            "Enquiries, complaints and follow-up with customers.",
            ["Open enquiries", "Unanswered handoffs", "Claims missing documents"],
            ["iip_core", "channels", "knowledge"],
            ["Service exceptions"],
            ["claims_missing_documents", "claims_unassigned"]),
        new("policy_administration", "Policy Administration", "Operations", "Operations",
            "Policy records, issuance and servicing.",
            ["Active policies", "Policies expiring in 30 days", "Policies without a customer"],
            ["iip_core", "policy_admin"],
            ["Policy servicing exceptions"],
            ["policies_expiring"]),
        new("underwriting", "Underwriting", "Underwriting", "Risk",
            "Underwriting support and referral review. Humans decide; this workspace only prepares.",
            ["Referrals waiting", "Claims flagged high risk"],
            ["iip_core", "policy_admin", "knowledge"],
            ["Underwriting portfolio review"],
            ["claims_high_risk"]),
        new("risk_compliance", "Risk and Compliance", "Compliance", "Risk",
            "Insurance obligations, exceptions and controls.",
            ["Claims past their deadline", "Claims flagged high risk", "Approvals waiting"],
            ["iip_core", "knowledge"],
            ["Exceptions register"],
            ["claims_sla", "claims_high_risk", "approvals_waiting"]),
        new("finance_accounting", "Finance and Accounting", "Finance", "Finance",
            "Premium production, billing and claims paid.",
            ["Premium production", "Premium receivable", "Claims paid"],
            ["finance_system", "policy_admin", "microsoft_365"],
            ["Premium production", "Finance pack"],
            ["daily_briefing"]),
        new("executive", "Executive Insurance Intelligence", "Executive", "Leadership",
            "One view of claims, renewals and distribution for leaders.",
            ["Open claims", "Claims past their deadline", "Policies expiring soon", "Approvals waiting"],
            ["iip_core", "finance_system"],
            ["Executive insurance review"],
            ["daily_briefing", "claims_sla"])
    ];

    public static readonly IReadOnlyList<AutomationTemplate> Automations =
    [
        new("claims_sla", "Claims past or near their service deadline", "Claims service",
            "Raises an item for each open claim whose service deadline has passed, or falls due within the hours you set.",
            "Meet the service deadline on every claim.", "schedule", "hourly",
            [new("hours", "Also alert when due within", "number", 24, "hours", "Zero alerts only on claims already past their deadline.")],
            ["One inbox item per claim"], ["Claims", "Service deadlines"], true),
        new("claims_stalled", "Claims with no movement", "Claims service",
            "Flags open claims that nobody has touched for longer than your target.",
            "Keep every open claim moving.", "schedule", "daily",
            [new("days", "No update for more than", "number", 5, "days")],
            ["One inbox item per claim"], ["Claims", "Last update"], true),
        new("claims_missing_documents", "Claims waiting on documents", "Claims service",
            "Finds claims that have been marked as missing documents for longer than your target.",
            "Complete every claim file before it reaches a decision.", "schedule", "daily",
            [new("days", "Waiting on documents for more than", "number", 2, "days")],
            ["One inbox item per claim"], ["Claims", "Required documents"], true),
        new("claims_unassigned", "Claims with no owner", "Claims service",
            "Finds open claims that nobody has been assigned to after your waiting time.",
            "Give every claim an owner.", "schedule", "hourly",
            [new("hours", "Unassigned for more than", "number", 4, "hours")],
            ["One inbox item per claim"], ["Claims", "Assignment"], true),
        new("claims_high_risk", "Claims flagged high risk", "Risk",
            "Lists open claims the rule engine flagged high risk so a person reviews them. It never decides the claim.",
            "Put every high risk claim in front of a person.", "schedule", "daily",
            [],
            ["One inbox item per claim"], ["Claims", "Risk level"], true),
        new("policies_expiring", "Policies approaching expiry", "Renewals",
            "Raises an item for each policy that expires within your window, so the renewal can be followed up.",
            "Improve policy renewal performance.", "schedule", "daily",
            [new("days", "Expiring within", "number", 30, "days")],
            ["One inbox item per policy"], ["Policies", "Expiry dates"], true),
        new("approvals_waiting", "Approvals waiting too long", "Governance",
            "Finds approvals that have waited for a decision longer than your target. A person still decides each one.",
            "Decide every approval on time.", "schedule", "hourly",
            [new("hours", "Waiting for more than", "number", 24, "hours")],
            ["One inbox item per approval"], ["Approvals"], true),
        new("daily_briefing", "Daily insurance briefing", "Reporting",
            "Each morning, summarises open claims, deadlines, expiring policies and approvals waiting into one briefing.",
            "Start every day with the same picture.", "schedule", "daily",
            [new("time", "Send at", "time", "07:00")],
            ["Briefing in the inbox"], ["Claims", "Policies", "Approvals"], true),
        new("custom", "Custom automation", "Custom",
            "Describe what to watch for. A person configures the data and logic before it can run.",
            "Your own objective.", "schedule", "daily",
            [new("instructions", "What should it watch for?", "text", "")],
            ["Inbox item"], ["Configured per automation"], false)
    ];

    public static WorkspaceTemplate? Workspace(string? key) =>
        Workspaces.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    public static AutomationTemplate? Automation(string? key) =>
        Automations.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}

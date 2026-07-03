using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class StaticWorkflowRuleProvider : IWorkflowRuleProvider
{
    private static readonly string[] SensitiveTerms =
    [
        "denial",
        "deny",
        "repudiation",
        "repudiate",
        "fraud",
        "settlement",
        "liability",
        "rejection",
        "reject"
    ];

    public IReadOnlyCollection<RecommendedNextAction> GetRecommendedActions(WorkflowEvaluationContext context)
    {
        var actions = new List<RecommendedNextAction>();
        var evidence = context.Checklist.MissingRequiredDocuments
            .Select(document => document.DocumentType.ToString())
            .ToArray();

        if (context.ClaimCase.CompletenessScore < 100m)
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.RequestMissingDocuments,
                "Request missing documents",
                "Required documents are still missing before this case can move to review.",
                RequiresHumanApproval: false,
                "Normal",
                evidence));
        }
        else
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.MarkReadyForReview,
                "Mark ready for review",
                "All required documents are present. A claims officer can prepare the case for human review.",
                RequiresHumanApproval: false,
                "Normal",
                Array.Empty<string>()));
        }

        if (IsHighRisk(context.ClaimCase.RiskLevel))
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.SupervisorReview,
                "Send to supervisor",
                "High or critical risk cases require supervisor approval before workflow progression.",
                RequiresHumanApproval: true,
                "High",
                ["risk-level"]));
        }

        if (context.AgentActions.Any(action => action.RiskFlag is "no_source" or "needs_review"))
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.HumanReview,
                "Review assistant escalation",
                "The assistant escalated a policy answer and a human reviewer should confirm next steps.",
                RequiresHumanApproval: true,
                "High",
                context.AgentActions.Select(action => action.AgentActionLogId.ToString()).ToArray()));
        }

        if (context.SlaStatus == SlaStatus.AtRisk)
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.FollowUpCustomer,
                "SLA at risk",
                "The case SLA is within 24 hours. Prioritize follow-up.",
                RequiresHumanApproval: false,
                "High",
                ["sla-at-risk"]));
        }
        else if (context.SlaStatus == SlaStatus.Breached)
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.HumanReview,
                "SLA breached",
                "The case SLA has breached and should be escalated for operational review.",
                RequiresHumanApproval: true,
                "Critical",
                ["sla-breached"]));
        }

        return actions;
    }

    public bool CustomerUpdateRequiresApproval(string messageBody) =>
        SensitiveTerms.Any(term => messageBody.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsHighRisk(string riskLevel) =>
        string.Equals(riskLevel, "High", StringComparison.OrdinalIgnoreCase)
        || string.Equals(riskLevel, "Critical", StringComparison.OrdinalIgnoreCase);
}

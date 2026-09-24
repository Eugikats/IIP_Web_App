using InsuranceIntelligence.Domain.Cases;
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

        var routingSignals = DetectRoutingSignals(context);
        var routing = ClaimRoutingPolicy.Evaluate(context.ClaimCase, routingSignals);

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
        else if (routing.AllowsStandardReviewProgression)
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.MarkReadyForReview,
                "Mark ready for review",
                "All required documents are present. A claims officer can prepare the case for human review.",
                RequiresHumanApproval: false,
                "Normal",
                Array.Empty<string>()));
        }

        if (routing.RequiresDesignatedHumanEscalation)
        {
            actions.Add(new RecommendedNextAction(
                RecommendedNextActionType.SupervisorReview,
                "Designated human escalation",
                routing.OperatorExplanation,
                RequiresHumanApproval: true,
                "Critical",
                routing.EscalationTriggers.Select(trigger => trigger.ToString()).ToArray()));
        }
        else if (IsHighRisk(context.ClaimCase.RiskLevel))
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

    private static ClaimRoutingSignals DetectRoutingSignals(WorkflowEvaluationContext context)
    {
        var contested = context.ClaimCase.ClaimType.Contains("contested", StringComparison.OrdinalIgnoreCase)
            || context.ClaimCase.ClaimType.Contains("liability dispute", StringComparison.OrdinalIgnoreCase)
            || context.AgentActions.Any(action =>
                action.RiskFlag?.Contains("contested", StringComparison.OrdinalIgnoreCase) == true);

        // Fraud signals are advisory operator findings surfaced for contest/override, never auto-verdicts.
        var fraud = context.AgentActions.Any(action =>
                action.RiskFlag?.Contains("fraud", StringComparison.OrdinalIgnoreCase) == true)
            || string.Equals(context.ClaimCase.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase)
                && context.ClaimCase.IncidentDescription.Contains("fraud", StringComparison.OrdinalIgnoreCase);

        var settlement = context.AgentActions.Any(action =>
            action.RiskFlag?.Contains("settlement", StringComparison.OrdinalIgnoreCase) == true
            || action.RiskFlag?.Contains("payment", StringComparison.OrdinalIgnoreCase) == true);

        var repudiation = context.AgentActions.Any(action =>
            action.RiskFlag?.Contains("repudiat", StringComparison.OrdinalIgnoreCase) == true
            || action.RiskFlag?.Contains("deny", StringComparison.OrdinalIgnoreCase) == true);

        return new ClaimRoutingSignals(contested, fraud, settlement, repudiation);
    }

    private static bool IsHighRisk(string riskLevel) =>
        string.Equals(riskLevel, "High", StringComparison.OrdinalIgnoreCase)
        || string.Equals(riskLevel, "Critical", StringComparison.OrdinalIgnoreCase);
}

namespace InsuranceIntelligence.Domain.Workflow;

public interface IWorkflowRuleProvider
{
    IReadOnlyCollection<RecommendedNextAction> GetRecommendedActions(WorkflowEvaluationContext context);

    bool CustomerUpdateRequiresApproval(string messageBody);
}

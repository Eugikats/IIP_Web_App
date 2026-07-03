using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Domain.Workflow;

public sealed record WorkflowEvaluationContext(
    ClaimCase ClaimCase,
    DocumentCompletenessChecklist Checklist,
    IReadOnlyCollection<AgentActionLog> AgentActions,
    SlaStatus SlaStatus);

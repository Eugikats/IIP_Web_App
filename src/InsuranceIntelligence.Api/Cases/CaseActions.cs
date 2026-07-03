using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Cases;

internal static class CaseActions
{
    public const string OpenCaseReview = "openCaseReview";
    public const string EvaluateCaseWorkflow = "evaluateCaseWorkflow";
    public const string CreateApprovalRequest = "createApprovalRequest";
    public const string DraftCustomerUpdate = "draftCustomerUpdate";

    public static IReadOnlyCollection<ActionStateResponse> Allowed(ClaimCase claimCase)
    {
        var actions = new List<ActionStateResponse>
        {
            Action(OpenCaseReview, "Open case review", Permission.ViewAssignedClaims)
        };

        if (CanEvaluateWorkflow(claimCase, out _))
        {
            actions.Add(Action(EvaluateCaseWorkflow, "Evaluate workflow", Permission.ApproveSettlementWorkflowStep));
        }

        if (CanCreateApproval(claimCase, out _))
        {
            actions.Add(Action(CreateApprovalRequest, "Create approval request", Permission.ApproveSettlementWorkflowStep));
        }

        if (CanDraftCustomerUpdate(claimCase, out _))
        {
            actions.Add(Action(DraftCustomerUpdate, "Draft customer update", Permission.ManageCustomerCommunications));
        }

        return actions;
    }

    public static IReadOnlyCollection<ActionStateResponse> Blocked(ClaimCase claimCase)
    {
        var actions = new List<ActionStateResponse>();

        if (!CanEvaluateWorkflow(claimCase, out var evaluateReason))
        {
            actions.Add(Action(EvaluateCaseWorkflow, "Evaluate workflow", Permission.ApproveSettlementWorkflowStep, evaluateReason));
        }

        if (!CanCreateApproval(claimCase, out var approvalReason))
        {
            actions.Add(Action(CreateApprovalRequest, "Create approval request", Permission.ApproveSettlementWorkflowStep, approvalReason));
        }

        if (!CanDraftCustomerUpdate(claimCase, out var updateReason))
        {
            actions.Add(Action(DraftCustomerUpdate, "Draft customer update", Permission.ManageCustomerCommunications, updateReason));
        }

        return actions;
    }

    public static bool CanEvaluateWorkflow(ClaimCase claimCase, out string? reason)
    {
        if (IsTerminal(claimCase))
        {
            reason = "Settled or closed cases cannot be re-evaluated from the active workflow queue.";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool CanCreateApproval(ClaimCase claimCase, out string? reason)
    {
        if (IsTerminal(claimCase))
        {
            reason = "Settled or closed cases cannot create new approval requests.";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool CanDraftCustomerUpdate(ClaimCase claimCase, out string? reason)
    {
        if (claimCase.Status == CaseStatus.Closed)
        {
            reason = "Closed cases cannot create new customer update drafts.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsTerminal(ClaimCase claimCase) =>
        claimCase.Status is CaseStatus.Settled or CaseStatus.Closed;

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);
}

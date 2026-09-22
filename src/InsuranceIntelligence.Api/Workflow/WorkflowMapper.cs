using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

public static class WorkflowMapper
{
    public static RecommendedActionResponse ToResponse(RecommendedNextAction action) =>
        new(
            action.ActionType.ToString(),
            action.Label,
            action.Summary,
            action.RequiresHumanApproval,
            action.Priority,
            action.Evidence);

    public static WorkflowTaskResponse ToResponse(WorkflowTask task) =>
        new(
            task.TaskId,
            task.CaseId,
            task.TaskType.ToString(),
            task.Status.ToString(),
            task.Title,
            task.Summary,
            task.AssignedUserId,
            task.AssignedTeam,
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt,
            task.CorrelationId);

    public static ApprovalResponse ToResponse(ApprovalRequest approval) =>
        new(
            approval.ApprovalId,
            approval.CaseId,
            approval.ApprovalType,
            approval.Status.ToString(),
            approval.RiskLevel,
            approval.EvidenceSummary,
            approval.SourceReferences,
            approval.RequestedByUserId,
            approval.ReviewerUserId,
            approval.Decision?.ToString(),
            approval.Comments,
            approval.ExecutedByUserId,
            approval.ExecutionResult,
            approval.CreatedAt,
            approval.UpdatedAt,
            approval.DecidedAt,
            approval.ExecutedAt,
            approval.CorrelationId,
            ApprovalActions.Allowed(approval),
            ApprovalActions.Blocked(approval));

    public static CustomerUpdateResponse ToResponse(CustomerUpdateDraft update) =>
        new(
            update.UpdateId,
            update.CaseId,
            update.ReasonCategory.ToString(),
            update.Channel.ToString(),
            update.Status.ToString(),
            update.MessageBody,
            update.ShortMessageBody,
            update.Language,
            update.CreationMethod.ToString(),
            update.OriginalMessageBody,
            update.EditedByUserId,
            update.EditedAt,
            update.SourceTemplateKey,
            update.RequiresApproval,
            update.Sensitive,
            update.DeliveryStatus,
            update.CreatedByUserId,
            update.ApprovedByUserId,
            update.SentByUserId,
            update.CreatedAt,
            update.UpdatedAt,
            update.ApprovedAt,
            update.SentAt,
            update.CorrelationId,
            CustomerUpdateActions.Allowed(update),
            CustomerUpdateActions.Blocked(update));

    public static CaseTimelineEventResponse ToResponse(CaseTimelineEvent timelineEvent) =>
        new(
            timelineEvent.EventId,
            timelineEvent.CaseId,
            timelineEvent.EventType,
            timelineEvent.Actor,
            timelineEvent.Timestamp,
            timelineEvent.Summary,
            timelineEvent.BeforeStatus,
            timelineEvent.AfterStatus,
            timelineEvent.CorrelationId);
}

internal static class CustomerUpdateActions
{
    public static IReadOnlyCollection<ActionStateResponse> Allowed(CustomerUpdateDraft update)
    {
        var actions = new List<ActionStateResponse>();
        if (CanEdit(update))
        {
            actions.Add(Action("editCustomerUpdate", "Edit before sending", Permission.ManageCustomerCommunications));
        }

        if (update.Status == CustomerUpdateStatus.PendingApproval)
        {
            actions.Add(Action("approveCustomerUpdate", "Approve update", Permission.ManageCustomerCommunications));
            actions.Add(Action("rejectCustomerUpdate", "Reject update", Permission.ManageCustomerCommunications));
        }

        if (CanSend(update, out _))
        {
            actions.Add(Action("sendCustomerUpdate", "Send through configured provider", Permission.SendCustomerUpdate));
        }

        return actions;
    }

    public static IReadOnlyCollection<ActionStateResponse> Blocked(CustomerUpdateDraft update)
    {
        var actions = new List<ActionStateResponse>();
        if (!CanEdit(update))
        {
            actions.Add(Action("editCustomerUpdate", "Edit before sending", Permission.ManageCustomerCommunications, "A sent message can never be edited, a correction is a new message."));
        }

        if (update.Status != CustomerUpdateStatus.PendingApproval)
        {
            actions.Add(Action(
                "approveCustomerUpdate",
                "Approve update",
                Permission.ManageCustomerCommunications,
                update.Status is CustomerUpdateStatus.Sent or CustomerUpdateStatus.SimulatedSent
                    ? "Already sent customer updates cannot be approved again."
                    : "Only updates pending human approval can be approved."));
            actions.Add(Action(
                "rejectCustomerUpdate",
                "Reject update",
                Permission.ManageCustomerCommunications,
                update.Status is CustomerUpdateStatus.Sent or CustomerUpdateStatus.SimulatedSent
                    ? "Already sent customer updates cannot be rejected."
                    : "Only updates pending human approval can be rejected."));
        }

        if (!CanSend(update, out var reason))
        {
            actions.Add(Action("sendCustomerUpdate", "Send through configured provider", Permission.SendCustomerUpdate, reason));
        }

        return actions;
    }

    private static bool CanEdit(CustomerUpdateDraft update) =>
        update.Status is not (CustomerUpdateStatus.Sent or CustomerUpdateStatus.SimulatedSent);

    private static bool CanSend(CustomerUpdateDraft update, out string? reason)
    {
        reason = update.Status switch
        {
            CustomerUpdateStatus.Draft when update.RequiresApproval => "This customer update requires human approval before sending.",
            CustomerUpdateStatus.Draft => null,
            CustomerUpdateStatus.Approved => null,
            CustomerUpdateStatus.PendingApproval => "This customer update requires human approval before sending.",
            CustomerUpdateStatus.Rejected => "Rejected customer updates cannot be sent.",
            CustomerUpdateStatus.Sent or CustomerUpdateStatus.SimulatedSent => "This customer update has already been sent.",
            _ => "This customer update is not in a sendable state."
        };

        return reason is null;
    }

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);
}

internal static class ApprovalActions
{
    private const string ExecuteApprovedNextStep = "executeApprovedNextStep";

    public static IReadOnlyCollection<ActionStateResponse> Allowed(ApprovalRequest approval)
    {
        if (approval.Status == ApprovalStatus.Pending)
        {
            return
            [
                Action("approveApproval", "Approve workflow step", Permission.ApproveSettlementWorkflowStep),
                Action("rejectApproval", "Reject workflow step", Permission.ApproveSettlementWorkflowStep),
                Action("escalateApproval", "Escalate workflow step", Permission.ApproveSettlementWorkflowStep),
                Action("overrideApproval", "Override AI recommendation", Permission.OverrideAiRecommendation)
            ];
        }

        return CanExecuteApprovedNextStep(approval, out _)
            ? [Action(ExecuteApprovedNextStep, "Execute approved next step", Permission.ApproveSettlementWorkflowStep)]
            : Array.Empty<ActionStateResponse>();
    }

    public static IReadOnlyCollection<ActionStateResponse> Blocked(ApprovalRequest approval)
    {
        var actions = new List<ActionStateResponse>();

        if (approval.Status != ApprovalStatus.Pending)
        {
            var reason = $"This approval is already {approval.Status.ToString().ToLowerInvariant()} and cannot be changed from this queue.";
            actions.Add(Action("approveApproval", "Approve workflow step", Permission.ApproveSettlementWorkflowStep, reason));
            actions.Add(Action("rejectApproval", "Reject workflow step", Permission.ApproveSettlementWorkflowStep, reason));
            actions.Add(Action("escalateApproval", "Escalate workflow step", Permission.ApproveSettlementWorkflowStep, reason));
            actions.Add(Action("overrideApproval", "Override AI recommendation", Permission.OverrideAiRecommendation, reason));
        }

        if (!CanExecuteApprovedNextStep(approval, out var executionReason))
        {
            actions.Add(Action(ExecuteApprovedNextStep, "Execute approved next step", Permission.ApproveSettlementWorkflowStep, executionReason));
        }

        return actions;
    }

    public static bool CanExecuteApprovedNextStep(ApprovalRequest approval, out string? reason)
    {
        if (approval.ExecutedAt is not null)
        {
            reason = "This approval's next step has already been executed.";
            return false;
        }

        if (approval.Status is ApprovalStatus.Approved or ApprovalStatus.Overridden)
        {
            reason = null;
            return true;
        }

        reason = "Only approved or overridden approval requests can execute a workflow next step.";
        return false;
    }

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);
}

using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public void IncompleteCaseCanRepresentMissingDocumentRecommendation()
    {
        var action = new RecommendedNextAction(
            RecommendedNextActionType.RequestMissingDocuments,
            "Request missing documents",
            "Required documents are still missing.",
            RequiresHumanApproval: false,
            "Normal",
            ["ClaimForm"]);

        Assert.Equal(RecommendedNextActionType.RequestMissingDocuments, action.ActionType);
        Assert.False(action.RequiresHumanApproval);
    }

    [Fact]
    public void CompleteCaseCanRepresentReadyForReviewRecommendation()
    {
        var action = new RecommendedNextAction(
            RecommendedNextActionType.MarkReadyForReview,
            "Mark ready for review",
            "All required documents are present.",
            RequiresHumanApproval: false,
            "Normal",
            Array.Empty<string>());

        Assert.Equal(RecommendedNextActionType.MarkReadyForReview, action.ActionType);
        Assert.Empty(action.Evidence);
    }

    [Fact]
    public void SensitiveCustomerUpdateRequiresApprovalShape()
    {
        var update = CustomerUpdate(requiresApproval: true, sensitive: true);

        Assert.True(update.RequiresApproval);
        Assert.True(update.Sensitive);
        Assert.Equal(CustomerUpdateStatus.PendingApproval, update.Status);
    }

    [Fact]
    public void NonSensitiveMissingDocumentUpdateCanRemainLowRisk()
    {
        var update = CustomerUpdate(
            requiresApproval: false,
            sensitive: false) with
        {
            ReasonCategory = CustomerUpdateReasonCategory.MissingDocuments,
            Status = CustomerUpdateStatus.Draft
        };

        Assert.False(update.RequiresApproval);
        Assert.False(update.Sensitive);
        Assert.Equal(CustomerUpdateReasonCategory.MissingDocuments, update.ReasonCategory);
    }

    [Theory]
    [InlineData(72, SlaStatus.OnTrack)]
    [InlineData(12, SlaStatus.AtRisk)]
    [InlineData(-1, SlaStatus.Breached)]
    public void SlaStatusBoundaryCanBeRepresented(int hoursFromNow, SlaStatus expected)
    {
        var now = DateTimeOffset.UtcNow;
        var dueAt = now.AddHours(hoursFromNow);
        var actual = dueAt <= now
            ? SlaStatus.Breached
            : dueAt <= now.AddHours(24)
                ? SlaStatus.AtRisk
                : SlaStatus.OnTrack;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TimelineContainsExpectedEventTypesInOrder()
    {
        var caseId = Guid.NewGuid();
        var events = new[]
        {
            new CaseTimelineEvent(Guid.NewGuid(), caseId, "case.created", "User", DateTimeOffset.UtcNow.AddMinutes(-3), "Case created.", null, "New", "corr-1"),
            new CaseTimelineEvent(Guid.NewGuid(), caseId, "workflow.evaluated", "System", DateTimeOffset.UtcNow.AddMinutes(-2), "Workflow evaluated.", null, "New", "corr-2"),
            new CaseTimelineEvent(Guid.NewGuid(), caseId, "approval.created", "User", DateTimeOffset.UtcNow.AddMinutes(-1), "Approval created.", null, "Pending", "corr-3")
        };

        var ordered = events.OrderBy(item => item.Timestamp).Select(item => item.EventType).ToArray();

        Assert.Equal(["case.created", "workflow.evaluated", "approval.created"], ordered);
    }

    [Fact]
    public void WorkflowAuditEventShapeIsValid()
    {
        var task = new WorkflowTask
        {
            TaskId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = Guid.NewGuid(),
            TaskType = WorkflowTaskType.ReviewCase,
            Status = WorkflowTaskStatus.Open,
            Title = "Review case",
            Summary = "Human review required.",
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-workflow-001"
        };

        var auditEvent = AuditEvent.WorkflowTaskCreated(task);

        Assert.Equal("workflow.task.created", auditEvent.ActionName);
        Assert.Equal(task.CaseId, auditEvent.CaseId);
        Assert.Equal(task.CorrelationId, auditEvent.CorrelationId);
    }

    private static CustomerUpdateDraft CustomerUpdate(bool requiresApproval, bool sensitive) =>
        new()
        {
            UpdateId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = Guid.NewGuid(),
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = CommunicationChannel.Portal,
            Status = requiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = "Customer-safe status update.",
            ShortMessageBody = "Customer-safe status update.",
            CreationMethod = CustomerUpdateCreationMethod.ManuallyWritten,
            RequiresApproval = requiresApproval,
            Sensitive = sensitive,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-update-001"
        };
}

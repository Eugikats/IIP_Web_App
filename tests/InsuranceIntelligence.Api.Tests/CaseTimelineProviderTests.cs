using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// WO 5.6: "every step across every channel in one place" only holds if every real audit action
/// this codebase actually writes for a case gets a real, specific summary — not the generic
/// fallback. This is a characterization test over the full, real action-name surface (kept in
/// sync with AuditEvent.cs's factory methods and every inline ActionName literal this session's
/// WhatsApp/email/FNOL-acknowledgement/missing-document work introduced), so a newly added action
/// name that forgets to update CaseTimelineProvider fails loudly here instead of silently showing
/// "A case event was recorded" in production.
/// </summary>
public sealed class CaseTimelineProviderTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-timeline", "workspace-claims");
    private const string GenericFallback = "A case event was recorded.";

    public static IEnumerable<object[]> RealActionNames =>
    [
        ["case.created"], ["case.assigned"], ["case.escalated"], ["case.confirmed"], ["case.status.updated"], ["case.completeness.updated"],
        ["document.uploaded"], ["document.processed"], ["document.reclassified"], ["document.extraction.approved"], ["document.extraction.field_corrected"],
        ["email.case_created"], ["email.case_linked"], ["email.attachment_processed"], ["email.attachment_processing_failed"], ["email.received"],
        ["whatsapp.outbound.attempted"],
        ["customer_assistant.autoreplied"], ["customer_assistant.queuedforhumanapproval"], ["customer_assistant.caseopened"],
        ["customer_assistant.leadcreated"], ["customer_assistant.escalated"], ["customer_assistant.awaitingmoreinformation"], ["customer_assistant.declined"],
        ["fnol.acknowledgement"], ["missing_document_request"],
        ["assistant.policy_question.answered"], ["workflow.evaluated"], ["workflow.task.created"], ["workflow.task.assigned"], ["workflow.task.completed"],
        ["approval.created"], ["approval.approved"], ["approval.rejected"], ["approval.overridden"], ["approval.escalated"], ["approval.next_step.executed"],
        ["customer_update.drafted"], ["customer_update.approved"], ["customer_update.rejected"], ["customer_update.sent"],
        ["claim.review_requested"], ["claim.review_acknowledged"], ["claim.review_resolved"], ["case_review.generated"],
        ["client_portal.magic_link.requested"], ["client_portal.session.established"],
        ["evidence_pack.exported"],
    ];

    [Theory]
    [MemberData(nameof(RealActionNames))]
    public void Build_EveryRealActionName_GetsASpecificSummary_NotTheGenericFallback(string actionName)
    {
        var caseId = Guid.NewGuid();
        var provider = new CaseTimelineProvider();

        var timeline = provider.Build(caseId, [Event(caseId, actionName)]);

        var entry = Assert.Single(timeline);
        Assert.NotEqual(GenericFallback, entry.Summary);
        Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
    }

    [Fact]
    public void Build_UnknownActionName_FallsBackHonestlyRatherThanThrowing()
    {
        var caseId = Guid.NewGuid();
        var provider = new CaseTimelineProvider();

        var timeline = provider.Build(caseId, [Event(caseId, "some.future.action.nobody.mapped.yet")]);

        Assert.Equal(GenericFallback, Assert.Single(timeline).Summary);
    }

    [Fact]
    public void Build_OrdersByTimestampAndFiltersToTheRequestedCase()
    {
        var caseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var provider = new CaseTimelineProvider();

        var timeline = provider.Build(caseId,
        [
            Event(caseId, "workflow.evaluated", now),
            Event(caseId, "case.created", now.AddMinutes(-5)),
            Event(otherCaseId, "case.created", now.AddMinutes(-10)),
        ]);

        Assert.Equal(2, timeline.Count);
        Assert.Equal("case.created", timeline.ElementAt(0).EventType);
        Assert.Equal("workflow.evaluated", timeline.ElementAt(1).EventType);
    }

    private static AuditEvent Event(Guid caseId, string actionName, DateTimeOffset? timestamp = null) =>
        new(
            Guid.NewGuid(), Scope, caseId, null, "user-claims-1", ActorType.User, actionName,
            timestamp ?? DateTimeOffset.UtcNow, null, null, [], null, null, "recorded", null, null, "corr-timeline-001");
}

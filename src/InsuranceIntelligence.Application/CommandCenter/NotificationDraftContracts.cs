using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.CommandCenter;

public interface INotificationDraftService
{
    Task<NotificationDraftResult> CreateDraftAsync(
        NotificationDraftCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationDraftCreateRequest(
    string TenantId,
    string? WorkspaceId,
    string RelatedEntityType,
    string RelatedEntityId,
    string Channel,
    string Preview,
    bool RequiresApproval,
    string CreatedByUserId,
    string CorrelationId);

public sealed record NotificationDraftResult(
    string DraftId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed class CustomerUpdateNotificationDraftService(
    ICustomerUpdateRepository customerUpdateRepository) : INotificationDraftService
{
    public async Task<NotificationDraftResult> CreateDraftAsync(
        NotificationDraftCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.RelatedEntityType.Equals("claim", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only claim customer-update drafts are supported.");
        }

        if (!Guid.TryParse(request.RelatedEntityId, out var caseId))
        {
            throw new InvalidOperationException("Notification draft related entity id must be a claim/case GUID.");
        }

        if (string.IsNullOrWhiteSpace(request.WorkspaceId))
        {
            throw new InvalidOperationException("Notification draft workspace id is required.");
        }

        var channel = ParseChannel(request.Channel);
        var now = DateTimeOffset.UtcNow;
        var draft = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = TenantScope.Create(request.TenantId, request.WorkspaceId),
            CaseId = caseId,
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = channel,
            Status = request.RequiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = request.Preview,
            RequiresApproval = request.RequiresApproval,
            Sensitive = true,
            DeliveryStatus = null,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = request.CorrelationId
        };

        await customerUpdateRepository.SaveAsync(draft, cancellationToken);

        return new NotificationDraftResult(
            draft.UpdateId.ToString(),
            draft.Status.ToString(),
            draft.CreatedAt);
    }

    private static CommunicationChannel ParseChannel(string value) =>
        Enum.TryParse<CommunicationChannel>(value, ignoreCase: true, out var channel)
            ? channel
            : CommunicationChannel.Email;
}

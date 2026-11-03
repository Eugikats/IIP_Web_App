using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using System.Text.Json;

namespace InsuranceIntelligence.Application.Notifications;

/// <summary>
/// Emits real in-app notifications from real events elsewhere in the system (approval assigned,
/// claim escalated, SLA breach, document extraction needing review, review requested) and serves
/// the recipient's own feed. Never called with synthesised content (AGENTS.md A5).
/// </summary>
public interface INotificationEmitter
{
    Task NotifyAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="NotifyAsync"/> but skips creating a duplicate when a
    /// notification of this type already exists for this recipient/case, for events re-derived
    /// from a live condition on every read (SLA breach) rather than a one-shot action.</summary>
    Task NotifyOnceAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface INotificationFeedService
{
    Task<NotificationFeedResponse> GetFeedAsync(int limit, CancellationToken cancellationToken = default);

    Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}

public sealed record NotificationDto(
    Guid NotificationId,
    string Type,
    string Message,
    Guid CaseId,
    DateTimeOffset CreatedAt,
    bool IsRead,
    DateTimeOffset? ReadAt);

public sealed record NotificationFeedResponse(
    int UnreadCount,
    IReadOnlyCollection<NotificationDto> Notifications);

public sealed class NotificationNotFoundException : Exception;

public sealed class NotificationService(
    ITenantContextAccessor tenantContextAccessor,
    INotificationRepository repository,
    ITenantConfigurationRepository? tenantConfigurationRepository = null,
    ITeamsNotificationQueue? teamsNotificationQueue = null,
    IUserAccountRepository? userAccountRepository = null) : INotificationEmitter, INotificationFeedService
{
    public async Task NotifyAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            // No known recipient (e.g. an unassigned approval), nothing to notify. Not an error;
            // the caller's own action still completes.
            return;
        }

        var rule = await GetRoutingRuleAsync(scope, type, cancellationToken);
        await repository.SaveAsync(Build(scope, recipientUserId, type, message, caseId), cancellationToken);
        await NotifyAdditionalRecipientsAsync(scope, rule, type, message, caseId, deduplicate: false, cancellationToken);
        await SendTeamsNotificationAsync(scope, rule, type, caseId, cancellationToken);
    }

    public async Task NotifyOnceAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            return;
        }

        var rule = await GetRoutingRuleAsync(scope, type, cancellationToken);
        if (await repository.ExistsAsync(scope, recipientUserId, type, caseId, cancellationToken))
        {
            return;
        }

        await repository.SaveAsync(Build(scope, recipientUserId, type, message, caseId), cancellationToken);
        await NotifyAdditionalRecipientsAsync(scope, rule, type, message, caseId, deduplicate: true, cancellationToken);
        await SendTeamsNotificationAsync(scope, rule, type, caseId, cancellationToken);
    }

    private async Task<NotificationRoutingRule?> GetRoutingRuleAsync(
        TenantScope scope,
        NotificationType type,
        CancellationToken cancellationToken)
    {
        if (tenantConfigurationRepository is null) return null;
        var configuration = await tenantConfigurationRepository.GetAsync(scope, cancellationToken);
        return configuration.NotificationRouting.Rules.FirstOrDefault(rule => rule.Type == type);
    }

    private async Task NotifyAdditionalRecipientsAsync(
        TenantScope scope,
        NotificationRoutingRule? rule,
        NotificationType type,
        string message,
        Guid caseId,
        bool deduplicate,
        CancellationToken cancellationToken)
    {
        if (rule is null || !rule.AdditionalDestinations.Contains(NotificationDestination.InApp)) return;
        foreach (var userId in rule.AdditionalRecipientUserIds)
        {
            if (userAccountRepository is not null)
            {
                var account = await userAccountRepository.GetAsync(userId, scope, cancellationToken);
                if (account is null || !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase)) continue;
            }
            if (deduplicate && await repository.ExistsAsync(scope, userId, type, caseId, cancellationToken)) continue;
            await repository.SaveAsync(Build(scope, userId, type, message, caseId), cancellationToken);
        }
    }

    private async Task SendTeamsNotificationAsync(
        TenantScope scope,
        NotificationRoutingRule? rule,
        NotificationType type,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (teamsNotificationQueue is null || rule is null
            || !rule.AdditionalDestinations.Contains(NotificationDestination.MicrosoftTeams)) return;
        var teamsType = type switch
        {
            NotificationType.ApprovalAssigned => TeamsNotificationType.ApprovalRequired,
            NotificationType.SlaAtRisk => TeamsNotificationType.SlaAtRisk,
            NotificationType.SlaBreach => TeamsNotificationType.SlaBreached,
            NotificationType.ClaimEscalated => TeamsNotificationType.ClaimEscalated,
            NotificationType.DocumentReviewNeeded => TeamsNotificationType.DocumentReviewNeeded,
            NotificationType.ReviewRequested => TeamsNotificationType.ReviewRequested,
            NotificationType.ClaimAssigned => TeamsNotificationType.ClaimAssigned,
            _ => (TeamsNotificationType?)null,
        };
        if (teamsType is null) return;

        var notification = new TeamsNotification
        {
            Scope = scope,
            CaseId = caseId,
            NotificationType = teamsType.Value,
            PayloadJson = JsonSerializer.Serialize(new { EventType = type.ToString() }),
            DeepLinkUrl = string.Empty,
        };
        await teamsNotificationQueue.EnqueueAsync(notification, cancellationToken);
    }

    private static Notification Build(TenantScope scope, string recipientUserId, NotificationType type, string message, Guid caseId) =>
        new(
            Guid.NewGuid(),
            scope,
            recipientUserId,
            type,
            message,
            caseId,
            DateTimeOffset.UtcNow,
            IsRead: false,
            ReadAt: null);

    public async Task<NotificationFeedResponse> GetFeedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var unreadCount = await repository.CountUnreadForUserAsync(context.Scope, context.UserId, cancellationToken);
        var notifications = await repository.ListForUserAsync(context.Scope, context.UserId, boundedLimit, cancellationToken);
        return new NotificationFeedResponse(unreadCount, notifications.Select(ToDto).ToArray());
    }

    public async Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var notification = await repository.GetAsync(notificationId, context.Scope, context.UserId, cancellationToken)
            ?? throw new NotificationNotFoundException();

        if (notification.IsRead)
        {
            return ToDto(notification);
        }

        var read = notification with { IsRead = true, ReadAt = DateTimeOffset.UtcNow };
        await repository.SaveAsync(read, cancellationToken);
        return ToDto(read);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var unread = (await repository.ListForUserAsync(context.Scope, context.UserId, 100, cancellationToken))
            .Where(item => !item.IsRead)
            .ToArray();

        foreach (var notification in unread)
        {
            await repository.SaveAsync(notification with { IsRead = true, ReadAt = DateTimeOffset.UtcNow }, cancellationToken);
        }

        return unread.Length;
    }

    private static NotificationDto ToDto(Notification notification) => new(
        notification.NotificationId,
        notification.Type.ToString(),
        notification.Message,
        notification.CaseId,
        notification.CreatedAt,
        notification.IsRead,
        notification.ReadAt);
}

using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

// WO-14: proves the in-app notification domain end to end — creation from an emitter call,
// a user- and tenant-scoped feed, read-state transitions, and cross-user isolation (I5).
public sealed class NotificationTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    [Fact]
    public async Task NotifyAsync_AppearsOnlyInTheRecipientsOwnFeed_AndCountsAsUnread()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(ContextFor("officer-1"), repository);
        var caseId = Guid.NewGuid();

        await service.NotifyAsync(Scope, "officer-1", NotificationType.ClaimEscalated, "This claim was escalated.", caseId);

        var ownFeed = await service.GetFeedAsync(20);
        Assert.Equal(1, ownFeed.UnreadCount);
        var notification = Assert.Single(ownFeed.Notifications);
        Assert.Equal("ClaimEscalated", notification.Type);
        Assert.Equal(caseId, notification.CaseId);
        Assert.False(notification.IsRead);

        var otherUserService = new NotificationService(ContextFor("officer-2"), repository);
        var otherFeed = await otherUserService.GetFeedAsync(20);
        Assert.Equal(0, otherFeed.UnreadCount);
        Assert.Empty(otherFeed.Notifications);
    }

    [Fact]
    public async Task NotifyAsync_WithNoRecipient_CreatesNothing()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(ContextFor("officer-1"), repository);

        await service.NotifyAsync(Scope, recipientUserId: "", NotificationType.ApprovalAssigned, "message", Guid.NewGuid());

        var feed = await service.GetFeedAsync(20);
        Assert.Empty(feed.Notifications);
    }

    [Fact]
    public async Task MarkReadAsync_TransitionsState_AndCannotMarkAnotherUsersNotification()
    {
        var repository = new InMemoryNotificationRepository();
        var owner = new NotificationService(ContextFor("officer-1"), repository);
        var caseId = Guid.NewGuid();
        await owner.NotifyAsync(Scope, "officer-1", NotificationType.ReviewRequested, "The customer requested a review.", caseId);
        var notificationId = (await owner.GetFeedAsync(20)).Notifications.Single().NotificationId;

        var read = await owner.MarkReadAsync(notificationId);
        Assert.True(read.IsRead);
        Assert.NotNull(read.ReadAt);
        Assert.Equal(0, (await owner.GetFeedAsync(20)).UnreadCount);

        var intruder = new NotificationService(ContextFor("officer-2"), repository);
        await Assert.ThrowsAsync<NotificationNotFoundException>(() => intruder.MarkReadAsync(notificationId));
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksOnlyTheCallersUnreadNotifications()
    {
        var repository = new InMemoryNotificationRepository();
        var owner = new NotificationService(ContextFor("officer-1"), repository);
        await owner.NotifyAsync(Scope, "officer-1", NotificationType.ClaimEscalated, "a", Guid.NewGuid());
        await owner.NotifyAsync(Scope, "officer-1", NotificationType.SlaBreach, "b", Guid.NewGuid());
        var other = new NotificationService(ContextFor("officer-2"), repository);
        await other.NotifyAsync(Scope, "officer-2", NotificationType.ClaimEscalated, "c", Guid.NewGuid());

        var markedCount = await owner.MarkAllReadAsync();

        Assert.Equal(2, markedCount);
        Assert.Equal(0, (await owner.GetFeedAsync(20)).UnreadCount);
        Assert.Equal(1, (await other.GetFeedAsync(20)).UnreadCount);
    }

    [Fact]
    public async Task NotifyOnceAsync_DoesNotDuplicate_ForTheSameRecipientTypeAndCase()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(ContextFor("officer-1"), repository);
        var caseId = Guid.NewGuid();

        await service.NotifyOnceAsync(Scope, "officer-1", NotificationType.SlaBreach, "This claim's SLA has been breached.", caseId);
        await service.NotifyOnceAsync(Scope, "officer-1", NotificationType.SlaBreach, "This claim's SLA has been breached.", caseId);
        await service.NotifyOnceAsync(Scope, "officer-1", NotificationType.SlaBreach, "This claim's SLA has been breached.", caseId);

        var feed = await service.GetFeedAsync(20);
        Assert.Single(feed.Notifications);
        Assert.Equal(1, feed.UnreadCount);
    }

    [Fact]
    public async Task NotifyOnceAsync_DifferentCase_CreatesASeparateNotification()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(ContextFor("officer-1"), repository);

        await service.NotifyOnceAsync(Scope, "officer-1", NotificationType.SlaBreach, "breach 1", Guid.NewGuid());
        await service.NotifyOnceAsync(Scope, "officer-1", NotificationType.SlaBreach, "breach 2", Guid.NewGuid());

        var feed = await service.GetFeedAsync(20);
        Assert.Equal(2, feed.Notifications.Count);
    }

    private static ITenantContextAccessor ContextFor(string userId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, userId, ["ClaimsOfficer"]));
        return accessor;
    }
}

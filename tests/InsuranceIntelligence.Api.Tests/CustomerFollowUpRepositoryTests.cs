using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CustomerFollowUpRepositoryTests
{
    [Fact]
    public async Task SaveAndGet_AreTenantAndWorkspaceScoped()
    {
        var options = Options();
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope, CustomerFollowUpStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(1));
        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfCustomerFollowUpRepository(db);

        await repository.SaveAsync(task);

        var loaded = await repository.GetAsync(scope, task.TaskId);
        Assert.Equal(task.TaskId, loaded!.TaskId);
        Assert.Equal(task.CustomerId, loaded.CustomerId);
        Assert.Equal(task.OriginatingMessageId, loaded.OriginatingMessageId);
        Assert.Equal(task.Status, loaded.Status);
        Assert.Null(await repository.GetAsync(TenantScope.Create("insurer-a", "sales"), task.TaskId));
        Assert.Empty(await repository.ListAsync(TenantScope.Create("insurer-b", "claims")));
    }

    [Fact]
    public async Task TryClaimDue_ClaimsOnlyScheduledTasksAndOnlyOnce()
    {
        var options = Options();
        var scope = TenantScope.Create("insurer-a", "claims");
        var now = DateTimeOffset.UtcNow;
        var due = TaskFor(scope, CustomerFollowUpStatus.Scheduled, now.AddMinutes(-1));
        var future = TaskFor(scope, CustomerFollowUpStatus.Scheduled, now.AddHours(1));
        var review = TaskFor(scope, CustomerFollowUpStatus.PendingReview, now.AddMinutes(-2));
        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfCustomerFollowUpRepository(db);
        await repository.SaveAsync(due);
        await repository.SaveAsync(future);
        await repository.SaveAsync(review);

        var claimed = await repository.TryClaimDueAsync(scope, now);

        var item = Assert.Single(claimed);
        Assert.Equal(due.TaskId, item.TaskId);
        Assert.Equal(CustomerFollowUpStatus.Processing, item.Status);
        Assert.Empty(await repository.TryClaimDueAsync(scope, now));
        Assert.Equal(CustomerFollowUpStatus.Scheduled, (await repository.GetAsync(scope, future.TaskId))!.Status);
        Assert.Equal(CustomerFollowUpStatus.PendingReview, (await repository.GetAsync(scope, review.TaskId))!.Status);
    }

    [Fact]
    public async Task CustomerReply_CancelsOnlyThatRecipientsPendingTasks()
    {
        var options = Options();
        var scope = TenantScope.Create("insurer-a", "claims");
        var due = TaskFor(scope, CustomerFollowUpStatus.Scheduled, DateTimeOffset.UtcNow.AddDays(1));
        var otherRecipient = TaskFor(scope, CustomerFollowUpStatus.Scheduled, DateTimeOffset.UtcNow.AddDays(1)) with
        {
            TaskId = Guid.NewGuid(), RecipientId = "different-recipient", OriginatingMessageId = Guid.NewGuid().ToString("N"),
        };
        var processing = TaskFor(scope, CustomerFollowUpStatus.Processing, DateTimeOffset.UtcNow.AddDays(-1)) with
        {
            TaskId = Guid.NewGuid(), OriginatingMessageId = Guid.NewGuid().ToString("N"),
        };
        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfCustomerFollowUpRepository(db);
        await repository.SaveAsync(due);
        await repository.SaveAsync(otherRecipient);
        await repository.SaveAsync(processing);

        var cancelled = await repository.CancelPendingForRecipientAsync(
            scope, due.Channel, due.AccountId, due.RecipientId, DateTimeOffset.UtcNow, "customer replied");

        Assert.Equal(1, cancelled);
        Assert.Equal(CustomerFollowUpStatus.Cancelled, (await repository.GetAsync(scope, due.TaskId))!.Status);
        Assert.Equal(CustomerFollowUpStatus.Scheduled, (await repository.GetAsync(scope, otherRecipient.TaskId))!.Status);
        Assert.Equal(CustomerFollowUpStatus.Processing, (await repository.GetAsync(scope, processing.TaskId))!.Status);
    }

    [Theory]
    [InlineData(CustomerFollowUpStatus.PendingReview)]
    [InlineData(CustomerFollowUpStatus.Scheduled)]
    public async Task TryCancel_CancelsOnlyPendingOrScheduledTasksAndPersistsReason(CustomerFollowUpStatus status)
    {
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope, status, DateTimeOffset.UtcNow.AddDays(1));
        await using var db = new InsuranceIntelligenceDbContext(Options());
        var repository = new EfCustomerFollowUpRepository(db);
        await repository.SaveAsync(task);

        var cancelled = await repository.TryCancelAsync(scope, task.TaskId, DateTimeOffset.UtcNow, "Customer asked us to stop.");
        var reloaded = await repository.GetAsync(scope, task.TaskId);

        Assert.NotNull(cancelled);
        Assert.Equal(CustomerFollowUpStatus.Cancelled, cancelled.Status);
        Assert.Equal(CustomerFollowUpStatus.Cancelled, reloaded!.Status);
        Assert.Equal("Customer asked us to stop.", reloaded.CancellationReason);
        Assert.NotNull(reloaded.CancelledAt);
    }

    [Theory]
    [InlineData(CustomerFollowUpStatus.Processing)]
    [InlineData(CustomerFollowUpStatus.Sent)]
    [InlineData(CustomerFollowUpStatus.Cancelled)]
    [InlineData(CustomerFollowUpStatus.Failed)]
    public async Task TryCancel_DoesNotChangeProcessingOrTerminalTasks(CustomerFollowUpStatus status)
    {
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope, status, DateTimeOffset.UtcNow.AddDays(1));
        await using var db = new InsuranceIntelligenceDbContext(Options());
        var repository = new EfCustomerFollowUpRepository(db);
        await repository.SaveAsync(task);

        var cancelled = await repository.TryCancelAsync(scope, task.TaskId, DateTimeOffset.UtcNow, "Cancellation request.");

        Assert.Null(cancelled);
        Assert.Equal(status, (await repository.GetAsync(scope, task.TaskId))!.Status);
    }

    [Fact]
    public async Task TryCancel_DoesNotFindTaskInAnotherWorkspace()
    {
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope, CustomerFollowUpStatus.Scheduled, DateTimeOffset.UtcNow.AddDays(1));
        await using var db = new InsuranceIntelligenceDbContext(Options());
        var repository = new EfCustomerFollowUpRepository(db);
        await repository.SaveAsync(task);

        var cancelled = await repository.TryCancelAsync(TenantScope.Create("insurer-a", "sales"), task.TaskId, DateTimeOffset.UtcNow, "Wrong workspace.");

        Assert.Null(cancelled);
        Assert.Equal(CustomerFollowUpStatus.Scheduled, (await repository.GetAsync(scope, task.TaskId))!.Status);
    }

    private static DbContextOptions<InsuranceIntelligenceDbContext> Options() =>
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"follow-ups-{Guid.NewGuid():N}")
            .Options;

    private static CustomerFollowUpTask TaskFor(TenantScope scope, CustomerFollowUpStatus status, DateTimeOffset dueAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new CustomerFollowUpTask
        {
            TaskId = Guid.NewGuid(), Scope = scope, CustomerId = Guid.NewGuid().ToString(), Channel = "whatsapp",
            AccountId = "business-account", RecipientId = "recipient-1", ThreadId = "thread-1",
            OriginatingMessageId = Guid.NewGuid().ToString("N"), Purpose = "customer_requested_follow_up",
            TemplateKey = "customer-requested-follow-up", DueAtUtc = dueAt, TimeZoneId = "Africa/Kampala",
            PolicyVersion = 1, ApprovalMode = CustomerFollowUpMode.Review, Status = status,
            CreatedAt = now, UpdatedAt = now, CorrelationId = Guid.NewGuid().ToString("N"),
        };
    }
}

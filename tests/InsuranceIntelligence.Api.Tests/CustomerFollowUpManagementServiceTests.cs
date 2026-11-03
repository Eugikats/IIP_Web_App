using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CustomerRecord = InsuranceIntelligence.Domain.Customers.Customer;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CustomerFollowUpManagementServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsCustomerSafeMetadataWithoutProviderIdentifiers()
    {
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope);
        await using var db = new InsuranceIntelligenceDbContext(Options());
        var followUps = new EfCustomerFollowUpRepository(db);
        await followUps.SaveAsync(task);
        var service = Service(scope, followUps, new CapturingAuditRepository());

        var item = Assert.Single(await service.ListAsync());
        var serialized = JsonSerializer.Serialize(item);

        Assert.Equal("Synthetic Customer", item.CustomerName);
        Assert.Equal(task.TaskId, item.TaskId);
        Assert.DoesNotContain(task.RecipientId, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(task.ThreadId, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(task.OriginatingMessageId, serialized, StringComparison.Ordinal);
        Assert.True(item.CanCancel);
    }

    [Fact]
    public async Task CancelAsync_RequiresReasonAndWritesSafeAuditOnSuccess()
    {
        var scope = TenantScope.Create("insurer-a", "claims");
        var task = TaskFor(scope);
        await using var db = new InsuranceIntelligenceDbContext(Options());
        var followUps = new EfCustomerFollowUpRepository(db);
        await followUps.SaveAsync(task);
        var audit = new CapturingAuditRepository();
        var service = Service(scope, followUps, audit);

        await Assert.ThrowsAsync<CustomerFollowUpManagementValidationException>(
            () => service.CancelAsync(task.TaskId, " ", "correlation"));
        var result = await service.CancelAsync(task.TaskId, "Customer requested cancellation.", "correlation");
        var written = Assert.Single(audit.Events);

        Assert.Equal(CustomerFollowUpStatus.Cancelled.ToString(), result!.Status);
        Assert.Equal("customer_follow_up.cancelled", written.ActionName);
        Assert.Equal("Customer requested cancellation.", written.AuditReason);
        Assert.DoesNotContain(task.RecipientId, JsonSerializer.Serialize(written), StringComparison.Ordinal);
    }

    private static CustomerFollowUpManagementService Service(
        TenantScope scope,
        ICustomerFollowUpRepository followUps,
        IAuditRepository audit)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, "staff-1", []));
        return new CustomerFollowUpManagementService(
            accessor,
            new AllowCommunicationsPermission(),
            followUps,
            new CustomerRepository(scope),
            audit);
    }

    private static DbContextOptions<InsuranceIntelligenceDbContext> Options() =>
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"follow-up-management-{Guid.NewGuid():N}")
            .Options;

    private static CustomerFollowUpTask TaskFor(TenantScope scope)
    {
        var now = DateTimeOffset.UtcNow;
        return new CustomerFollowUpTask
        {
            TaskId = Guid.NewGuid(), Scope = scope, CustomerId = "customer-1", Channel = "whatsapp",
            AccountId = "provider-account-id", RecipientId = "provider-recipient-id", ThreadId = "provider-thread-id",
            OriginatingMessageId = "provider-origin-message-id", Purpose = "claim_status_follow_up",
            TemplateKey = "customer-requested-follow-up", DueAtUtc = now.AddDays(3), TimeZoneId = "Africa/Kampala",
            PolicyVersion = 1, ApprovalMode = CustomerFollowUpMode.Review, Status = CustomerFollowUpStatus.PendingReview,
            CreatedAt = now, UpdatedAt = now, CorrelationId = "correlation",
        };
    }

    private sealed class AllowCommunicationsPermission : IPermissionService
    {
        public Task<bool> HasPermissionAsync(InsuranceIntelligence.Domain.Administration.Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task RequirePermissionAsync(InsuranceIntelligence.Domain.Administration.Permission permission, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CustomerRepository(TenantScope scope) : ICustomerRepository
    {
        public Task<CustomerRecord?> GetAsync(string customerId, TenantScope requestedScope, CancellationToken cancellationToken) =>
            Task.FromResult<CustomerRecord?>(null);

        public Task<IReadOnlyCollection<CustomerRecord>> ListAsync(TenantScope requestedScope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<CustomerRecord>>(requestedScope.Matches(scope)
                ? [new CustomerRecord("customer-1", scope.TenantId, scope.WorkspaceId, "Synthetic Customer", null, null)]
                : []);

        public Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);

        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(item => item.Scope.Matches(scope)).ToArray());
    }
}

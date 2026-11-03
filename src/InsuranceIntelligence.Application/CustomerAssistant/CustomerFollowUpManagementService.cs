using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;

namespace InsuranceIntelligence.Application.CustomerAssistant;

public sealed record CustomerFollowUpQueueItem(
    Guid TaskId,
    string CustomerName,
    string Channel,
    string Purpose,
    DateTimeOffset DueAtUtc,
    string TimeZoneId,
    string ApprovalMode,
    string Status,
    DateTimeOffset CreatedAt,
    bool CanCancel);

public sealed record CustomerFollowUpCancellation(string Status, DateTimeOffset CancelledAt);

public interface ICustomerFollowUpManagementService
{
    Task<IReadOnlyList<CustomerFollowUpQueueItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<CustomerFollowUpCancellation?> CancelAsync(Guid taskId, string auditReason, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class CustomerFollowUpManagementService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICustomerFollowUpRepository followUpRepository,
    ICustomerRepository customerRepository,
    IAuditRepository auditRepository) : ICustomerFollowUpManagementService
{
    public async Task<IReadOnlyList<CustomerFollowUpQueueItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;
        var followUps = await followUpRepository.ListAsync(context.Scope, limit: 200, cancellationToken: cancellationToken);
        var customers = (await customerRepository.ListAsync(context.Scope, cancellationToken))
            .ToDictionary(customer => customer.CustomerId, StringComparer.Ordinal);

        return followUps
            .OrderBy(item => item.DueAtUtc)
            .Select(item => new CustomerFollowUpQueueItem(
                item.TaskId,
                customers.TryGetValue(item.CustomerId, out var customer) ? customer.FullName : "Customer record unavailable",
                item.Channel,
                item.Purpose,
                item.DueAtUtc,
                item.TimeZoneId,
                item.ApprovalMode.ToString(),
                item.Status.ToString(),
                item.CreatedAt,
                item.Status is CustomerFollowUpStatus.PendingReview or CustomerFollowUpStatus.Scheduled))
            .ToArray();
    }

    public async Task<CustomerFollowUpCancellation?> CancelAsync(
        Guid taskId, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        if (taskId == Guid.Empty) throw new CustomerFollowUpManagementValidationException("taskId", "A valid follow-up task is required.");
        var reason = auditReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new CustomerFollowUpManagementValidationException("auditReason", "A cancellation reason of 1 to 500 characters is required.");
        }

        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var cancelled = await followUpRepository.TryCancelAsync(context.Scope, taskId, now, reason, cancellationToken);
        if (cancelled is null)
        {
            var existing = await followUpRepository.GetAsync(context.Scope, taskId, cancellationToken);
            if (existing is null) return null;
            throw new CustomerFollowUpManagementConflictException("This follow-up is already processing or has reached a terminal state, so it cannot be cancelled.");
        }

        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), context.Scope, null, taskId.ToString("N"), context.UserId, ActorType.User,
            "customer_follow_up.cancelled", now, null, CustomerFollowUpStatus.Cancelled.ToString(),
            [taskId.ToString("N")], null, null, "cancelled", null, reason,
            string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId), cancellationToken);

        return new CustomerFollowUpCancellation(cancelled.Status.ToString(), now);
    }
}

public sealed class CustomerFollowUpManagementValidationException(string field, string message)
    : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

public sealed class CustomerFollowUpManagementConflictException(string message) : InvalidOperationException(message);

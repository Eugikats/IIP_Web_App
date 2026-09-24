using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Retention;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Retention;

/// <summary>Outcome O: "policies whose cover ends in 60, 30 and 14 days, scored by claim
/// experience, premium change and time since last contact. Reminder drafts prepared for approval,
/// never auto sent, consent gated." Sending reuses the same ISmsProvider/IEmailSendingProvider the
/// rest of the platform sends through (Outcome M's universal choke point), so consent enforcement
/// applies here automatically rather than being reimplemented.</summary>
public sealed class RetentionWatchService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IPolicyRepository policyRepository,
    ICustomerRepository customerRepository,
    ICaseRepository caseRepository,
    ICommunicationRepository communicationRepository,
    IRetentionReminderRepository reminderRepository,
    ISmsProvider smsProvider,
    IEmailSendingProvider emailSendingProvider,
    IAuditWriter auditWriter) : IRetentionWatchService
{
    private static readonly int[] ExpiryBuckets = [14, 30, 60];

    public async Task<RetentionWatchSummaryDto> GetWatchAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCustomers, cancellationToken);
        var context = tenantContextAccessor.Current;

        var policies = await policyRepository.ListAsync(context.Scope, cancellationToken);
        var cases = await caseRepository.ListAsync(context.Scope, cancellationToken);
        var communications = await communicationRepository.ListAsync(context.Scope, cancellationToken);
        var reminders = await reminderRepository.ListAsync(context.Scope, cancellationToken);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);

        var claimCountByPolicyNumber = cases
            .GroupBy(claimCase => claimCase.PolicyNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var lastContactByCustomerId = communications
            .Where(communication => !string.IsNullOrWhiteSpace(communication.CustomerId))
            .GroupBy(communication => communication.CustomerId!)
            .ToDictionary(group => group.Key, group => group.Max(communication => communication.CreatedAt));

        var latestReminderByPolicyId = reminders
            .GroupBy(reminder => reminder.PolicyId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(reminder => reminder.CreatedAt).First());

        var items = new List<RetentionWatchItemDto>();
        foreach (var policy in policies)
        {
            if (policy.EffectiveTo is not { } expiryDate)
            {
                continue;
            }

            var daysToExpiry = expiryDate.DayNumber - today.DayNumber;
            if (daysToExpiry < 0 || daysToExpiry > 60)
            {
                continue;
            }

            var bucket = ExpiryBuckets.First(candidate => daysToExpiry <= candidate);
            var claimCount = claimCountByPolicyNumber.GetValueOrDefault(policy.PolicyNumber, 0);
            int? daysSinceLastContact = policy.CustomerId is { } customerId && lastContactByCustomerId.TryGetValue(customerId, out var lastContact)
                ? (int)(DateTimeOffset.UtcNow - lastContact).TotalDays
                : null;

            var customer = policy.CustomerId is null ? null : await customerRepository.GetAsync(policy.CustomerId, context.Scope, cancellationToken);
            var score = AttentionScore(bucket, claimCount, daysSinceLastContact);
            var latestReminder = latestReminderByPolicyId.GetValueOrDefault(policy.PolicyId);

            items.Add(new RetentionWatchItemDto(
                policy.PolicyId,
                policy.PolicyNumber,
                policy.ProductLine,
                policy.CustomerId ?? string.Empty,
                customer?.FullName ?? "Unknown customer",
                expiryDate,
                daysToExpiry,
                bucket,
                claimCount,
                daysSinceLastContact,
                policy.AnnualPremium,
                policy.PremiumCurrency,
                score,
                latestReminder?.Status.ToString(),
                latestReminder?.ReminderId.ToString()));
        }

        var ranked = items.OrderByDescending(item => item.AttentionScore).ThenBy(item => item.DaysToExpiry).ToArray();
        var premiumAtRisk = ranked.Sum(item => item.AnnualPremium ?? 0m);
        var currency = ranked.Select(item => item.PremiumCurrency).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "UGX";

        return new RetentionWatchSummaryDto(ranked.Length, premiumAtRisk, currency, ranked);
    }

    public async Task<RetentionReminderDto> DraftReminderAsync(string policyId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;

        var policy = await policyRepository.GetAsync(policyId, context.Scope, cancellationToken);
        if (policy is null)
        {
            throw new RetentionWatchValidationException("policyId", "This policy was not found for the current tenant workspace.");
        }

        if (policy.EffectiveTo is not { } expiryDate)
        {
            throw new RetentionWatchValidationException("policyId", "This policy has no recorded cover-end date to remind about.");
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var daysToExpiry = expiryDate.DayNumber - today.DayNumber;
        if (daysToExpiry < 0 || daysToExpiry > 60)
        {
            throw new RetentionWatchValidationException("policyId", "This policy is not within the 60-day retention window.");
        }

        var customer = policy.CustomerId is null ? null : await customerRepository.GetAsync(policy.CustomerId, context.Scope, cancellationToken);
        if (customer is null)
        {
            throw new RetentionWatchValidationException("policyId", "No customer record is linked to this policy, so a reminder cannot be addressed.");
        }

        var (channel, recipient) = ResolveChannel(customer);
        if (recipient is null)
        {
            throw new RetentionWatchValidationException("policyId", "This customer has no phone or email on file to send a reminder to.");
        }

        var bucket = ExpiryBuckets.First(candidate => daysToExpiry <= candidate);
        var now = DateTimeOffset.UtcNow;
        var reminder = new RetentionReminderDraft
        {
            ReminderId = Guid.NewGuid(),
            Scope = context.Scope,
            PolicyId = policy.PolicyId,
            PolicyNumber = policy.PolicyNumber,
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            ExpiryBucketDays = bucket,
            Channel = channel,
            Recipient = recipient,
            MessageBody = ComposeMessage(customer.FullName, policy, expiryDate),
            Status = RetentionReminderStatus.PendingApproval,
            CreatedByUserId = context.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        await reminderRepository.SaveAsync(reminder, cancellationToken);
        await auditWriter.WriteAsync(AuditEvent.RetentionReminderDrafted(context.Scope, context.UserId, reminder.ReminderId, policy.PolicyNumber, bucket, reminder.CorrelationId), cancellationToken);

        return ToDto(reminder);
    }

    public async Task<RetentionReminderDto> ApproveAndSendAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;

        var reminder = await reminderRepository.GetAsync(reminderId, context.Scope, cancellationToken);
        if (reminder is null)
        {
            throw new RetentionWatchValidationException("reminderId", "This reminder was not found for the current tenant workspace.");
        }

        if (reminder.Status != RetentionReminderStatus.PendingApproval)
        {
            throw new RetentionWatchValidationException("reminderId", $"This reminder is '{reminder.Status}' and cannot be approved and sent again.");
        }

        var request = new CommunicationSendRequest(
            context.Scope,
            CaseId: null,
            reminder.Channel,
            reminder.Recipient,
            reminder.MessageBody,
            context.UserId,
            Approved: true,
            LowRisk: false,
            reminder.CorrelationId);

        var result = reminder.Channel == CommunicationChannel.Email
            ? await emailSendingProvider.SendEmailAsync(request, cancellationToken)
            : await smsProvider.SendAsync(request, cancellationToken);

        var status = result.DeliveryStatus switch
        {
            "Sent" => RetentionReminderStatus.Sent,
            "ConsentRequired" => RetentionReminderStatus.ConsentRequired,
            "ConfigurationRequired" => RetentionReminderStatus.ConfigurationRequired,
            _ => RetentionReminderStatus.Failed
        };

        var now = DateTimeOffset.UtcNow;
        var updated = reminder with
        {
            Status = status,
            DeliveryStatus = result.DeliveryStatus,
            ApprovedByUserId = context.UserId,
            ApprovedAt = now,
            SentAt = status == RetentionReminderStatus.Sent ? now : null,
            UpdatedAt = now
        };

        await reminderRepository.SaveAsync(updated, cancellationToken);
        await auditWriter.WriteAsync(AuditEvent.RetentionReminderSent(context.Scope, context.UserId, updated.ReminderId, updated.PolicyNumber, status.ToString(), updated.CorrelationId), cancellationToken);

        return ToDto(updated);
    }

    /// <summary>Deterministic, documented weights (AGENTS.md non-negotiable 2: real Domain fields,
    /// never a model call). Expiry proximity weighs most because it is the one signal that is
    /// certain and time-bound; claim experience and contact recency add context on top.</summary>
    private static int AttentionScore(int expiryBucketDays, int claimCount, int? daysSinceLastContact)
    {
        var expiryWeight = expiryBucketDays switch { 14 => 3, 30 => 2, _ => 1 };
        var claimWeight = claimCount switch { 0 => 0, 1 => 1, _ => 2 };
        var contactWeight = daysSinceLastContact switch
        {
            null => 2,
            <= 30 => 0,
            <= 90 => 1,
            _ => 2
        };

        return (expiryWeight * 3) + (claimWeight * 2) + contactWeight;
    }

    private static (CommunicationChannel Channel, string? Recipient) ResolveChannel(Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            return (CommunicationChannel.SMS, customer.Phone);
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            return (CommunicationChannel.Email, customer.Email);
        }

        return (CommunicationChannel.SMS, null);
    }

    private static string ComposeMessage(string customerName, Policy policy, DateOnly expiryDate) =>
        $"Hello {customerName}, your {policy.ProductLine} policy {policy.PolicyNumber} is due for renewal on " +
        $"{expiryDate:d MMM yyyy}. Contact us to renew and stay covered.";

    private static RetentionReminderDto ToDto(RetentionReminderDraft reminder) =>
        new(
            reminder.ReminderId.ToString(),
            reminder.PolicyId,
            reminder.PolicyNumber,
            reminder.CustomerName,
            reminder.ExpiryBucketDays,
            reminder.Channel.ToString(),
            reminder.Recipient,
            reminder.MessageBody,
            reminder.Status.ToString(),
            reminder.DeliveryStatus,
            reminder.CreatedAt,
            reminder.SentAt);
}

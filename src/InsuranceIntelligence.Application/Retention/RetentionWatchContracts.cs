using InsuranceIntelligence.Domain.Retention;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Retention;

/// <summary>Outcome O: "policies whose cover ends in 60, 30 and 14 days, scored by claim
/// experience, premium change and time since last contact." PremiumChangePercent is honestly null
/// for every policy today: Policy carries only its current AnnualPremium, no premium history, so a
/// real percentage change cannot be computed without fabricating one. The score below is built from
/// the two signals that are real (days to expiry, claim experience, contact recency); premium
/// change joins the score once a premium history exists to compute it from.</summary>
public sealed record RetentionWatchItemDto(
    string PolicyId,
    string PolicyNumber,
    string ProductLine,
    string CustomerId,
    string CustomerName,
    DateOnly ExpiryDate,
    int DaysToExpiry,
    int ExpiryBucketDays,
    int ClaimCountLast12Months,
    int? DaysSinceLastContact,
    decimal? AnnualPremium,
    string? PremiumCurrency,
    int AttentionScore,
    string? LatestReminderStatus,
    string? LatestReminderId);

public sealed record RetentionWatchSummaryDto(
    int PoliciesAtRisk,
    decimal PremiumAtRisk,
    string PremiumAtRiskCurrency,
    IReadOnlyList<RetentionWatchItemDto> Items);

public sealed record RetentionReminderDto(
    string ReminderId,
    string PolicyId,
    string PolicyNumber,
    string CustomerName,
    int ExpiryBucketDays,
    string Channel,
    string Recipient,
    string MessageBody,
    string Status,
    string? DeliveryStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

public interface IRetentionWatchService
{
    Task<RetentionWatchSummaryDto> GetWatchAsync(CancellationToken cancellationToken = default);

    Task<RetentionReminderDto> DraftReminderAsync(string policyId, CancellationToken cancellationToken = default);

    Task<RetentionReminderDto> ApproveAndSendAsync(Guid reminderId, CancellationToken cancellationToken = default);
}

public sealed class RetentionWatchValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

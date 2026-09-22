namespace InsuranceIntelligence.Application.Customers;

public sealed record CustomerConsentRecordDto(
    Guid RecordId,
    string Basis,
    string Purpose,
    string Channel,
    DateTimeOffset RecordedAt,
    string RecordedByUserId,
    DateTimeOffset? WithdrawnAt,
    string? InteractionReference,
    bool IsCurrentlyPermitted);

public interface ICustomerConsentService
{
    Task<IReadOnlyList<CustomerConsentRecordDto>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>Records an explicit opt-in for one of the platform's known purposes ("whatsapp_messaging",
    /// "sms_messaging", "email_messaging"; see InsuranceIntelligence.Domain.Compliance.ConsentPurposes),
    /// e.g. a customer confirmed by phone. The customer must already have an email or phone on file
    /// for the channel that purpose implies.</summary>
    Task<CustomerConsentRecordDto> RecordExplicitOptInAsync(string customerId, string purpose, CancellationToken cancellationToken = default);

    /// <summary>Records a withdrawal: a new row, never an edit of the grant it supersedes, so the
    /// full history including exactly when consent was withdrawn is always reconstructable.</summary>
    Task<CustomerConsentRecordDto> WithdrawAsync(string customerId, string purpose, CancellationToken cancellationToken = default);
}

public sealed class CustomerConsentValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

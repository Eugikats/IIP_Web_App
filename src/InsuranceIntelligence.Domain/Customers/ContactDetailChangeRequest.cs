using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Customers;

/// <summary>Lifecycle of a policyholder-initiated request to change their contact details. Build
/// prompt outcome L: "request a change to their contact details for staff approval." Mirrors
/// InsuranceIntelligence.Domain.Contestability.ClaimReviewRequest's shape exactly, the same
/// customer-initiates/staff-resolves pattern, since a contact-detail edit is never applied
/// directly by the claimant: it only ever proposes new values for a person to accept.</summary>
public enum ContactDetailChangeStatus
{
    Open,
    Acknowledged,
    Resolved,
}

public sealed record ContactDetailChangeRequest(
    Guid RequestId,
    TenantScope Scope,
    string PolicyNumber,
    Guid? CaseId,
    string RequestedByUserId,
    string? RequestedEmail,
    string? RequestedPhone,
    string? Note,
    ContactDetailChangeStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByUserId,
    string? ResolutionNote,
    string CorrelationId)
{
    public const int MaxNoteLength = 1000;

    public static ContactDetailChangeRequest Create(
        TenantScope scope,
        string policyNumber,
        Guid? caseId,
        string requestedByUserId,
        string? requestedEmail,
        string? requestedPhone,
        string? note,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(requestedEmail) && string.IsNullOrWhiteSpace(requestedPhone))
        {
            throw new ArgumentException("A new email address or phone number is required.", nameof(requestedEmail));
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > MaxNoteLength })
        {
            trimmedNote = trimmedNote[..MaxNoteLength];
        }

        return new ContactDetailChangeRequest(
            Guid.NewGuid(),
            scope,
            policyNumber,
            caseId,
            string.IsNullOrWhiteSpace(requestedByUserId) ? "client-portal-user" : requestedByUserId,
            string.IsNullOrWhiteSpace(requestedEmail) ? null : requestedEmail.Trim(),
            string.IsNullOrWhiteSpace(requestedPhone) ? null : requestedPhone.Trim(),
            trimmedNote,
            ContactDetailChangeStatus.Open,
            DateTimeOffset.UtcNow,
            ResolvedAt: null,
            ResolvedByUserId: null,
            ResolutionNote: null,
            correlationId);
    }

    public ContactDetailChangeRequest Acknowledge() =>
        Status == ContactDetailChangeStatus.Open
            ? this with { Status = ContactDetailChangeStatus.Acknowledged }
            : this;

    public ContactDetailChangeRequest Resolve(string resolvedByUserId, string? resolutionNote) =>
        Status == ContactDetailChangeStatus.Resolved
            ? this
            : this with
            {
                Status = ContactDetailChangeStatus.Resolved,
                ResolvedAt = DateTimeOffset.UtcNow,
                ResolvedByUserId = resolvedByUserId,
                ResolutionNote = resolutionNote,
            };
}

public interface IContactDetailChangeRequestRepository
{
    Task SaveAsync(ContactDetailChangeRequest request, CancellationToken cancellationToken = default);

    Task<ContactDetailChangeRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContactDetailChangeRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

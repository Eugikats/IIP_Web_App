using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Compliance;

/// <summary>Everything this platform actually holds about one customer (blueprint §14 Phase 6, WO
/// 6.4's export right), the same aggregate CustomerEndpoints' Customer 360 view already builds,
/// extended with the two data classes this session added (ConsentRecords, conversation content)
/// that a subject-access request must also cover.</summary>
public sealed record DataSubjectExportDto(
    Customer Customer,
    IReadOnlyCollection<Policy> Policies,
    IReadOnlyCollection<ClaimCase> Cases,
    IReadOnlyCollection<Communication> Communications,
    IReadOnlyCollection<ConsentRecord> ConsentHistory,
    IReadOnlyCollection<CustomerConversationTurn> ConversationHistory);

public interface IDataSubjectRightsService
{
    /// <summary>whatsAppBusinessPhoneNumberId is optional and, like
    /// MissingDocumentRequestService's identical parameter, resolved and passed in by the Api-layer
    /// caller (IWhatsAppRuntimeSettingsProvider is an Infrastructure-layer provider this Application
    /// service cannot reference directly), without it, WhatsApp conversation history cannot be
    /// located (it is keyed by business-number + customer-wa-id, not customer id) and the export
    /// honestly omits it rather than guessing.</summary>
    Task<DataSubjectExportDto?> ExportAsync(TenantScope scope, string customerId, string? whatsAppBusinessPhoneNumberId, CancellationToken cancellationToken = default);

    Task<Customer?> RectifyAsync(
        TenantScope scope, string customerId, DataSubjectRectificationRequest corrections,
        string actorUserId, string auditReason, string correlationId, CancellationToken cancellationToken = default);

    Task<DataSubjectErasureResult?> RequestErasureAsync(
        TenantScope scope, string customerId, string actorUserId, string auditReason, string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class DataSubjectRightsService(
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository,
    ICaseRepository caseRepository,
    ICommunicationRepository communicationRepository,
    IConsentRepository consentRepository,
    ICustomerConversationRepository conversationRepository,
    IAuditRepository auditRepository) : IDataSubjectRightsService
{
    // A case is "still open" for erasure-refusal purposes using the same terminal-status set
    // CaseAssignmentPolicy already established (see its OpenStatuses), Settled/Closed are the
    // only two statuses this codebase treats as finished.
    private static readonly CaseStatus[] TerminalStatuses = [CaseStatus.Settled, CaseStatus.Closed];

    public async Task<DataSubjectExportDto?> ExportAsync(TenantScope scope, string customerId, string? whatsAppBusinessPhoneNumberId, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetAsync(customerId, scope, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var policies = await policyRepository.ListByCustomerAsync(customerId, scope, cancellationToken);
        var matchingCases = await FindMatchingCasesAsync(scope, customer, cancellationToken);
        var caseIds = matchingCases.Select(c => c.CaseId).ToHashSet();
        var communications = (await communicationRepository.ListAsync(scope, cancellationToken))
            .Where(c => caseIds.Contains(c.CaseId))
            .ToArray();

        var consentHistory = string.IsNullOrWhiteSpace(customer.Phone)
            ? []
            : await consentRepository.ListForCustomerAsync(scope, NormalizePhone(customer.Phone), cancellationToken);

        // Conversation turns are keyed by thread (business number + customer wa id), not customer
        // id directly, without a resolved business number id this data class is honestly omitted
        // rather than guessed at; WhatsApp is the only channel that captures it today.
        var conversationHistory = !string.IsNullOrWhiteSpace(customer.Phone) && !string.IsNullOrWhiteSpace(whatsAppBusinessPhoneNumberId)
            ? await conversationRepository.ListForThreadAsync(
                scope,
                WhatsAppMessagingService.BuildThreadId(whatsAppBusinessPhoneNumberId, NormalizePhone(customer.Phone)),
                cancellationToken)
            : [];

        return new DataSubjectExportDto(customer, policies, matchingCases, communications, consentHistory, conversationHistory);
    }

    public async Task<Customer?> RectifyAsync(
        TenantScope scope, string customerId, DataSubjectRectificationRequest corrections,
        string actorUserId, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetAsync(customerId, scope, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var rectified = customer with
        {
            FullName = string.IsNullOrWhiteSpace(corrections.FullName) ? customer.FullName : corrections.FullName.Trim(),
            Email = corrections.Email is null ? customer.Email : (string.IsNullOrWhiteSpace(corrections.Email) ? null : corrections.Email.Trim()),
            Phone = corrections.Phone is null ? customer.Phone : (string.IsNullOrWhiteSpace(corrections.Phone) ? null : corrections.Phone.Trim()),
        };
        await customerRepository.UpsertAsync(rectified, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, actorUserId, ActorType.User,
                "data_subject.rectified", DateTimeOffset.UtcNow,
                null, null, [], null, null,
                "rectified", null,
                $"Customer {customerId}'s record was corrected at the data subject's request. {auditReason}",
                correlationId),
            cancellationToken);

        return rectified;
    }

    public async Task<DataSubjectErasureResult?> RequestErasureAsync(
        TenantScope scope, string customerId, string actorUserId, string auditReason, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetAsync(customerId, scope, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var matchingCases = await FindMatchingCasesAsync(scope, customer, cancellationToken);
        var openCases = matchingCases.Where(c => !TerminalStatuses.Contains(c.Status)).ToArray();
        var now = DateTimeOffset.UtcNow;

        if (openCases.Length > 0)
        {
            var reason = $"Cannot erase: {openCases.Length} case(s) tied to this customer are still open " +
                $"({string.Join(", ", openCases.Select(c => c.Status))}). Insurance claim records must be retained while a claim is active.";

            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, null, null, actorUserId, ActorType.User,
                    "data_subject.erasure_refused", now,
                    null, null, [], null, null,
                    "refused", null,
                    $"Erasure request for customer {customerId} refused: {reason} Requestor reason: {auditReason}",
                    correlationId),
                cancellationToken);

            return new DataSubjectErasureResult(customerId, DataSubjectErasureOutcome.RefusedActiveCase, reason, now);
        }

        // Anonymize the identifying fields, never delete the customer row outright, and never
        // touch case/policy/communication records: those carry their own retention obligations
        // independent of this request, and this platform's compliance posture depends on the case
        // record continuing to exist even after the customer who filed it is erased.
        var anonymized = customer with { FullName = "Erased customer", Email = null, Phone = null };
        await customerRepository.UpsertAsync(anonymized, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, actorUserId, ActorType.User,
                "data_subject.erased", now,
                null, null, [], null, null,
                "erased", null,
                $"Customer {customerId}'s identifying fields were anonymized at the data subject's request. {auditReason}",
                correlationId),
            cancellationToken);

        return new DataSubjectErasureResult(customerId, DataSubjectErasureOutcome.Erased, "Customer's identifying fields were anonymized.", now);
    }

    // Same heuristic CustomerEndpoints' existing Customer 360 view already uses (name/email match)
    //, an acknowledged, pre-existing limitation ("client-side filtering for MVP" in that file's
    // own comment), not one this service introduces.
    private async Task<IReadOnlyCollection<ClaimCase>> FindMatchingCasesAsync(TenantScope scope, Customer customer, CancellationToken cancellationToken)
    {
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        return cases
            .Where(c => c.CustomerName == customer.FullName
                || (!string.IsNullOrWhiteSpace(customer.Email) && c.CustomerEmail == customer.Email))
            .ToArray();
    }

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
}

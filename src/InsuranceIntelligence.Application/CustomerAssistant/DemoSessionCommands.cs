using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>Whether the hidden demo commands are live, and for which number. Enabled only in the
/// Demo runtime profile, and only ever for the one configured demo phone.</summary>
public sealed record DemoSessionSettings(bool Enabled, string? DemoPhoneNumber)
{
    public static readonly DemoSessionSettings Disabled = new(false, null);

    public bool Allows(string senderWaId) =>
        Enabled && !string.IsNullOrWhiteSpace(DemoPhoneNumber) && PhoneNumbers.SameNumber(senderWaId, DemoPhoneNumber);
}

/// <summary>Loads the demo policyholder onto the demo phone (the hidden "#demo" command), so the
/// claim journey can be shown after "#reset" has made the phone a stranger again.</summary>
public interface IDemoPolicyholderLoader
{
    Task<string> LoadAsync(TenantScope scope, string senderWaId, string correlationId, CancellationToken cancellationToken);
}

public enum DemoCommand
{
    None,
    Reset,
    LoadPolicyholder,
}

/// <summary>
/// Hidden commands for field testing on one shared phone. "#reset" returns the demo number to a
/// stranger: every customer record on it is anonymised (the same effect as a data-subject erasure),
/// its language preference is forgotten, its open claims are closed with an audit reason, and the
/// caller marks the conversation so earlier turns are ignored. "#demo" loads the demo policyholder.
///
/// Exact match only, the demo number only, the Demo runtime profile only. From any other number, or
/// in any other profile, the text is an ordinary message and nothing here runs.
/// </summary>
public sealed class DemoSessionCommands(
    DemoSessionSettings settings,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository,
    ICaseRepository caseRepository,
    ICustomerLanguagePreferenceRepository languagePreferenceRepository,
    IAuditRepository auditRepository,
    IDemoPolicyholderLoader? policyholderLoader = null)
{
    public const string ResetCommand = "#reset";
    public const string LoadPolicyholderCommand = "#demo";
    private const string ActorId = "system:demo-session-reset";

    public DemoCommand Recognise(string senderWaId, string messageText)
    {
        if (!settings.Allows(senderWaId))
        {
            return DemoCommand.None;
        }

        var text = messageText.Trim();
        if (string.Equals(text, ResetCommand, StringComparison.Ordinal))
        {
            return DemoCommand.Reset;
        }

        return string.Equals(text, LoadPolicyholderCommand, StringComparison.Ordinal) && policyholderLoader is not null
            ? DemoCommand.LoadPolicyholder
            : DemoCommand.None;
    }

    public Task<string> RunAsync(DemoCommand command, TenantScope scope, string senderWaId, string correlationId, CancellationToken cancellationToken) =>
        command switch
        {
            DemoCommand.Reset => ResetAsync(scope, senderWaId, correlationId, cancellationToken),
            DemoCommand.LoadPolicyholder => policyholderLoader!.LoadAsync(scope, senderWaId, correlationId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a demo command."),
        };

    private async Task<string> ResetAsync(TenantScope scope, string senderWaId, string correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var customers = (await customerRepository.ListAsync(scope, cancellationToken))
            .Where(customer => PhoneNumbers.SameNumber(customer.Phone, senderWaId))
            .ToArray();

        // Collected before anonymising: once the phone is cleared, these policies no longer point
        // back to this number, and their claims would be missed.
        var policyNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var customer in customers)
        {
            foreach (var policy in await policyRepository.ListByCustomerAsync(customer.CustomerId, scope, cancellationToken))
            {
                policyNumbers.Add(policy.PolicyNumber);
            }
        }

        var openClaims = (await caseRepository.ListAsync(scope, cancellationToken))
            .Where(claim => claim.Status is not (CaseStatus.Settled or CaseStatus.Closed))
            .Where(claim => PhoneNumbers.SameNumber(claim.CustomerPhone, senderWaId)
                || (!string.IsNullOrWhiteSpace(claim.PolicyNumber) && policyNumbers.Contains(claim.PolicyNumber)))
            .ToArray();

        foreach (var claim in openClaims)
        {
            await caseRepository.SaveAsync(claim with { Status = CaseStatus.Closed, UpdatedAt = now }, cancellationToken);
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, claim.CaseId, null, ActorId, ActorType.Integration,
                    "case.closed", now, claim.Status.ToString(), CaseStatus.Closed.ToString(),
                    [], "DemoSessionReset", null, "accepted", null,
                    "Closed by the demo phone reset between field-test participants. Not a claim decision.",
                    correlationId),
                cancellationToken);
        }

        foreach (var customer in customers)
        {
            await customerRepository.UpsertAsync(customer with { FullName = "Erased customer", Email = null, Phone = null }, cancellationToken);
        }

        await languagePreferenceRepository.ClearAsync(scope, senderWaId, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, ActorId, ActorType.Integration,
                "demo.session_reset", now, null, null,
                [], "DemoSessionReset", null, "recorded", null,
                $"Demo phone reset: {customers.Length} customer record(s) anonymised, {openClaims.Length} open claim(s) closed, language preference cleared.",
                correlationId),
            cancellationToken);

        return $"Reset done. {customers.Length} customer record(s) cleared and {openClaims.Length} open claim(s) closed. The next person can start with any message.";
    }
}

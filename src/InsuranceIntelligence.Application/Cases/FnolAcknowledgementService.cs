using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Cases;

/// <summary>What actually happened when the platform tried to acknowledge a new claim — always
/// truthful, never a fabricated "sent" when nothing went out (blueprint §14 Phase 5, WO 5.2).</summary>
public enum FnolAcknowledgementOutcome
{
    /// <summary>Actually delivered to the sending provider.</summary>
    Sent,

    /// <summary>WhatsApp-originated claims are acknowledged inline by CustomerAssistantService's
    /// own reply — sending a second, separate acknowledgement here would double-notify the
    /// customer over the same channel for the same event.</summary>
    SkippedAlreadyAcknowledgedInline,

    /// <summary>Neither a phone number nor an email address is on file for this claim.</summary>
    NoRecipientAvailable,

    /// <summary>A channel and recipient were resolved, but the send did not go out — could be
    /// pending human approval (the tenant has not opted into WhatsAppOptions/AfricasTalkingOptions-
    /// style auto-send for low-risk transactional messages), a provider that is not configured, or
    /// a provider-side failure. The specific reason is in the audit trail, not swallowed.</summary>
    NotSent,
}

public interface IFnolAcknowledgementService
{
    /// <summary>Sends (or honestly declines to send) a low-risk transactional acknowledgement for
    /// a newly created claim: a reference number plus the required-documents checklist, over
    /// whichever channel the claim actually has a contact for. Never throws — a failure to
    /// acknowledge must not fail claim creation, which has already happened by the time this
    /// runs.</summary>
    Task<FnolAcknowledgementOutcome> SendAsync(ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class FnolAcknowledgementService(
    IDocumentChecklistProvider checklistProvider,
    ICommunicationSendingProvider sendingProvider,
    ICommunicationRepository communicationRepository,
    IAuditRepository auditRepository) : IFnolAcknowledgementService
{
    private const string SystemActorId = "system:fnol-acknowledgement";

    public async Task<FnolAcknowledgementOutcome> SendAsync(ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(claimCase.Channel, "WhatsApp", StringComparison.OrdinalIgnoreCase))
        {
            return FnolAcknowledgementOutcome.SkippedAlreadyAcknowledgedInline;
        }

        var (channel, recipient) = ResolveRecipient(claimCase);
        if (channel is null || recipient is null)
        {
            await WriteAuditAsync(claimCase, "NoRecipientAvailable", correlationId, cancellationToken);
            return FnolAcknowledgementOutcome.NoRecipientAvailable;
        }

        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, [], cancellationToken);
        var messageBody = ComposeMessage(claimCase, checklist);

        var request = new CommunicationSendRequest(
            claimCase.Scope,
            claimCase.CaseId,
            channel.Value,
            recipient,
            messageBody,
            SystemActorId,
            Approved: false,
            LowRisk: true, // FNOL acknowledgement is the exact case AutoSendTransactionalSmsEnabled's doc comment names.
            correlationId);

        var result = channel.Value == CommunicationChannel.SMS
            ? await sendingProvider.SendSmsAsync(request, cancellationToken)
            : await sendingProvider.SendEmailAsync(request, cancellationToken);

        if (result.DeliveryStatus != "Sent")
        {
            await WriteAuditAsync(claimCase, result.DeliveryStatus, correlationId, cancellationToken);
            return FnolAcknowledgementOutcome.NotSent;
        }

        var communication = new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            Channel = channel.Value,
            Direction = "Outbound",
            MessageBody = messageBody,
            TemplateId = "fnol-acknowledgement",
            SentBy = SystemActorId,
            DeliveryStatus = result.DeliveryStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            ProviderMessageId = string.IsNullOrWhiteSpace(result.ProviderMessageId) ? null : result.ProviderMessageId,
        };
        await communicationRepository.SaveAsync(communication, cancellationToken);
        await WriteAuditAsync(claimCase, "Sent", correlationId, cancellationToken, communication.CommunicationId);

        return FnolAcknowledgementOutcome.Sent;
    }

    private static (CommunicationChannel? Channel, string? Recipient) ResolveRecipient(ClaimCase claimCase)
    {
        if (!string.IsNullOrWhiteSpace(claimCase.CustomerPhone))
        {
            return (CommunicationChannel.SMS, claimCase.CustomerPhone);
        }

        if (!string.IsNullOrWhiteSpace(claimCase.CustomerEmail))
        {
            return (CommunicationChannel.Email, claimCase.CustomerEmail);
        }

        return (null, null);
    }

    private static string ComposeMessage(ClaimCase claimCase, DocumentCompletenessChecklist checklist)
    {
        var reference = claimCase.CaseId.ToString()[..8].ToUpperInvariant();
        var required = checklist.RequiredDocuments.Count == 0
            ? null
            : string.Join(", ", checklist.RequiredDocuments.Select(StaticDocumentTypeLabel.For));

        return required is null
            ? $"Thank you, {claimCase.CustomerName}. Your claim has been received. Reference: {reference}. We will contact you with next steps."
            : $"Thank you, {claimCase.CustomerName}. Your claim has been received. Reference: {reference}. Please have ready: {required}. We will contact you with next steps.";
    }

    private Task WriteAuditAsync(ClaimCase claimCase, string outcome, string correlationId, CancellationToken cancellationToken, Guid? communicationId = null) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), claimCase.Scope, claimCase.CaseId, communicationId?.ToString(), SystemActorId, ActorType.Integration,
                "fnol.acknowledgement", DateTimeOffset.UtcNow,
                null, null, communicationId is null ? [] : [communicationId.Value.ToString()], "FnolAcknowledgementService", null,
                outcome, null,
                $"FNOL acknowledgement for case {claimCase.CaseId}: {outcome}.",
                correlationId),
            cancellationToken);
}

/// <summary>Document-type display labels, shared with the claimant-portal checklist rendering
/// (see StaticDocumentChecklistProvider.LabelFor in the Api layer) — duplicated rather than
/// referenced because Application must not depend on the Api project; keep the two in sync by
/// hand if a new DocumentType is ever added.</summary>
internal static class StaticDocumentTypeLabel
{
    public static string For(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "Claim form",
            DocumentType.PoliceAbstract => "Police abstract",
            DocumentType.Invoice => "Invoice",
            DocumentType.MedicalReport => "Medical report",
            DocumentType.PhotoEvidence => "Photo evidence",
            DocumentType.CustomerId => "Customer ID",
            DocumentType.GarageEstimate => "Garage estimate",
            _ => "Supporting document",
        };
}

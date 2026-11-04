using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Cases;

/// <summary>What happened when a status change was announced to the customer.</summary>
public enum StatusNotificationOutcome
{
    /// <summary>The status did not change, so nothing was said.</summary>
    Unchanged,

    /// <summary>The workspace has not turned on status messages.</summary>
    SwitchedOff,

    /// <summary>This status sends no message of its own (see <see cref="ClaimStatusNotificationPolicy.SendsMessage"/>).</summary>
    NoMessageForStatus,

    /// <summary>There is no phone or email on the claim to send to.</summary>
    NoRecipient,

    /// <summary>The message is written and waiting for a person to approve it.</summary>
    QueuedForApproval,

    /// <summary>The message went out without a person, because the workspace allows that for this kind of message.</summary>
    Sent,

    /// <summary>The message could not go out (not connected, no consent, no approved WhatsApp template, provider refused). It is kept, with the reason, for a person.</summary>
    NotSent,
}

public interface IClaimStatusNotifier
{
    /// <summary>Tells the customer, on the channel they reached us by, that their claim's status changed. Never
    /// throws for a delivery problem: the status change has already happened, and a message that did not go
    /// out is kept with its reason for a person to see.</summary>
    Task<StatusNotificationOutcome> NotifyAsync(ClaimCase before, ClaimCase after, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends the customer the workspace's own wording for a claim's new status (build-loop item 21). Every
/// message is recorded as an ordinary customer update, so it appears in the same queue, follows the same
/// approval policy (outcome statuses always need a person) and carries the same audit trail as one a person
/// wrote. A message may go without approval only when the workspace has switched that on and the policy
/// allows it; the words are always the ones the insurer set on the wording page, never model text.
/// </summary>
public sealed class ClaimStatusNotifier(
    ITenantSettingsRepository settingsRepository,
    ICustomerUpdateRepository updateRepository,
    ICommunicationProvider communicationProvider,
    IAuditRepository auditRepository) : IClaimStatusNotifier
{
    public const string SystemActorId = "system:status-notifier";

    public async Task<StatusNotificationOutcome> NotifyAsync(ClaimCase before, ClaimCase after, string correlationId, CancellationToken cancellationToken = default)
    {
        if (before.Status == after.Status)
        {
            return StatusNotificationOutcome.Unchanged;
        }

        var settings = await settingsRepository.GetAsync(after.Scope, cancellationToken);
        if (!settings.NotifyCustomerOnStatusChange)
        {
            return StatusNotificationOutcome.SwitchedOff;
        }

        if (!ClaimStatusNotificationPolicy.SendsMessage(after.Status))
        {
            return StatusNotificationOutcome.NoMessageForStatus;
        }

        var route = ClaimStatusNotificationPolicy.RouteFor(after.Channel, after.CustomerPhone, after.CustomerEmail);
        if (route is null)
        {
            await AuditAsync(after, "NoRecipient", null, correlationId, cancellationToken);
            return StatusNotificationOutcome.NoRecipient;
        }

        var wording = ClaimantStatusWords.For(after.Status, settings.ClaimantStatusWording);
        var (body, shortBody) = ClaimStatusNotificationPolicy.Compose(wording, ClaimReference.For(after.CaseId), after.CustomerName);
        var reason = ClaimStatusNotificationPolicy.ReasonFor(after.Status);
        var requiresApproval = CustomerUpdateApprovalPolicy.RequiresApproval(sensitive: false, reason);
        var now = DateTimeOffset.UtcNow;
        var update = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = after.Scope,
            CaseId = after.CaseId,
            ReasonCategory = reason,
            Channel = route.Channel,
            Status = requiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = body,
            ShortMessageBody = shortBody,
            CreationMethod = CustomerUpdateCreationMethod.Template,
            SourceTemplateKey = ClaimStatusNotificationPolicy.SourceKey(after.Status),
            RequiresApproval = requiresApproval,
            Sensitive = false,
            CreatedByUserId = SystemActorId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId,
        };
        await updateRepository.SaveAsync(update, cancellationToken);

        if (!ClaimStatusNotificationPolicy.MaySendWithoutApproval(settings.AutoSendStatusMessages, after.Status))
        {
            await AuditAsync(after, "QueuedForApproval", update.UpdateId, correlationId, cancellationToken);
            return StatusNotificationOutcome.QueuedForApproval;
        }

        CustomerUpdateDraft sent;
        try
        {
            sent = await communicationProvider.SendAsync(update, SystemActorId, route.Recipient, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            sent = update with { DeliveryStatus = "Failed", UpdatedAt = DateTimeOffset.UtcNow };
        }

        await updateRepository.SaveAsync(sent, cancellationToken);
        if (CustomerUpdateDelivery.IsFailure(sent.DeliveryStatus))
        {
            await AuditAsync(after, $"NotSent:{sent.DeliveryStatus}", update.UpdateId, correlationId, cancellationToken);
            return StatusNotificationOutcome.NotSent;
        }

        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateSent(sent, SystemActorId, "Sent automatically when the claim's status changed."), cancellationToken);
        await AuditAsync(after, "Sent", update.UpdateId, correlationId, cancellationToken);
        return StatusNotificationOutcome.Sent;
    }

    private Task AuditAsync(ClaimCase claim, string outcome, Guid? updateId, string correlationId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(AuditEvent.ClaimStatusNotified(claim.Scope, claim.CaseId, claim.Status.ToString(), outcome, updateId, correlationId), cancellationToken);
}

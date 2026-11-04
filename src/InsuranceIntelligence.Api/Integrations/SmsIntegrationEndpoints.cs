using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// SMS delivery-status webhook receiver (blueprint §5.4). Matches an inbound gateway callback
/// (EgoSMS's MsgFollowUpUniqueCode, or Africa's Talking's equivalent) to the Communication it
/// belongs to via ProviderMessageId and updates DeliveryStatus, so the Communications screen shows
/// real delivery rather than "sent and hoped."
///
/// Neither gateway's delivery-status payload is signed, this is documented in
/// docs/comms-api-reference.md and is a real, acknowledged limitation, not an oversight. The path
/// secret below is the mitigation: it is unguessable, it is how this single-tenant-per-deployment
/// edition (see WhatsAppChannelConfiguration's own "one Managed Application per insurer" precedent)
/// knows which deployment-configured TenantScope to update, and delivery status is treated as
/// advisory display data only, it is never evidence in a claim decision.
/// </summary>
public static class SmsIntegrationEndpoints
{
    public static async Task<IResult> ReceiveDeliveryStatus(
        string pathSecret,
        SmsDeliveryStatusPayload payload,
        ICommunicationRepository repository,
        InfrastructureOptions options,
        ILogger<SmsDeliveryStatusLogMarker> logger,
        CancellationToken cancellationToken,
        InsuranceIntelligence.Infrastructure.ClientPortal.IPortalDirectory? directory = null)
    {
        // Each workspace has its own secret address (build-loop item 29), so a gateway's report can only ever reach the
        // workspace that gave it that address. A deployment dedicated to one insurer may still use its one shared secret.
        TenantScope scope;
        var ownSecret = directory is null
            ? null
            : await FindWorkspaceBySecretAsync(directory, pathSecret, cancellationToken);
        if (ownSecret is { } workspace)
        {
            scope = workspace;
        }
        else if (string.IsNullOrWhiteSpace(options.SmsWebhookSharedSecret)
            || !CryptographicEquals(pathSecret, options.SmsWebhookSharedSecret))
        {
            // Deliberately the same 404 whether the secret is simply unconfigured or the caller
            // guessed wrong, never confirm to an outsider that a webhook endpoint exists here.
            return Results.NotFound();
        }
        else if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            logger.LogWarning("SMS delivery-status webhook received but INSURANCEINTELLIGENCE_TENANT_ID/WORKSPACE_ID are not configured.");
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured");
        }
        else
        {
            scope = TenantScope.Create(options.TenantId!, options.WorkspaceId!);
        }

        if (string.IsNullOrWhiteSpace(payload.MessageId))
        {
            return Results.BadRequest(new { message = "A message id is required." });
        }

        var communication = await repository.FindByProviderMessageIdAsync(scope, payload.MessageId, cancellationToken);
        if (communication is null)
        {
            // Not an error from the gateway's point of view, just nothing to update on our side
            // (e.g. an older message from before ProviderMessageId capture was added).
            return Results.Ok(new { status = "Unmatched" });
        }

        var updated = communication with { DeliveryStatus = NormalizeStatus(payload.Status) };
        await repository.SaveAsync(updated, cancellationToken);

        logger.LogInformation(
            "SMS delivery status updated. CommunicationId={CommunicationId} Status={Status}",
            updated.CommunicationId, updated.DeliveryStatus);

        return Results.Ok(new { status = "Updated" });
    }

    private static async Task<TenantScope?> FindWorkspaceBySecretAsync(
        InsuranceIntelligence.Infrastructure.ClientPortal.IPortalDirectory directory, string secret, CancellationToken cancellationToken)
    {
        await directory.RefreshIfStaleAsync(cancellationToken);
        return directory.BySmsSecretHash(InsuranceIntelligence.Domain.ClientPortal.PortalIdentity.HashSecret(secret))?.Scope;
    }

    private static string NormalizeStatus(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? "Unknown" : raw.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static bool CryptographicEquals(string a, string b)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(a);
        var right = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }
}

/// <summary>The documented EgoSMS transaction-status payload
/// (docs/comms-api-reference.md "Transaction Status Webhook"): {MsgFollowUpUniqueCode, number, Status}.</summary>
public sealed record SmsDeliveryStatusPayload(
    string? MsgFollowUpUniqueCode,
    string? Number,
    string? Status)
{
    public string? MessageId => MsgFollowUpUniqueCode;
}

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;SmsIntegrationEndpoints&gt; is not usable, the class is static).</summary>
public sealed class SmsDeliveryStatusLogMarker;

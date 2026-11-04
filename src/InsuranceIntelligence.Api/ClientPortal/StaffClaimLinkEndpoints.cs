using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

public sealed record SendClaimLinkRequest(string? Channel);

/// <summary>
/// "Send claim link" from the claim page (build-loop item 22): a staff member sends the claimant a link to
/// that one claim, on the channel the claim came in on or the one they choose. The link is the same
/// single use token the claimant portal already issues, valid for 24 hours; only its hash is stored. The
/// customer's consent is respected (WhatsApp needs a recorded consent, text and email are never sent after a
/// withdrawal), a person must send it, and the send is audited with the channel and outcome but never the
/// link. The message record that is kept holds a plain sentence instead of the link.
/// </summary>
public static class StaffClaimLinkEndpoints
{
    public static async Task<IResult> Send(
        Guid caseId,
        SendClaimLinkRequest request,
        ICaseRepository caseRepository,
        IClaimantMagicLinkTokenRepository tokenRepository,
        IConsentRepository consentRepository,
        ICommunicationProvider communicationProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        InsuranceIntelligence.Application.ClientPortal.IPortalAddress? portalAddress = null)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var actor, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required to send a claim link."] });
        }

        try
        {
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(true, ApprovalDecisionGuard.ResolveActorType(actor), "sendClaimLink");
        }
        catch (DomainException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        var correlationId = httpContext.TraceIdentifier;
        var (route, refusal) = ClaimLinkPolicy.Choose(request?.Channel, claim.Channel, claim.CustomerPhone, claim.CustomerEmail);
        if (route is null)
        {
            await auditRepository.WriteAsync(AuditEvent.ClaimLinkSent(scope, caseId, actor, request?.Channel ?? "auto", refusal.ToString(), correlationId), cancellationToken);
            return Results.Conflict(new { outcome = refusal.ToString(), message = ClaimLinkPolicy.PlainReason(refusal) });
        }

        var consentIdentifier = route.Channel == CommunicationChannel.WhatsApp ? new string(route.Recipient.Where(char.IsDigit).ToArray()) : route.Recipient.Trim();
        var consent = await consentRepository.GetCurrentAsync(scope, consentIdentifier, ClaimLinkPolicy.PurposeFor(route.Channel), cancellationToken);
        refusal = ClaimLinkPolicy.ConsentCheck(route.Channel, consent);
        if (refusal != ClaimLinkRefusal.None)
        {
            await auditRepository.WriteAsync(AuditEvent.ClaimLinkSent(scope, caseId, actor, route.Channel.ToString(), refusal.ToString(), correlationId), cancellationToken);
            return Results.Conflict(new { outcome = refusal.ToString(), message = ClaimLinkPolicy.PlainReason(refusal) });
        }

        var baseUrl = portalAddress is not null
            ? portalAddress.BaseUrlFor(scope)
            : !string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl)
                ? options.ClientPortalBaseUrl.TrimEnd('/')
                : options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "http://localhost:3000" : null;
        if (baseUrl is null)
        {
            await auditRepository.WriteAsync(AuditEvent.ClaimLinkSent(scope, caseId, actor, route.Channel.ToString(), "ConfigurationRequired", correlationId), cancellationToken);
            return Results.Conflict(new { outcome = "ConfigurationRequired", message = "The customer portal address is not configured, so a link cannot be made." });
        }

        var rawToken = ClaimantTokenGenerator.GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var token = new ClaimantMagicLinkToken
        {
            TokenId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            PolicyNumber = claim.PolicyNumber,
            ContactValue = route.Recipient,
            Channel = route.Channel == CommunicationChannel.Email ? ClaimantContactChannel.Email : ClaimantContactChannel.Sms,
            TokenHash = ClaimantTokenGenerator.Hash(rawToken),
            ExpiresAt = now.Add(ClaimLinkPolicy.LinkLifetime),
            CreatedAt = now,
            CorrelationId = correlationId,
            DeliveryStatus = "SentByStaff",
        };
        await tokenRepository.SaveAsync(token, cancellationToken);

        var link = $"{baseUrl}/access/verify?token={Uri.EscapeDataString(rawToken)}{portalAddress?.LinkQueryFor(scope)}";
        var (body, shortBody) = ClaimLinkPolicy.Compose(claim.CustomerName, ClaimReference.For(caseId), link);
        var message = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = route.Channel,
            Status = CustomerUpdateStatus.Draft,
            MessageBody = body,
            ShortMessageBody = shortBody,
            CreationMethod = CustomerUpdateCreationMethod.Template,
            SourceTemplateKey = ClaimLinkPolicy.SourceKey,
            RequiresApproval = false,
            Sensitive = false,
            CreatedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId,
        };

        // The message is sent but not saved as a customer update: it carries the link, which must not sit in
        // a record staff can read back. Only the sending channel's own record keeps a plain sentence.
        var sent = await communicationProvider.SendAsync(message, actor, route.Recipient, cancellationToken);
        if (CustomerUpdateDelivery.IsFailure(sent.DeliveryStatus))
        {
            // The token was never delivered; it expires on its own and nobody holds it.
            await tokenRepository.SaveAsync(token with { ConsumedAt = now, DeliveryStatus = $"NotSent:{sent.DeliveryStatus}" }, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.ClaimLinkSent(scope, caseId, actor, route.Channel.ToString(), $"NotSent:{sent.DeliveryStatus}", correlationId), cancellationToken);
            return Results.Conflict(new { outcome = sent.DeliveryStatus, message = CustomerUpdateDelivery.PlainReason(sent.DeliveryStatus) });
        }

        await auditRepository.WriteAsync(AuditEvent.ClaimLinkSent(scope, caseId, actor, route.Channel.ToString(), "Sent", correlationId), cancellationToken);
        return Results.Ok(new
        {
            outcome = "Sent",
            channel = route.Channel.ToString(),
            sentTo = ClaimLinkPolicy.Mask(route.Recipient),
            expiresAt = token.ExpiresAt,
        });
    }
}

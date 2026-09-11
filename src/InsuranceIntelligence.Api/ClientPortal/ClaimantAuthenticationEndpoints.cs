using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Magic-link authentication for claimants: a claimant has no platform account, so these two
/// routes are intentionally unauthenticated (that is the point — they establish identity, they
/// don't assume it). Every response is deliberately generic regardless of whether a case or
/// contact value actually matched, so this can never be used to enumerate claims or the people on
/// them. See ClaimantSessionEndpointFilter for how the session this issues is verified on every
/// subsequent claimant-facing request.
/// </summary>
public static class ClaimantAuthenticationEndpoints
{
    private static readonly TimeSpan MagicLinkLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    public static async Task<IResult> RequestMagicLink(
        ClaimantMagicLinkRequest request,
        ICaseRepository caseRepository,
        IPolicyRepository policyRepository,
        ICustomerRepository customerRepository,
        IClaimantMagicLinkTokenRepository tokenRepository,
        ICommunicationSendingProvider communicationProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        ILogger<ClaimantMagicLinkLogMarker> logger,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl)
            && !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                deliveryStatus = "ConfigurationRequired",
                message = "The client portal base URL is not configured; magic links cannot be issued."
            });
        }

        if (string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            return Validation("policyNumber", "A policy number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ContactValue))
        {
            return Validation("contactValue", "An email address or phone number is required.");
        }

        var correlationId = GetCorrelationId(httpContext);
        var policyNumber = request.PolicyNumber.Trim();

        // Constant-shape response regardless of match: an unknown policy number or a contact value
        // that doesn't match must look identical to the caller as a real match, or this becomes an
        // oracle for enumerating claims and the people on them. A claimant only ever knows their
        // policy number, not IIP's internal case id, so this — not a case id — is the lookup key;
        // a policy can have more than one claim, so this picks the most recently created one, the
        // same tie-break PolicyLookupEndpoints already uses for duplicate detection at intake.
        var allCases = await caseRepository.ListAsync(scope, cancellationToken);
        var claimCase = allCases
            .Where(c => string.Equals(c.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase))
            .Where(c => ClaimantContactMatcher.Matches(request.ContactValue, c.CustomerEmail, c.CustomerPhone))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        // No existing claim on that policy for this contact — check whether they're a verified
        // policyholder who simply hasn't filed one yet. If so, issue a token scoped to that policy
        // with no case id: it can only ever be redeemed into a "start a new claim" session (see
        // CreateClaim), never used to read or touch anyone's existing case.
        Policy? policy = null;
        if (claimCase is null)
        {
            var candidatePolicy = await policyRepository.GetByPolicyNumberAsync(policyNumber, scope, cancellationToken);
            if (candidatePolicy is not null)
            {
                var customer = string.IsNullOrWhiteSpace(candidatePolicy.CustomerId)
                    ? null
                    : await customerRepository.GetAsync(candidatePolicy.CustomerId, scope, cancellationToken);
                if (customer is not null && ClaimantContactMatcher.Matches(request.ContactValue, customer.Email, customer.Phone))
                {
                    policy = candidatePolicy;
                }
            }
        }

        var matched = claimCase is not null || policy is not null;

        if (claimCase is not null)
        {
            await IssueAndSendAsync(
                policyNumber, claimCase.CaseId, request.ContactValue, claimCase.CustomerEmail, claimCase.CustomerPhone,
                scope, communicationProvider, tokenRepository, options, correlationId, logger, cancellationToken);
        }
        else if (policy is not null)
        {
            var customer = string.IsNullOrWhiteSpace(policy.CustomerId)
                ? null
                : await customerRepository.GetAsync(policy.CustomerId, scope, cancellationToken);
            await IssueAndSendAsync(
                policyNumber, caseId: null, request.ContactValue, customer?.Email, customer?.Phone,
                scope, communicationProvider, tokenRepository, options, correlationId, logger, cancellationToken);
        }

        await auditRepository.WriteAsync(
            AuditEvent.ClaimantMagicLinkRequested(
                scope,
                claimCase?.CaseId,
                matched,
                ClaimantContactMatcher.ClassifyChannel(request.ContactValue).ToString(),
                correlationId),
            cancellationToken);

        return Results.Ok(new ClaimantMagicLinkRequestResponse(
            "If that policy number and contact value match a claim on file — or a policy you hold — we've sent a secure access link."));
    }

    public static async Task<IResult> RedeemMagicLink(
        ClaimantMagicLinkRedeemRequest request,
        IClaimantMagicLinkTokenRepository tokenRepository,
        IClaimantPortalSessionRepository sessionRepository,
        IPolicyRepository policyRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Validation("token", "An access token is required.");
        }

        var tokenHash = ClaimantTokenGenerator.Hash(request.Token);
        var token = await tokenRepository.GetByTokenHashAsync(tokenHash, scope, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (token is null || !token.IsRedeemableAt(now))
        {
            return Results.BadRequest(new { message = "This link is invalid or has expired. Request a new one." });
        }

        var correlationId = GetCorrelationId(httpContext);
        await tokenRepository.SaveAsync(token with { ConsumedAt = now }, cancellationToken);

        var rawSessionToken = ClaimantTokenGenerator.GenerateRawToken();
        var session = new ClaimantPortalSession
        {
            SessionId = Guid.NewGuid(),
            Scope = scope,
            CaseId = token.CaseId,
            PolicyNumber = token.PolicyNumber,
            ContactValue = token.ContactValue,
            SessionTokenHash = ClaimantTokenGenerator.Hash(rawSessionToken),
            ExpiresAt = now.Add(SessionLifetime),
            CreatedAt = now,
            CorrelationId = correlationId
        };
        await sessionRepository.SaveAsync(session, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimantSessionEstablished(session, correlationId), cancellationToken);

        // A new-claim session has nothing for the claimant to view yet — hand back the policy's
        // product line so the intake form can prefill/lock it instead of asking a verified
        // policyholder a question IIP already knows the answer to.
        string? productLine = null;
        if (token.CaseId is null)
        {
            var policy = await policyRepository.GetByPolicyNumberAsync(token.PolicyNumber, scope, cancellationToken);
            productLine = policy?.ProductLine;
        }

        return Results.Ok(new ClaimantMagicLinkRedeemResponse(
            rawSessionToken, token.CaseId, session.ExpiresAt, token.PolicyNumber, productLine));
    }

    /// <summary>
    /// Submits a new claim from a verified "no claim yet" session (see RequestMagicLink / policy
    /// match branch). Requires ClaimantSessionEndpointFilter to have run — this route has no
    /// {caseId} segment, so the filter's route-match check never fires; the CaseId is null check
    /// below is what stops a session for an existing claim from being reused to spam-create
    /// unrelated ones.
    /// </summary>
    public static async Task<IResult> CreateClaim(
        ClaimantCreateClaimRequest request,
        IPolicyRepository policyRepository,
        ICustomerRepository customerRepository,
        CaseIntakeService caseIntakeService,
        IClaimantPortalSessionRepository sessionRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) is false
            || stashed is not ClaimantPortalSession session)
        {
            return Results.Unauthorized();
        }

        if (session.CaseId is not null)
        {
            return Results.Conflict(new { message = "This access link is for an existing claim. Use the claim status page instead." });
        }

        if (string.IsNullOrWhiteSpace(request.ProductLine)
            || string.IsNullOrWhiteSpace(request.ClaimType)
            || request.IncidentDate is null
            || string.IsNullOrWhiteSpace(request.IncidentDescription))
        {
            return Validation("request", "Product line, claim type, incident date, and a description are all required.");
        }

        var policy = await policyRepository.GetByPolicyNumberAsync(session.PolicyNumber, session.Scope, cancellationToken);
        if (policy is null)
        {
            return Results.Conflict(new { message = "The policy on this access link could no longer be verified." });
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId)
            ? null
            : await customerRepository.GetAsync(policy.CustomerId, session.Scope, cancellationToken);

        var correlationId = GetCorrelationId(httpContext);
        var command = new CreateCaseCommand(
            session.Scope,
            customer?.FullName ?? session.ContactValue,
            customer?.Email ?? (session.ContactValue.Contains('@') ? session.ContactValue : null),
            customer?.Phone ?? (!session.ContactValue.Contains('@') ? session.ContactValue : null),
            session.PolicyNumber,
            request.ProductLine.Trim(),
            request.ClaimType.Trim(),
            "WebPortal",
            request.IncidentDate.Value,
            request.IncidentDescription.Trim(),
            session.ContactValue,
            correlationId,
            request.MotorFnolDetails);

        ClaimCase claimCase;
        try
        {
            claimCase = await caseIntakeService.CreateCaseAsync(command, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Validation(ex.ParamName ?? "request", ex.Message);
        }

        // Upgrade the session in place so the claimant lands straight on their new claim's status
        // page with the same link — no second magic link needed for the thing they just did.
        var upgraded = session with { CaseId = claimCase.CaseId };
        await sessionRepository.SaveAsync(upgraded, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.ClaimantMagicLinkRequested(session.Scope, claimCase.CaseId, matched: true, "PortalSubmission", correlationId),
            cancellationToken);

        return Results.Created($"/api/v1/client-portal/claims/{claimCase.CaseId}", new ClaimantCreateClaimResponse(claimCase.CaseId));
    }

    private static async Task IssueAndSendAsync(
        string policyNumber,
        Guid? caseId,
        string requestedContactValue,
        string? verifiedEmail,
        string? verifiedPhone,
        TenantScope scope,
        ICommunicationSendingProvider communicationProvider,
        IClaimantMagicLinkTokenRepository tokenRepository,
        InfrastructureOptions options,
        string correlationId,
        ILogger<ClaimantMagicLinkLogMarker> logger,
        CancellationToken cancellationToken)
    {
        var channel = ClaimantContactMatcher.ClassifyChannel(requestedContactValue);
        // Deliver to the value verified on file, not whatever the requester typed — the requester
        // is unauthenticated at this point, so the recipient must always be the record's own
        // stored contact, never attacker-supplied input.
        var recipient = channel == ClaimantContactChannel.Email ? verifiedEmail : verifiedPhone;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        var rawToken = ClaimantTokenGenerator.GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var token = new ClaimantMagicLinkToken
        {
            TokenId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            PolicyNumber = policyNumber,
            ContactValue = recipient,
            Channel = channel,
            TokenHash = ClaimantTokenGenerator.Hash(rawToken),
            ExpiresAt = now.Add(MagicLinkLifetime),
            CreatedAt = now,
            CorrelationId = correlationId
        };
        await tokenRepository.SaveAsync(token, cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl)
            ? "http://localhost:3000"
            : options.ClientPortalBaseUrl!.TrimEnd('/');
        var link = $"{baseUrl}/access/verify?token={Uri.EscapeDataString(rawToken)}";
        var messageBody = caseId is null
            ? $"Use this secure link to start your claim on policy {policyNumber}. It expires in 15 minutes and can only be used once: {link}"
            : $"Use this secure link to view your claim. It expires in 15 minutes and can only be used once: {link}";

        var sendRequest = new CommunicationSendRequest(
            scope,
            caseId ?? Guid.Empty,
            channel == ClaimantContactChannel.Email ? CommunicationChannel.Email : CommunicationChannel.SMS,
            recipient,
            messageBody,
            SentByUserId: "system:claimant-magic-link",
            Approved: true,
            LowRisk: true,
            correlationId);

        // Previously fire-and-forget (`_ = await ...`): the result was awaited but discarded, so a
        // delivery failure produced the exact same "we've sent a link" response as a real send,
        // with no record anywhere that it didn't go out. Now the outcome is logged and recorded on
        // the token — but the anonymous caller's response is deliberately left untouched by this
        // fix: it must stay identical whether delivery succeeded, failed, or was never attempted
        // (no match), or the response shape itself becomes an enumeration oracle. This is a fix for
        // visibility (ops, and future support tooling reading DeliveryStatus off the token), not a
        // change to what the requester is told.
        var sendResult = channel == ClaimantContactChannel.Email
            ? await communicationProvider.SendEmailAsync(sendRequest, cancellationToken)
            : await communicationProvider.SendSmsAsync(sendRequest, cancellationToken);

        if (!string.Equals(sendResult.DeliveryStatus, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Claimant magic-link delivery did not succeed. Channel={Channel} Status={Status} CorrelationId={CorrelationId}",
                channel, sendResult.DeliveryStatus, correlationId);
        }

        await tokenRepository.SaveAsync(
            token with { DeliveryStatus = sendResult.DeliveryStatus, DeliverySafeError = sendResult.SafeError },
            cancellationToken);
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

public sealed record ClaimantMagicLinkRequest(string PolicyNumber, string ContactValue);

public sealed record ClaimantMagicLinkRequestResponse(string Message);

public sealed record ClaimantMagicLinkRedeemRequest(string Token);

public sealed record ClaimantMagicLinkRedeemResponse(
    string SessionToken,
    Guid? CaseId,
    DateTimeOffset ExpiresAt,
    string PolicyNumber,
    string? ProductLine);

public sealed record ClaimantCreateClaimRequest(
    string ProductLine,
    string ClaimType,
    DateOnly? IncidentDate,
    string IncidentDescription,
    MotorFnolDetails? MotorFnolDetails = null);

public sealed record ClaimantCreateClaimResponse(Guid CaseId);

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;ClaimantAuthenticationEndpoints&gt; is not usable — the class
/// is static).</summary>
public sealed class ClaimantMagicLinkLogMarker;

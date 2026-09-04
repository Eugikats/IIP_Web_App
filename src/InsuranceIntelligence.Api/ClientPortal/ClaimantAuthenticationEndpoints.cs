using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

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
        IClaimantMagicLinkTokenRepository tokenRepository,
        ICommunicationSendingProvider communicationProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
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

        if (string.IsNullOrWhiteSpace(request.ContactValue))
        {
            return Validation("contactValue", "An email address or phone number is required.");
        }

        var correlationId = GetCorrelationId(httpContext);

        // Constant-shape response regardless of match: request.CaseId or request.ContactValue not
        // matching a real case must look identical to the caller as a real match, or this becomes
        // an oracle for enumerating claims and the people on them.
        var claimCase = await caseRepository.GetAsync(request.CaseId, cancellationToken);
        var matched = claimCase is not null
            && claimCase.Scope == scope
            && ClaimantContactMatcher.Matches(request.ContactValue, claimCase.CustomerEmail, claimCase.CustomerPhone);

        if (matched)
        {
            await IssueAndSendAsync(claimCase!, scope, communicationProvider, tokenRepository, options, correlationId, cancellationToken);
        }

        await auditRepository.WriteAsync(
            AuditEvent.ClaimantMagicLinkRequested(
                scope,
                request.CaseId,
                matched,
                ClaimantContactMatcher.ClassifyChannel(request.ContactValue).ToString(),
                correlationId),
            cancellationToken);

        return Results.Ok(new ClaimantMagicLinkRequestResponse(
            "If that claim reference and contact value match a claim on file, we've sent a secure access link."));
    }

    public static async Task<IResult> RedeemMagicLink(
        ClaimantMagicLinkRedeemRequest request,
        IClaimantMagicLinkTokenRepository tokenRepository,
        IClaimantPortalSessionRepository sessionRepository,
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
            ContactValue = token.ContactValue,
            SessionTokenHash = ClaimantTokenGenerator.Hash(rawSessionToken),
            ExpiresAt = now.Add(SessionLifetime),
            CreatedAt = now,
            CorrelationId = correlationId
        };
        await sessionRepository.SaveAsync(session, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimantSessionEstablished(session, correlationId), cancellationToken);

        return Results.Ok(new ClaimantMagicLinkRedeemResponse(rawSessionToken, token.CaseId, session.ExpiresAt));
    }

    private static async Task IssueAndSendAsync(
        ClaimCase claimCase,
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        ICommunicationSendingProvider communicationProvider,
        IClaimantMagicLinkTokenRepository tokenRepository,
        InfrastructureOptions options,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var channel = ClaimantContactMatcher.ClassifyChannel(claimCase.CustomerEmail ?? claimCase.CustomerPhone ?? string.Empty);
        var recipient = channel == ClaimantContactChannel.Email ? claimCase.CustomerEmail : claimCase.CustomerPhone;
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
            CaseId = claimCase.CaseId,
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
        var messageBody = $"Use this secure link to view your claim. It expires in 15 minutes and can only be used once: {link}";

        var sendRequest = new CommunicationSendRequest(
            scope,
            claimCase.CaseId,
            channel == ClaimantContactChannel.Email ? CommunicationChannel.Email : CommunicationChannel.SMS,
            recipient,
            messageBody,
            SentByUserId: "system:claimant-magic-link",
            Approved: true,
            LowRisk: true,
            correlationId);

        _ = channel == ClaimantContactChannel.Email
            ? await communicationProvider.SendEmailAsync(sendRequest, cancellationToken)
            : await communicationProvider.SendSmsAsync(sendRequest, cancellationToken);
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

public sealed record ClaimantMagicLinkRequest(Guid CaseId, string ContactValue);

public sealed record ClaimantMagicLinkRequestResponse(string Message);

public sealed record ClaimantMagicLinkRedeemRequest(string Token);

public sealed record ClaimantMagicLinkRedeemResponse(string SessionToken, Guid CaseId, DateTimeOffset ExpiresAt);

using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Communications;

public static class CommunicationEndpoints
{
    public static Task<IResult> SendEmail(
        SendCommunicationRequest request,
        ICommunicationSendingProvider provider,
        ICommunicationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        Send(request, CommunicationChannel.Email, provider.SendEmailAsync, repository, auditRepository, options, httpContext, cancellationToken);

    public static Task<IResult> SendSms(
        SendCommunicationRequest request,
        ICommunicationSendingProvider provider,
        ICommunicationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        Send(request, CommunicationChannel.SMS, provider.SendSmsAsync, repository, auditRepository, options, httpContext, cancellationToken);

    public static async Task<IResult> SendTeamsNotification(
        SendTeamsNotificationRequest request,
        ITeamsNotificationProvider provider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (!Enum.TryParse<TeamsNotificationType>(request.NotificationType, true, out var notificationType))
        {
            return Validation("notificationType", "A supported Teams notification type is required.");
        }

        if (options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
            && !IsDevelopmentAuth(options))
        {
            return Results.BadRequest(new
            {
                deliveryStatus = "ConfigurationRequired",
                message = "Teams notifications require a configured provider outside Development."
            });
        }

        var correlationId = httpContext.TraceIdentifier;
        var notification = new TeamsNotification
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            CaseId = request.CaseId,
            NotificationType = notificationType,
            PayloadJson = $"{{\"Message\": \"{request.Message}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var providerStatus = await provider.SendAsync(notification, cancellationToken);
        if (providerStatus is "ConfigurationRequired" or "Failed")
        {
            return Results.BadRequest(new { deliveryStatus = providerStatus, message = "Teams notifications are not configured." });
        }

        var (auditAction, providerName) = TeamsAuditProvider(options.NotificationProvider);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                scope,
                request.CaseId,
                null,
                request.RequestedByUserId,
                ActorType.Integration,
                auditAction,
                DateTimeOffset.UtcNow,
                null,
                null,
                [],
                providerName,
                null,
                providerStatus,
                null,
                AuditReasonReader.Read(httpContext),
                correlationId),
            cancellationToken);
        return Results.Ok(new { deliveryStatus = providerStatus });
    }

    public static async Task<IResult> ListHistory(
        ICaseRepository caseRepository,
        ICommunicationRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAssignedClaims) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var communications = await repository.ListAsync(scope, cancellationToken);
        var visibleCommunications = new List<Communication>();
        foreach (var communication in communications)
        {
            var claimCase = await caseRepository.GetAsync(communication.CaseId, cancellationToken);
            if (claimCase is not null
                && claimCase.Scope.Matches(scope)
                && ClaimAccessGuard.CanViewCase(httpContext, claimCase))
            {
                visibleCommunications.Add(communication);
            }
        }

        return Results.Ok(Sanitize(visibleCommunications));
    }

    public static async Task<IResult> ListForCase(
        Guid caseId,
        ICaseRepository caseRepository,
        ICommunicationRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAssignedClaims) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null
            || !claimCase.Scope.Matches(scope)
            || !ClaimAccessGuard.CanViewCase(httpContext, claimCase))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        return Results.Ok(Sanitize(await repository.ListForCaseAsync(caseId, scope, cancellationToken)));
    }

    private static async Task<IResult> Send(
        SendCommunicationRequest request,
        CommunicationChannel channel,
        Func<CommunicationSendRequest, CancellationToken, Task<CommunicationSendResult>> send,
        ICommunicationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.SendCustomerUpdate) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
            && !IsDevelopmentAuth(options))
        {
            return Results.BadRequest(new
            {
                deliveryStatus = "ConfigurationRequired",
                message = "Customer communications require a configured provider outside Development."
            });
        }

        var result = await send(new CommunicationSendRequest(scope, request.CaseId, channel, request.Recipient, request.MessageBody, request.SentByUserId, request.Approved, request.LowRisk, httpContext.TraceIdentifier), cancellationToken);
        if (result.DeliveryStatus is "ApprovalRequired" or "ConfigurationRequired" or "Failed")
        {
            return Results.BadRequest(new { deliveryStatus = result.DeliveryStatus, message = result.SafeError });
        }

        var communication = new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = scope,
            CaseId = request.CaseId,
            Channel = channel,
            Direction = "Outbound",
            MessageBody = request.MessageBody,
            SentBy = request.SentByUserId,
            DeliveryStatus = result.DeliveryStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = httpContext.TraceIdentifier
        };
        await repository.SaveAsync(communication, cancellationToken);
        var (auditAction, providerName) = AuditProvider(channel, options.CommunicationProvider);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                scope,
                request.CaseId,
                communication.CommunicationId.ToString(),
                request.SentByUserId,
                ActorType.Integration,
                auditAction,
                DateTimeOffset.UtcNow,
                null,
                null,
                [communication.CommunicationId.ToString()],
                providerName,
                null,
                result.DeliveryStatus,
                null,
                AuditReasonReader.Read(httpContext),
                httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(Sanitize([communication]).Single());
    }

    private static (string Action, string ProviderName) AuditProvider(CommunicationChannel channel, string configuredProvider)
    {
        if (configuredProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return channel == CommunicationChannel.SMS
                ? ("notification.sms.simulated", "Mock")
                : ("notification.email.simulated", "Mock");
        }

        if (channel == CommunicationChannel.Email)
        {
            return ("notification.email.sent", configuredProvider);
        }

        if (channel == CommunicationChannel.SMS)
        {
            return ("notification.sms.sent", configuredProvider);
        }

        return ("notification.sent", configuredProvider);
    }

    private static (string Action, string ProviderName) TeamsAuditProvider(string configuredProvider) =>
        configuredProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
            ? ("notification.teams.simulated", "Mock")
            : ("notification.teams.sent", configuredProvider);

    private static IReadOnlyCollection<Communication> Sanitize(IReadOnlyCollection<Communication> communications) =>
        communications
            .Where(communication => !ContainsUnsafe(communication.MessageBody))
            .ToArray();

    private static bool ContainsUnsafe(string value) =>
        new[] { "raw prompt", "system message", "internal note", "fraud flag", "reserve", "hidden risk", "secret" }
            .Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static bool IsDevelopmentAuth(InfrastructureOptions options) =>
        options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
}

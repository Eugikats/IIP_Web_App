using System.Net.Mail;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// "Send a test" for email and SMS: the only way to know a channel works is to send through it. It goes through the
/// same tenant-aware provider real messages use, so it proves the saved connection, not a copy of it. A person with
/// permission to configure integrations presses the button, so that person is the named approver, and the audit entry
/// records who, which channel and a masked recipient, never the message. The text is fixed and says it is a test. It is
/// limited to a few sends an hour per person and channel, so it cannot be used to message people in bulk.
/// </summary>
public static class IntegrationTestSendEndpoints
{
    public const int MaxPerHour = 3;
    public const string TestText = "This is a test message from the Insurance Intelligence Platform. No action is needed.";

    public sealed record TestSendRequest(string? Recipient);

    public static async Task<IResult> Send(
        string channel,
        TestSendRequest request,
        IEmailSendingProvider email,
        ISmsProvider sms,
        IAuditWriter audit,
        IMemoryCache cache,
        TimeProvider clock,
        ITenantContextSetter setter,
        ITenantContextAccessor tenantAccessor,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!CommandCenter.CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, setter, options, out var error))
        {
            return error;
        }

        var isEmail = string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase);
        var isSms = string.Equals(channel, "sms", StringComparison.OrdinalIgnoreCase);
        if (!isEmail && !isSms)
        {
            return Results.NotFound(new { message = "A test can be sent through email or SMS." });
        }

        var recipient = (request.Recipient ?? string.Empty).Trim();
        if (isEmail ? !IsEmail(recipient) : !IsPhone(recipient))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["recipient"] = [isEmail ? "Enter a valid email address." : "Enter a phone number with its country code, for example +256700000000."],
            });
        }

        var tenant = tenantAccessor.Current;
        if (!TryTakeSlot(cache, clock, tenant.UserId, isEmail ? "email" : "sms"))
        {
            return Results.Json(
                new { message = $"A test can be sent {MaxPerHour} times an hour. Try again later." },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var send = new CommunicationSendRequest(
            tenant.Scope, null, isEmail ? CommunicationChannel.Email : CommunicationChannel.SMS, recipient, TestText,
            tenant.UserId, Approved: true, LowRisk: true, httpContext.TraceIdentifier);

        CommunicationSendResult result;
        try
        {
            result = isEmail ? await email.SendEmailAsync(send, cancellationToken) : await sms.SendAsync(send, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever the provider threw stays on the server. The person is told it did not go, in words.
            result = new CommunicationSendResult("Failed", string.Empty, "The provider could not be reached or refused the message.");
        }

        var accepted = !string.Equals(result.DeliveryStatus, "Failed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(result.DeliveryStatus, "ConfigurationRequired", StringComparison.OrdinalIgnoreCase);

        await audit.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), tenant.Scope, null, null, tenant.UserId, ActorType.User,
                "integration.test_sent", clock.GetUtcNow(), null, null, [], "Integrations", null,
                accepted ? "sent" : "failed", null,
                $"Test {(isEmail ? "email" : "SMS")} to {(isEmail ? MaskEmail(recipient) : ContactMask.Mask(recipient))}.",
                httpContext.TraceIdentifier),
            cancellationToken);

        return Results.Ok(new
        {
            channel = isEmail ? "email" : "sms",
            accepted,
            deliveryStatus = result.DeliveryStatus,
            message = accepted
                ? $"The provider accepted the test {(isEmail ? "email" : "message")}. Check that it arrived."
                : result.DeliveryStatus.Equals("ConfigurationRequired", StringComparison.OrdinalIgnoreCase)
                    ? "This channel is not set up yet, so nothing was sent."
                    : result.SafeError ?? "The provider did not accept the message.",
        });
    }

    private static bool TryTakeSlot(IMemoryCache cache, TimeProvider clock, string userId, string channel)
    {
        var key = $"test-send:{userId.ToLowerInvariant()}:{channel}:{clock.GetUtcNow():yyyyMMddHH}";
        lock (cache)
        {
            var used = cache.TryGetValue(key, out int count) ? count : 0;
            if (used >= MaxPerHour) return false;
            cache.Set(key, used + 1, TimeSpan.FromMinutes(65));
            return true;
        }
    }

    public static bool IsEmail(string value) =>
        value.Length <= 254 && MailAddress.TryCreate(value, out var address) && address.Address == value && value.Contains('.', StringComparison.Ordinal);

    public static bool IsPhone(string value)
    {
        var digits = value.Count(char.IsDigit);
        return value.StartsWith('+') && digits is >= 9 and <= 15 && value.Skip(1).All(ch => char.IsDigit(ch) || ch is ' ' or '-');
    }

    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }
}

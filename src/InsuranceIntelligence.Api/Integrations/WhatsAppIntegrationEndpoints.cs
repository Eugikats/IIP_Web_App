using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Meta WhatsApp Cloud API webhook surface. GET verifies subscription; POST verifies signature
/// then routes inbound media onto case documents. Returns honest Not configured when the channel
/// provider is not WhatsAppCloudApi or credentials are missing.
/// </summary>
public static class WhatsAppIntegrationEndpoints
{
    public static IResult GetHealth(
        IWhatsAppInboundProvider inboundProvider,
        IWhatsAppOutboundProvider outboundProvider,
        IWhatsAppRuntimeSettingsProvider settingsProvider,
        InfrastructureOptions options)
    {
        var settings = settingsProvider.Current;
        var mappingConfigured = settings is not null
            && !string.IsNullOrWhiteSpace(settings.TenantId)
            && !string.IsNullOrWhiteSpace(settings.WorkspaceId);
        // A connection saved from the settings screen is live without a deployment change (the
        // providers are registered unconditionally, see InfrastructureRegistration), so it counts
        // as selected here too. Previously this reported "Not configured" for a working, tenant-
        // connected channel whenever INSURANCEINTELLIGENCE_CHANNEL_PROVIDER was still Disabled.
        var providerSelected = options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings?.Source, "TenantConfigured", StringComparison.Ordinal);
        var configured = providerSelected && inboundProvider.IsConfigured && outboundProvider.IsConfigured && mappingConfigured;
        return Results.Ok(new
        {
            channel = "whatsapp",
            provider = options.ChannelProvider,
            configured,
            inboundConfigured = providerSelected && inboundProvider.IsConfigured,
            outboundConfigured = providerSelected && outboundProvider.IsConfigured,
            tenantMappingConfigured = mappingConfigured,
            configurationSource = settings?.Source,
            status = configured ? "ready" : "Not configured"
        });
    }

    public static IResult VerifyWebhook(
        HttpRequest request,
        IWhatsAppInboundProvider provider)
    {
        var mode = request.Query["hub.mode"].ToString();
        var token = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();

        if (!provider.IsConfigured)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Not configured",
                detail: "WhatsApp inbound is not configured.");
        }

        if (provider.TryVerifySubscription(mode, token, challenge, out var challengeResponse)
            && challengeResponse is not null)
        {
            return Results.Text(challengeResponse, "text/plain");
        }

        return Results.Unauthorized();
    }

    public static async Task<IResult> ReceiveWebhook(
        HttpRequest request,
        WhatsAppInboundDocumentIngestionService ingestionService,
        IWhatsAppMessagingService messagingService,
        IWhatsAppAssistantQueue assistantQueue,
        IServiceScopeFactory scopeFactory,
        IWhatsAppRuntimeSettingsProvider settingsProvider,
        InfrastructureOptions options,
        ILogger<WhatsAppCustomerAssistantLogMarker> logger,
        CancellationToken cancellationToken)
    {
        // Read raw body before any JSON model binding so HMAC covers exact bytes Meta signed.
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken);
        var rawBody = memory.ToArray();
        var signature = request.Headers["X-Hub-Signature-256"].ToString();

        var result = await ingestionService.IngestWebhookPayloadAsync(rawBody, signature, cancellationToken);
        if (result.Status == "ConfigurationRequired")
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Not configured",
                detail: result.SafeError ?? "WhatsApp inbound is not configured.");
        }
        if (result.Status == "InvalidSignature") return Results.Unauthorized();

        var settings = settingsProvider.Current;
        if (settings is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Not configured",
                detail: "WhatsApp inbound is not configured.");
        }

        var scope = TenantScope.Create(settings.TenantId, settings.WorkspaceId);
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(rawBody);
            var inbound = WhatsAppWebhookParser.ParseMessages(json)
                .Where(message => string.Equals(message.BusinessPhoneNumberId, settings.PhoneNumberId, StringComparison.Ordinal))
                .ToArray();
            var statuses = WhatsAppWebhookParser.ParseDeliveryEvents(json)
                .Where(status => string.Equals(status.BusinessPhoneNumberId, settings.PhoneNumberId, StringComparison.Ordinal))
                .ToArray();
            var newInbound = await messagingService.IngestInboundAsync(scope, inbound, cancellationToken);
            var messagesSaved = newInbound.Count;
            var deliveryUpdates = await messagingService.ApplyDeliveryEventsAsync(scope, statuses, cancellationToken);
            if (newInbound.Count > 0)
            {
                // The messages are already durably saved above, so Meta is answered now and the
                // assistant runs in the background. Handling it inside this request meant a slow
                // assistant outlasted Meta's patience, and a dropped connection cancelled the work
                // after the message was saved, so the retry saw it as already handled and the
                // customer was never answered. Only messages seen for the first time reach the
                // queue, so a redelivered webhook neither answers twice nor loses the message.
                var assistantRequest = new WhatsAppAssistantRequest(
                    scope, newInbound, request.HttpContext.TraceIdentifier, options.WhatsApp.AssistantAutoReplyEnabled);
                if (!assistantQueue.TryEnqueue(assistantRequest))
                {
                    // The queue is full. Never drop a customer's message: handle it here instead,
                    // on a token that survives the caller hanging up.
                    logger.LogWarning("WhatsApp assistant queue is full; handling a batch inline for tenant {TenantId}", scope.TenantId);
                    _ = WhatsAppAssistantWorker.RunAsync(assistantRequest, scopeFactory, logger, CancellationToken.None);
                }
            }
            return Results.Ok(new
            {
                status = "Accepted",
                messagesSaved,
                deliveryUpdates,
                mediaMessagesSeen = result.MediaMessagesSeen,
                documentsStored = result.DocumentsStored,
                manualReviewRequired = result.ManualReviewRequired
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.BadRequest(new { message = "WhatsApp webhook payload was not valid JSON." });
        }
    }

    public static async Task<IResult> GetThread(
        string threadId,
        IWhatsAppMessagingService service,
        CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.GetThreadAsync(threadId, cancellationToken)); }
        catch (PermissionDeniedException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (ArgumentException error) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["threadId"] = [error.Message] }); }
    }

    public static async Task<IResult> SendMessage(
        string threadId,
        SendWhatsAppMessageRequest request,
        IWhatsAppMessagingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await service.SendAsync(new SendWhatsAppMessageCommand(
                request.CaseId, threadId, request.RecipientWaId, request.CustomerSafeBody,
                request.Purpose, request.ConsentBasis, request.Language, request.TemplateName,
                request.HumanApproved, request.IdempotencyKey, request.ReplyToProviderMessageId,
                request.TemplateParameters), cancellationToken);
            return message.DeliveryState switch
            {
                WhatsAppDeliveryState.Failed => Results.BadRequest(message),
                WhatsAppDeliveryState.Unknown => Results.Json(message, statusCode: StatusCodes.Status202Accepted),
                _ => Results.Ok(message)
            };
        }
        catch (PermissionDeniedException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (WhatsAppPolicyException error)
        {
            var status = error.Code == "ConfigurationRequired" ? StatusCodes.Status503ServiceUnavailable
                : error.Code is "CaseNotFound" or "RecipientNotVerified" ? StatusCodes.Status404NotFound
                : error.Code == "IdempotencyConflict" ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Json(new { code = error.Code, message = error.Message }, statusCode: status);
        }
    }
}

public sealed record SendWhatsAppMessageRequest(
    Guid CaseId,
    string RecipientWaId,
    string CustomerSafeBody,
    string Purpose,
    string ConsentBasis,
    string Language,
    string? TemplateName,
    bool HumanApproved,
    string IdempotencyKey,
    string? ReplyToProviderMessageId,
    IReadOnlyDictionary<string, string>? TemplateParameters);

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;WhatsAppIntegrationEndpoints&gt; is not usable, the class is
/// static), matching SmsIntegrationEndpoints' SmsDeliveryStatusLogMarker precedent.</summary>
public sealed class WhatsAppCustomerAssistantLogMarker;

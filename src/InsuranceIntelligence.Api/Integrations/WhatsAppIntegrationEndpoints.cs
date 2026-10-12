using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.CustomerAssistant;
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
        var settings = settingsProvider.Current ?? settingsProvider.All.FirstOrDefault();
        var mappingConfigured = settings is not null
            && !string.IsNullOrWhiteSpace(settings.TenantId)
            && !string.IsNullOrWhiteSpace(settings.WorkspaceId);
        // A connection saved from the settings screen is live without a deployment change (the
        // providers are registered unconditionally, see InfrastructureRegistration), so it counts
        // as selected here too. Previously this reported "Not configured" for a working, tenant-
        // connected channel whenever INSURANCEINTELLIGENCE_CHANNEL_PROVIDER was still Disabled.
        var providerSelected = WhatsAppChannelProviderSupport.IsLive(options, settingsProvider);
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

        // "Ignored" only says the document pipeline had nothing to store: a plain text message has no
        // attachment, so every text arrived here as "Ignored". Returning on it dropped every text a
        // customer sent (the webhook answered 200 and nothing was saved or answered). Numbers that belong to
        // no insurer here are skipped in the loop below, so there is nothing to short-cut.
        if (!settingsProvider.AnyConfigured)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Not configured",
                detail: "WhatsApp inbound is not configured.");
        }

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(rawBody);
            var allInbound = WhatsAppWebhookParser.ParseMessages(json);
            var allStatuses = WhatsAppWebhookParser.ParseDeliveryEvents(json);
            var messagesSaved = 0;
            var deliveryUpdates = 0;

            // One payload can carry several numbers; each is handled in its own insurer's scope.
            // The signature was already verified for every number that belongs to an insurer here.
            foreach (var phoneNumberId in WhatsAppWebhookParser.ReadPhoneNumberIds(json))
            {
                var settings = settingsProvider.ForPhoneNumberId(phoneNumberId);
                if (settings is null)
                {
                    continue;
                }

                var scope = TenantScope.Create(settings.TenantId, settings.WorkspaceId);
                var inbound = allInbound
                    .Where(message => string.Equals(message.BusinessPhoneNumberId, phoneNumberId, StringComparison.Ordinal))
                    .ToArray();
                var statuses = allStatuses
                    .Where(status => string.Equals(status.BusinessPhoneNumberId, phoneNumberId, StringComparison.Ordinal))
                    .ToArray();
                var newInbound = await messagingService.IngestInboundAsync(scope, inbound, cancellationToken);
                messagesSaved += newInbound.Count;
                deliveryUpdates += await messagingService.ApplyDeliveryEventsAsync(scope, statuses, cancellationToken);
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
        IConversationControlService? control,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await service.SendAsync(new SendWhatsAppMessageCommand(
                request.CaseId, threadId, request.RecipientWaId, request.CustomerSafeBody,
                request.Purpose, request.ConsentBasis, request.Language, request.TemplateName,
                request.HumanApproved, request.IdempotencyKey, request.ReplyToProviderMessageId,
                request.TemplateParameters), cancellationToken);
            // A reply sent by a person is what keeps Kato quiet on this conversation.
            if (control is not null && message.DeliveryState != WhatsAppDeliveryState.Failed)
            {
                await control.RecordReplyAsync(threadId, request.CustomerSafeBody.Trim(), cancellationToken);
            }

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

public sealed record ConversationControlRequest(string? Mode);

public static class ConversationControlEndpoints
{
    public static async Task<IResult> Get(string threadId, IConversationControlService service, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.GetAsync(threadId, cancellationToken)); }
        catch (PermissionDeniedException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
    }

    public static async Task<IResult> Set(string threadId, ConversationControlRequest request, IConversationControlService service, CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToLowerInvariant() switch
        {
            "person" => (ConversationMode?)ConversationMode.Person,
            "kato" => ConversationMode.Kato,
            _ => null,
        };
        if (mode is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["mode"] = ["Mode must be \"person\" or \"kato\"."] });
        }

        try { return Results.Ok(await service.SetAsync(threadId, mode.Value, cancellationToken)); }
        catch (PermissionDeniedException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (ArgumentException error) { return Results.NotFound(new { message = error.Message }); }
    }
}

public sealed record SendWhatsAppMessageRequest(
    Guid? CaseId,
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

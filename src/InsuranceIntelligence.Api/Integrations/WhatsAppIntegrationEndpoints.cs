using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Meta WhatsApp Cloud API webhook surface. GET verifies subscription; POST verifies signature
/// then routes inbound media onto case documents. Returns honest Not configured when the channel
/// provider is not WhatsAppCloudApi or credentials are missing.
/// </summary>
public static class WhatsAppIntegrationEndpoints
{
    public static IResult GetHealth(
        IWhatsAppInboundProvider provider,
        InfrastructureOptions options) =>
        Results.Ok(new
        {
            channel = "whatsapp",
            provider = options.ChannelProvider,
            configured = provider.IsConfigured
                && options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase),
            status = provider.IsConfigured
                && options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase)
                    ? "ready"
                    : "Not configured",
        });

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
        CancellationToken cancellationToken)
    {
        // Read raw body before any JSON model binding so HMAC covers exact bytes Meta signed.
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken);
        var rawBody = memory.ToArray();
        var signature = request.Headers["X-Hub-Signature-256"].ToString();

        var result = await ingestionService.IngestWebhookPayloadAsync(rawBody, signature, cancellationToken);
        return result.Status switch
        {
            "ConfigurationRequired" => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Not configured",
                detail: result.SafeError ?? "WhatsApp inbound is not configured."),
            "InvalidSignature" => Results.Unauthorized(),
            _ => Results.Ok(result),
        };
    }
}

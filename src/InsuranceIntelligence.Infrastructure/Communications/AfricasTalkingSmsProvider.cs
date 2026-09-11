using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// Real outbound SMS adapter for Africa's Talking (the standard USSD/SMS gateway across Uganda
/// and East Africa). Follows the platform provider pattern: real when configured, honest
/// <c>Not configured</c> otherwise, and safe provider errors instead of fabricated success.
/// Live sending requires an Africa's Talking username + API key; the adapter builds and
/// unit-tests without any credentials or network access. Takes AfricasTalkingOptions directly
/// (not the full InfrastructureOptions) so it is trivially constructible either from deployment
/// env-var configuration or from a tenant's decrypted IntegrationConnection settings — see
/// SmsProviderFactory, the seam both paths go through.
/// </summary>
public sealed class AfricasTalkingSmsProvider(
    AfricasTalkingOptions settings,
    bool autoSendTransactionalSmsEnabled,
    ILogger<AfricasTalkingSmsProvider> logger,
    HttpClient? httpClient = null) : ISmsProvider
{
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();

    public async Task<CommunicationSendResult> SendAsync(
        CommunicationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        // Human-supervised boundary: a customer-facing SMS must not leave the platform unless a
        // human has approved it — or it is a low-risk, non-consequential transactional message and
        // the tenant has opted into transactional auto-send. See OutboundSmsGuard.
        if (!OutboundSmsGuard.IsDispatchPermitted(request, autoSendTransactionalSmsEnabled))
        {
            logger.LogInformation(
                "Outbound SMS blocked pending human approval. CaseId={CaseId} CorrelationId={CorrelationId}",
                request.CaseId,
                request.CorrelationId);
            return new CommunicationSendResult(
                "HumanApprovalRequired",
                string.Empty,
                "This SMS requires human approval before it can be sent.");
        }

        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new CommunicationSendResult(
                "ConfigurationRequired",
                string.Empty,
                "SMS provider is not configured.");
        }

        var normalizedRecipient = UgandaMsisdn.Normalize(request.Recipient);
        if (normalizedRecipient is null)
        {
            return new CommunicationSendResult(
                "InvalidRecipient",
                string.Empty,
                "The recipient phone number is not a valid Ugandan mobile number.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return new CommunicationSendResult(
                "EmptyMessage",
                string.Empty,
                "The SMS message body was empty.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, MessagingUri(settings));
            httpRequest.Headers.TryAddWithoutValidation("apiKey", settings.ApiKey);
            httpRequest.Headers.TryAddWithoutValidation("Accept", "application/json");
            var form = new List<KeyValuePair<string, string>>
            {
                new("username", settings.Username!),
                new("to", normalizedRecipient),
                new("message", request.MessageBody),
            };
            if (!string.IsNullOrWhiteSpace(settings.SenderId))
            {
                form.Add(new("from", settings.SenderId!));
            }

            httpRequest.Content = new FormUrlEncodedContent(form);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));
            using var response = await httpClient.SendAsync(httpRequest, timeout.Token);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AfricasTalkingResponse>(
                cancellationToken: timeout.Token);
            var recipient = payload?.SmsMessageData?.Recipients?.FirstOrDefault();
            if (recipient is null)
            {
                return new CommunicationSendResult(
                    "ProviderError",
                    string.Empty,
                    "The SMS gateway accepted the request but returned no recipient status.");
            }

            logger.LogInformation(
                "Africa's Talking SMS dispatched. CaseId={CaseId} Status={Status} CorrelationId={CorrelationId}",
                request.CaseId,
                recipient.Status,
                request.CorrelationId);

            return new CommunicationSendResult(
                NormalizeStatus(recipient.Status),
                recipient.MessageId ?? string.Empty,
                null);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(
                "Africa's Talking SMS send failed safely. CaseId={CaseId} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                request.CaseId,
                request.CorrelationId,
                exception.GetType().Name);
            return new CommunicationSendResult(
                "ProviderError",
                string.Empty,
                "The SMS gateway could not be reached. No message was sent.");
        }
    }

    private static Uri MessagingUri(AfricasTalkingOptions settings) =>
        new($"{settings.BaseUrl.TrimEnd('/')}/version1/messaging");

    private static string NormalizeStatus(string? providerStatus) =>
        string.IsNullOrWhiteSpace(providerStatus)
            ? "Unknown"
            : providerStatus.Replace(" ", string.Empty, StringComparison.Ordinal);

    private sealed record AfricasTalkingResponse(
        [property: JsonPropertyName("SMSMessageData")] SmsMessageData? SmsMessageData);

    private sealed record SmsMessageData(
        [property: JsonPropertyName("Message")] string? Message,
        [property: JsonPropertyName("Recipients")] IReadOnlyList<SmsRecipient>? Recipients);

    private sealed record SmsRecipient(
        [property: JsonPropertyName("number")] string? Number,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("messageId")] string? MessageId,
        [property: JsonPropertyName("cost")] string? Cost);
}

/// <summary>
/// Human-supervised send policy for outbound customer SMS (tiered — see
/// docs/research/sms-auto-send-policy.md, grounded in Uganda's Data Protection and Privacy Act 2019,
/// the UCC Text &amp; Multimedia Messaging Regulations 2019, and AGENTS.md §4/§16).
///
/// A message may leave the platform when EITHER:
///  - a human has explicitly approved it (<c>request.Approved</c>); or
///  - it is low-risk / non-consequential (<c>request.LowRisk</c>, set upstream to
///    <c>!RequiresApproval</c> — i.e. a transactional status notice, not a claim decision) AND the
///    tenant has opted into transactional auto-send.
///
/// Consequential messages (claim decisions, payouts, rejections, sensitive content) are never
/// low-risk and therefore always require human approval, regardless of the tenant flag.
/// </summary>
public static class OutboundSmsGuard
{
    public static bool IsDispatchPermitted(
        CommunicationSendRequest request,
        bool autoSendTransactionalEnabled) =>
        request.Approved || (autoSendTransactionalEnabled && request.LowRisk);
}

/// <summary>Normalizes Ugandan mobile numbers to E.164 (+256XXXXXXXXX). Returns null if invalid.</summary>
public static class UgandaMsisdn
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // Reduce to the 9-digit national subscriber number (Ugandan mobile numbers start with 7).
        string? local = digits switch
        {
            { Length: 12 } when digits.StartsWith("256", StringComparison.Ordinal) => digits[3..],
            { Length: 10 } when digits.StartsWith("0", StringComparison.Ordinal) => digits[1..],
            { Length: 9 } => digits,
            _ => null,
        };

        if (local is null || local.Length != 9 || local[0] != '7')
        {
            return null;
        }

        return $"+256{local}";
    }
}

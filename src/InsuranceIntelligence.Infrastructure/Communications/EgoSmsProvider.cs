using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// Tenant-supplied settings for the EgoSMS / Comms Platform gateway (docs/comms-api-reference.md).
/// Parsed defensively from the flat connection settings dictionary — never throws on a malformed
/// or missing field; callers get a clear error string instead.
/// </summary>
public sealed record EgoSmsSettings(
    string Username,
    string ApiKey,
    string? SenderId,
    string BaseUrl,
    int RequestTimeoutSeconds)
{
    public const string SandboxBaseUrl = "https://sandbox.egosms.co/api/v1/json/";
    public const string LiveBaseUrl = "https://comms.egosms.co/api/v1/json/";

    public static bool TryParse(IReadOnlyDictionary<string, string> settings, out EgoSmsSettings? parsed, out string? error)
    {
        parsed = null;
        if (!settings.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            error = "An EgoSMS API username is required.";
            return false;
        }

        if (!settings.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            error = "An EgoSMS API key is required.";
            return false;
        }

        settings.TryGetValue("senderId", out var senderId);
        if (!string.IsNullOrWhiteSpace(senderId) && senderId.Length > 11)
        {
            error = "The EgoSMS sender ID must be 11 characters or fewer.";
            return false;
        }

        var sandbox = settings.TryGetValue("sandbox", out var sandboxRaw) && bool.TryParse(sandboxRaw, out var sandboxValue) && sandboxValue;
        var baseUrl = settings.TryGetValue("baseUrl", out var explicitBaseUrl) && !string.IsNullOrWhiteSpace(explicitBaseUrl)
            ? explicitBaseUrl
            : sandbox ? SandboxBaseUrl : LiveBaseUrl;

        var timeoutSeconds = settings.TryGetValue("requestTimeoutSeconds", out var timeoutRaw) && int.TryParse(timeoutRaw, out var parsedTimeout)
            ? parsedTimeout
            : 15;

        parsed = new EgoSmsSettings(username.Trim(), apiKey.Trim(), string.IsNullOrWhiteSpace(senderId) ? null : senderId.Trim(), baseUrl, timeoutSeconds);
        error = null;
        return true;
    }
}

/// <summary>
/// Real outbound SMS adapter for EgoSMS / Comms Platform (docs/comms-api-reference.md) — a second,
/// independently working ISmsProvider alongside AfricasTalkingSmsProvider, demonstrating that
/// adding an SMS gateway to this platform is a new class, not a re-architecture. Follows the same
/// provider contract exactly: the human-approval guard runs first, credentials are checked before
/// any network call, phone numbers are normalized and validated, and provider errors are
/// sanitized before ever reaching a caller.
/// </summary>
public sealed class EgoSmsProvider(
    EgoSmsSettings settings,
    bool autoSendTransactionalSmsEnabled,
    ILogger<EgoSmsProvider> logger,
    HttpClient? httpClient = null) : ISmsProvider
{
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();

    public async Task<CommunicationSendResult> SendAsync(
        CommunicationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        // Human-supervised boundary — identical guard to AfricasTalkingSmsProvider. Every SMS
        // provider must enforce this independently; it is not safe to assume a shared caller does.
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
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "SMS provider is not configured.");
        }

        var normalizedRecipient = UgandaMsisdn.Normalize(request.Recipient);
        if (normalizedRecipient is null)
        {
            return new CommunicationSendResult("InvalidRecipient", string.Empty, "The recipient phone number is not a valid Ugandan mobile number.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return new CommunicationSendResult("EmptyMessage", string.Empty, "The SMS message body was empty.");
        }

        // EgoSMS documents numbers without the leading '+' (e.g. "256700111222"), unlike Africa's
        // Talking's E.164 "+256700111222" — strip it here rather than changing UgandaMsisdn.Normalize,
        // whose '+' prefix other call sites (the claimant portal, WhatsApp) rely on.
        var egoRecipient = normalizedRecipient.TrimStart('+');

        try
        {
            var body = new EgoSmsSendRequest(
                "SendSms",
                new EgoSmsUserData(settings.Username, settings.ApiKey),
                [new EgoSmsMessage(egoRecipient, request.MessageBody, settings.SenderId ?? "IIP", DefaultPriority)]);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));
            using var response = await httpClient.PostAsJsonAsync(settings.BaseUrl, body, JsonOptions, timeout.Token);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<EgoSmsSendResponse>(JsonOptions, timeout.Token);
            if (payload is null)
            {
                return new CommunicationSendResult("ProviderError", string.Empty, "The SMS gateway returned an unreadable response.");
            }

            if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "EgoSMS send failed. CaseId={CaseId} CorrelationId={CorrelationId} Status={Status}",
                    request.CaseId, request.CorrelationId, payload.Status);
                return new CommunicationSendResult("ProviderError", string.Empty, "The SMS gateway declined the message. Verify the sender ID and account balance.");
            }

            logger.LogInformation(
                "EgoSMS dispatched. CaseId={CaseId} CorrelationId={CorrelationId}",
                request.CaseId, request.CorrelationId);

            return new CommunicationSendResult("Sent", payload.MsgFollowUpUniqueCode ?? string.Empty, null);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(
                "EgoSMS send failed safely. CaseId={CaseId} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                request.CaseId, request.CorrelationId, exception.GetType().Name);
            return new CommunicationSendResult("ProviderError", string.Empty, "The SMS gateway could not be reached. No message was sent.");
        }
    }

    // "1" (HIGH) is the documented default priority; lower values are higher priority per
    // docs/comms-api-reference.md. CommunicationSendRequest carries no purpose signal distinct
    // enough to justify differentiating OTP/magic-link traffic from other transactional SMS today.
    private const string DefaultPriority = "1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record EgoSmsSendRequest(string method, EgoSmsUserData userdata, EgoSmsMessage[] msgdata);
    private sealed record EgoSmsUserData(string username, string password);
    private sealed record EgoSmsMessage(string number, string message, string senderid, string priority);

    private sealed record EgoSmsSendResponse(
        [property: JsonPropertyName("Status")] string? Status,
        [property: JsonPropertyName("Message")] string? Message,
        [property: JsonPropertyName("Cost")] string? Cost,
        [property: JsonPropertyName("MsgFollowUpUniqueCode")] string? MsgFollowUpUniqueCode);
}

/// <summary>
/// Validates EgoSMS credentials via the Balance API — proves the account is real and reachable
/// without sending a message or spending money. See docs/comms-api-reference.md "Checking Balance".
/// </summary>
public sealed class EgoSmsConnectionValidator(IHttpClientFactory httpClientFactory) : IConnectionValidator
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient(nameof(EgoSmsConnectionValidator));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ConnectionType ConnectionType => ConnectionType.Sms;
    public string ProviderKey => "EgoSms";

    public async Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        if (!EgoSmsSettings.TryParse(settings, out var parsed, out var parseError) || parsed is null)
        {
            return new ConnectionValidationResult(false, parseError);
        }

        try
        {
            var body = new { method = "Balance", userdata = new { username = parsed.Username, password = parsed.ApiKey } };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(parsed.RequestTimeoutSeconds, 5, 60)));
            using var response = await httpClient.PostAsJsonAsync(parsed.BaseUrl, body, JsonOptions, timeout.Token);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<BalanceResponse>(JsonOptions, timeout.Token);
            if (payload is null || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return new ConnectionValidationResult(false, "EgoSMS rejected these credentials. Verify the API username and key.");
            }

            var discovered = new Dictionary<string, string> { ["balance"] = payload.Balance?.ToString() ?? "unknown" };
            return new ConnectionValidationResult(true, null, discovered);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ConnectionValidationResult(false, "Could not reach the EgoSMS gateway to verify these credentials.");
        }
    }

    private sealed record BalanceResponse(
        [property: JsonPropertyName("Status")] string? Status,
        [property: JsonPropertyName("Balance")] double? Balance);
}

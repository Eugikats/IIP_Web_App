using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

public sealed class MetaGraphWhatsAppValidationClient(HttpClient httpClient) : IWhatsAppGraphValidationClient
{
    public async Task<WhatsAppGraphValidationResult> ValidateAsync(
        string graphBaseUrl,
        string phoneNumberId,
        string accessToken,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 30)));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(phoneNumberId)}?fields=display_phone_number,verified_name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new WhatsAppGraphValidationResult(false, null, null, SafeErrorFrom(body, response.StatusCode));
            }

            var parsed = JsonSerializer.Deserialize<PhoneNumberResponse>(body);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.DisplayPhoneNumber))
            {
                return new WhatsAppGraphValidationResult(false, null, null, "Meta returned an unexpected response for this phone number id.");
            }

            return new WhatsAppGraphValidationResult(true, parsed.DisplayPhoneNumber, parsed.VerifiedName, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WhatsAppGraphValidationResult(false, null, null, "Meta did not respond in time. Try again.");
        }
        catch (HttpRequestException)
        {
            return new WhatsAppGraphValidationResult(false, null, null, "Could not reach the Meta Graph API. Check connectivity and try again.");
        }
        catch (JsonException)
        {
            return new WhatsAppGraphValidationResult(false, null, null, "Meta returned a response IIP could not parse.");
        }
    }

    public async Task<WhatsAppWebhookRegistrationResult> RegisterWebhookAsync(
        string graphBaseUrl,
        string wabaId,
        string accessToken,
        string callbackUrl,
        string verifyToken,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        // Meta caps override_callback_uri at 200 characters (blueprint §4.3), fail locally with a
        // clear reason rather than let Meta reject it opaquely.
        if (callbackUrl.Length > 200)
        {
            return new WhatsAppWebhookRegistrationResult(false, "The webhook callback URL exceeds Meta's 200-character limit, shorten the configured API public base URL.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 30)));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{graphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(wabaId)}/subscribed_apps");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new SubscribedAppsRequest(callbackUrl, verifyToken));

        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new WhatsAppWebhookRegistrationResult(false, SafeWebhookErrorFrom(body, response.StatusCode));
            }

            var parsed = JsonSerializer.Deserialize<SubscribedAppsResponse>(body);
            return parsed?.Success == true
                ? new WhatsAppWebhookRegistrationResult(true, null)
                : new WhatsAppWebhookRegistrationResult(false, "Meta accepted the request but did not confirm the webhook was registered.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WhatsAppWebhookRegistrationResult(false, "Meta did not respond in time while registering the webhook.");
        }
        catch (HttpRequestException)
        {
            return new WhatsAppWebhookRegistrationResult(false, "Could not reach the Meta Graph API to register the webhook.");
        }
        catch (JsonException)
        {
            return new WhatsAppWebhookRegistrationResult(false, "Meta returned a response IIP could not parse while registering the webhook.");
        }
    }

    private static string SafeWebhookErrorFrom(string body, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var error = JsonSerializer.Deserialize<GraphErrorEnvelope>(body);
            var message = error?.Error?.Message;
            if (!string.IsNullOrWhiteSpace(message) && message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            {
                return "The access token is missing the whatsapp_business_management permission required to register a webhook. Add that scope to the token in Meta Business Settings, or register the webhook manually using the callback URL and verify token shown below.";
            }

            return string.IsNullOrWhiteSpace(message)
                ? $"Meta rejected the webhook registration request ({(int)statusCode})."
                : "Meta rejected the webhook registration request. Register it manually using the callback URL and verify token shown below.";
        }
        catch (JsonException)
        {
            return $"Meta rejected the webhook registration request ({(int)statusCode}).";
        }
    }

    private sealed record SubscribedAppsRequest(
        [property: JsonPropertyName("override_callback_uri")] string OverrideCallbackUri,
        [property: JsonPropertyName("verify_token")] string VerifyToken);

    private sealed record SubscribedAppsResponse([property: JsonPropertyName("success")] bool Success);

    private static string SafeErrorFrom(string body, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var error = JsonSerializer.Deserialize<GraphErrorEnvelope>(body);
            var message = error?.Error?.Message;
            // Never echo Meta's raw error text verbatim to the browser, it can include the
            // access token's own app id and other adapter internals (A/A9-style boundary already
            // applied elsewhere in this codebase to provider errors).
            return string.IsNullOrWhiteSpace(message)
                ? $"Meta rejected this phone number id or access token ({(int)statusCode})."
                : SummarizeKnownError(message);
        }
        catch (JsonException)
        {
            return $"Meta rejected this phone number id or access token ({(int)statusCode}).";
        }
    }

    private static string SummarizeKnownError(string message) => message switch
    {
        _ when message.Contains("Invalid OAuth", StringComparison.OrdinalIgnoreCase)
            => "The access token was rejected. Generate a new permanent token in Meta Business Settings and try again.",
        _ when message.Contains("Unsupported get request", StringComparison.OrdinalIgnoreCase)
            => "The phone number id was not found. Double-check it in Meta Business Settings.",
        _ => "Meta rejected this phone number id or access token. Double-check both values."
    };

    private sealed record PhoneNumberResponse(
        [property: JsonPropertyName("display_phone_number")] string? DisplayPhoneNumber,
        [property: JsonPropertyName("verified_name")] string? VerifiedName);

    private sealed record GraphErrorEnvelope([property: JsonPropertyName("error")] GraphError? Error);
    private sealed record GraphError([property: JsonPropertyName("message")] string? Message);
}

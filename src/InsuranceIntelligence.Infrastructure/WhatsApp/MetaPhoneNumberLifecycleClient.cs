using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>Meta's number verification and registration calls. Verification codes and PINs are
/// request values only and are never persisted or included in logs.</summary>
public sealed class MetaPhoneNumberLifecycleClient(HttpClient http, IOptions<InfrastructureOptions> options)
    : IWhatsAppPhoneNumberLifecycleClient
{
    public Task<WhatsAppPhoneNumberResult> RequestCodeAsync(string phoneNumberId, string method, string language, string accessToken, CancellationToken cancellationToken = default)
    {
        var normalized = method.Trim().ToUpperInvariant();
        if (normalized is not ("SMS" or "VOICE"))
            return Task.FromResult(new WhatsAppPhoneNumberResult(false, "Choose SMS or voice verification.", Failure: WhatsAppPhoneNumberFailure.Other));
        return PostAsync(phoneNumberId, "request_code", new { code_method = normalized, language = string.IsNullOrWhiteSpace(language) ? "en_US" : language }, accessToken, cancellationToken);
    }

    public Task<WhatsAppPhoneNumberResult> VerifyCodeAsync(string phoneNumberId, string code, string accessToken, CancellationToken cancellationToken = default) =>
        PostAsync(phoneNumberId, "verify_code", new { code }, accessToken, cancellationToken);

    public Task<WhatsAppPhoneNumberResult> RegisterAsync(string phoneNumberId, string pin, string accessToken, CancellationToken cancellationToken = default) =>
        PostAsync(phoneNumberId, "register", new { messaging_product = "whatsapp", pin }, accessToken, cancellationToken);

    public Task<WhatsAppPhoneNumberResult> SubscribeAppAsync(string wabaId, string accessToken, CancellationToken cancellationToken = default) =>
        PostAsync(wabaId, "subscribed_apps", new { }, accessToken, cancellationToken);

    private async Task<WhatsAppPhoneNumberResult> PostAsync(string id, string operation, object payload, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new(false, "WhatsApp managed setup is not configured on this platform.", Failure: WhatsAppPhoneNumberFailure.NotConfigured);
        var baseUrl = options.Value.WhatsApp.GraphBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{Uri.EscapeDataString(id)}/{operation}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return new(true);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return MapError(body);
        }
        catch (HttpRequestException)
        {
            return new(false, "Meta could not be reached. Try again shortly.", Failure: WhatsAppPhoneNumberFailure.Other);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Meta did not respond in time. Try again shortly.", Failure: WhatsAppPhoneNumberFailure.Other);
        }
    }

    public static WhatsAppPhoneNumberResult MapError(string body)
    {
        string? code = null;
        string? message = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                code = error.TryGetProperty("code", out var codeElement) ? codeElement.ToString() : null;
                message = error.TryGetProperty("error_user_msg", out var userMessage) ? userMessage.GetString()
                    : error.TryGetProperty("message", out var rawMessage) ? rawMessage.GetString() : null;
            }
        }
        catch (JsonException) { }

        var text = message?.ToLowerInvariant() ?? string.Empty;
        if (code is "4" or "80007" or "80008" or "130429" or "133016" || text.Contains("rate limit") || text.Contains("too many"))
            return new(false, "Too many attempts with Meta. Wait a few minutes before trying again.", code, WhatsAppPhoneNumberFailure.RateLimited);
        if (text.Contains("expired")) return new(false, "That code has expired. Request a new code.", code, WhatsAppPhoneNumberFailure.CodeExpired);
        if (text.Contains("code") && (text.Contains("incorrect") || text.Contains("invalid") || text.Contains("mismatch")))
            return new(false, "That code is not correct. Check the latest message and try again.", code, WhatsAppPhoneNumberFailure.WrongCode);
        if ((text.Contains("already") && text.Contains("whatsapp")) || text.Contains("registered to"))
            return new(false, "This number is still active in the WhatsApp app. Remove it there before continuing.", code, WhatsAppPhoneNumberFailure.NumberInUse);
        if (text.Contains("display name") || text.Contains("name_status"))
            return new(false, "Meta rejected the display name. Publisher Support must review it.", code, WhatsAppPhoneNumberFailure.DisplayNameRejected);
        return new(false, "Meta could not complete this step. Publisher Support will review the request.", code, WhatsAppPhoneNumberFailure.Other);
    }
}

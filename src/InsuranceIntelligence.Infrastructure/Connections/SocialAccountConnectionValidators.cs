using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>Checks ownership of a fixed provider account. This does not activate message transport.</summary>
public sealed class SocialAccountConnectionValidator(ConnectionType connectionType, HttpClient httpClient) : IConnectionValidator
{
    public ConnectionType ConnectionType { get; } = connectionType;
    public string ProviderKey => ConnectionType switch
    {
        ConnectionType.Instagram => "MetaInstagram",
        ConnectionType.Messenger => "MetaMessenger",
        ConnectionType.TikTok => "TikTokBusiness",
        ConnectionType.X => "XApi",
        _ => throw new InvalidOperationException("Unsupported social account validator."),
    };

    public async Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        var idKey = ConnectionType switch
        {
            ConnectionType.Instagram => "accountId",
            ConnectionType.Messenger => "pageId",
            ConnectionType.TikTok => "businessId",
            ConnectionType.X => "userId",
            _ => throw new InvalidOperationException("Unsupported social account validator."),
        };
        var tokenKey = ConnectionType == ConnectionType.Messenger ? "pageAccessToken" : "accessToken";
        if (!settings.TryGetValue(idKey, out var expectedId) || string.IsNullOrWhiteSpace(expectedId)
            || !settings.TryGetValue(tokenKey, out var token) || string.IsNullOrWhiteSpace(token))
            return new ConnectionValidationResult(false, "An account ID and access token are required.");
        if (expectedId.Length > 256 || token.Length > 8192 || token.Any(char.IsControl))
            return new ConnectionValidationResult(false, "The account ID or access token has an invalid format.");
        if ((ConnectionType == ConnectionType.Instagram || ConnectionType == ConnectionType.Messenger || ConnectionType == ConnectionType.TikTok)
            && (!settings.TryGetValue("appSecret", out var appSecret) || string.IsNullOrWhiteSpace(appSecret)))
            return new ConnectionValidationResult(false, "The provider app secret is required for signed webhooks.");
        if (ConnectionType == ConnectionType.X
            && !settings.Any(pair => (pair.Key.Equals("clientSecret", StringComparison.OrdinalIgnoreCase)
                    || pair.Key.Equals("consumerSecret", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(pair.Value)))
            return new ConnectionValidationResult(false, "An X OAuth app secret is required for signed webhook validation.");
        if ((ConnectionType == ConnectionType.Instagram || ConnectionType == ConnectionType.Messenger)
            && (!settings.TryGetValue("webhookVerifyToken", out var verifyToken) || string.IsNullOrWhiteSpace(verifyToken)
                || verifyToken.Length > 256 || verifyToken.Any(char.IsControl)))
            return new ConnectionValidationResult(false, "A webhook verification token is required for Meta webhook setup.");

        var endpoint = ConnectionType switch
        {
            ConnectionType.Instagram => "https://graph.instagram.com/me?fields=id,username",
            ConnectionType.Messenger => "https://graph.facebook.com/me?fields=id,name",
            ConnectionType.TikTok => $"https://business-api.tiktok.com/open_api/v1.3/business/get/?business_id={Uri.EscapeDataString(expectedId.Trim())}",
            ConnectionType.X => "https://api.x.com/2/users/me",
            _ => throw new InvalidOperationException("Unsupported social account validator."),
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (ConnectionType == ConnectionType.TikTok)
            request.Headers.TryAddWithoutValidation("Access-Token", token.Trim());
        else if (ConnectionType == ConnectionType.X)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return new ConnectionValidationResult(false, $"{ConnectionType} rejected the account or access token. Check the account ID and permissions.");
            if (response.Content.Headers.ContentLength is > 65536)
                return new ConnectionValidationResult(false, $"{ConnectionType} returned an unexpected account response.");
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 65536)
                return new ConnectionValidationResult(false, $"{ConnectionType} returned an unexpected account response.");
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new ConnectionValidationResult(false, $"{ConnectionType} returned an unexpected account response.");
            return ConnectionType switch
            {
                ConnectionType.TikTok => ValidateTikTok(root, expectedId.Trim()),
                ConnectionType.Instagram => ValidateMeta(root, expectedId.Trim(), "username"),
                ConnectionType.Messenger => ValidateMeta(root, expectedId.Trim(), "name"),
                ConnectionType.X => ValidateX(root, expectedId.Trim()),
                _ => throw new InvalidOperationException("Unsupported social account validator."),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionValidationResult(false, $"{ConnectionType} did not respond in time.");
        }
        catch (HttpRequestException)
        {
            return new ConnectionValidationResult(false, $"Could not reach {ConnectionType} to verify the account.");
        }
        catch (JsonException)
        {
            return new ConnectionValidationResult(false, $"{ConnectionType} returned an unexpected account response.");
        }
    }

    private ConnectionValidationResult ValidateMeta(JsonElement root, string expectedId, string displayField)
    {
        var actualId = StringAt(root, "id");
        if (!string.Equals(actualId, expectedId, StringComparison.Ordinal))
            return new ConnectionValidationResult(false, $"{ConnectionType} returned a different account ID. Check the token and account.");
        var display = StringAt(root, displayField);
        return new ConnectionValidationResult(true, null, new Dictionary<string, string>
        {
            ["accountId"] = actualId!,
            ["displayName"] = display ?? "Verified account",
            ["messagingStatus"] = "Webhook setup required",
        });
    }

    private static ConnectionValidationResult ValidateTikTok(JsonElement root, string expectedId)
    {
        if (!root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var statusCode) || statusCode != 0
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return new ConnectionValidationResult(false, "TikTok rejected the account or access token. Check Business Messaging access.");
        var actualId = StringAt(data, "business_id");
        if (actualId is not null && !string.Equals(actualId, expectedId, StringComparison.Ordinal))
            return new ConnectionValidationResult(false, "TikTok returned a different business account ID.");
        if (data.TryGetProperty("has_advanced_access", out var advanced)
            && advanced.ValueKind == JsonValueKind.False)
            return new ConnectionValidationResult(false, "This TikTok account does not have the advanced access required for business messaging.");
        return new ConnectionValidationResult(true, null, new Dictionary<string, string>
        {
            ["accountId"] = expectedId,
            ["messagingStatus"] = "Business Messaging API access not verified",
        });
    }

    private static ConnectionValidationResult ValidateX(JsonElement root, string expectedId)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !string.Equals(StringAt(data, "id"), expectedId, StringComparison.Ordinal))
            return new ConnectionValidationResult(false, "X returned a different user ID. Check the token and account.");
        return new ConnectionValidationResult(true, null, new Dictionary<string, string>
        {
            ["accountId"] = expectedId,
            ["displayName"] = StringAt(data, "username") ?? "Verified account",
            ["messagingStatus"] = "Direct Messages API access not verified",
        });
    }

    private static string? StringAt(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}

/// <summary>Telegram puts bot tokens in request paths, so this client has no request URL logger.</summary>
public sealed class TelegramConnectionValidator : IConnectionValidator
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });
    private static readonly Regex TokenPattern = new("^[0-9]{5,}:[A-Za-z0-9_-]{20,}$", RegexOptions.Compiled);

    public ConnectionType ConnectionType => ConnectionType.Telegram;
    public string ProviderKey => "TelegramBot";

    public async Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.TryGetValue("botToken", out var token) || string.IsNullOrWhiteSpace(token) || token.Length > 256
            || !TokenPattern.IsMatch(token.Trim()))
            return new ConnectionValidationResult(false, "Enter a valid Telegram bot token from BotFather.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.telegram.org/bot{token.Trim()}/getMe");
        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return new ConnectionValidationResult(false, "Telegram rejected this bot token.");
            if (response.Content.Headers.ContentLength is > 65536)
                return new ConnectionValidationResult(false, "Telegram returned an unexpected bot response.");
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 65536)
                return new ConnectionValidationResult(false, "Telegram returned an unexpected bot response.");
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object
                || !result.TryGetProperty("id", out var botId) || botId.ValueKind != JsonValueKind.Number)
                return new ConnectionValidationResult(false, "Telegram did not confirm the bot identity.");
            var username = result.TryGetProperty("username", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() : null;
            var accountId = botId.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var messagingStatus = settings.ContainsKey("webhookSecret")
                ? await ReadWebhookStatusAsync(token.Trim(), accountId, timeout.Token)
                : "Not configured";
            return new ConnectionValidationResult(true, null, new Dictionary<string, string>
            {
                ["accountId"] = accountId,
                ["displayName"] = username ?? "Verified bot",
                ["messagingStatus"] = messagingStatus,
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionValidationResult(false, "Telegram did not respond in time.");
        }
        catch (HttpRequestException)
        {
            return new ConnectionValidationResult(false, "Could not reach Telegram to verify the bot.");
        }
        catch (JsonException)
        {
            return new ConnectionValidationResult(false, "Telegram returned an unexpected bot response.");
        }
    }

    private static async Task<string> ReadWebhookStatusAsync(string token, string accountId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.telegram.org/bot{token}/getWebhookInfo");
        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 16384) return "Webhook status unavailable";
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (payload.Length > 16384) return "Webhook status unavailable";
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object
                || !result.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
                return "Not configured";
            var url = urlElement.GetString();
            var expectedSuffix = $"/api/v1/integrations/telegram/bots/{accountId}/webhook";
            var hasRecentError = result.TryGetProperty("last_error_date", out var errorDate)
                && errorDate.ValueKind == JsonValueKind.Number;
            return Uri.TryCreate(url, UriKind.Absolute, out var callback)
                && callback.Scheme == Uri.UriSchemeHttps
                && callback.AbsolutePath.EndsWith(expectedSuffix, StringComparison.Ordinal)
                && !hasRecentError
                    ? "Human replies"
                    : "Not configured";
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            return "Webhook status unavailable";
        }
    }
}

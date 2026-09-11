using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Graph;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// Builds a live ISmsProvider from a provider key and settings — either a tenant's decrypted
/// IntegrationConnection settings, or (via CreateFallback) the deployment's environment
/// configuration. Registering a third SMS gateway means adding one case here and one
/// IConnectionValidator; nothing else in the resolution chain changes.
/// </summary>
public sealed class SmsProviderFactory(InfrastructureOptions options, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory) : ISmsProviderFactory
{
    public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["AfricasTalking", "EgoSms"];

    public ISmsProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings)
    {
        var autoSend = options.AfricasTalking.AutoSendTransactionalSmsEnabled;

        if (string.Equals(providerKey, "AfricasTalking", StringComparison.OrdinalIgnoreCase))
        {
            var atSettings = new AfricasTalkingOptions
            {
                Username = settings.GetValueOrDefault("username"),
                ApiKey = settings.GetValueOrDefault("apiKey"),
                SenderId = settings.GetValueOrDefault("senderId"),
                BaseUrl = settings.GetValueOrDefault("baseUrl", "https://api.africastalking.com"),
                RequestTimeoutSeconds = int.TryParse(settings.GetValueOrDefault("requestTimeoutSeconds"), out var atTimeout) ? atTimeout : 15,
            };
            return new AfricasTalkingSmsProvider(atSettings, autoSend, loggerFactory.CreateLogger<AfricasTalkingSmsProvider>(), httpClientFactory.CreateClient(nameof(AfricasTalkingSmsProvider)));
        }

        if (string.Equals(providerKey, "EgoSms", StringComparison.OrdinalIgnoreCase))
        {
            return EgoSmsSettings.TryParse(settings, out var egoSettings, out _) && egoSettings is not null
                ? new EgoSmsProvider(egoSettings, autoSend, loggerFactory.CreateLogger<EgoSmsProvider>(), httpClientFactory.CreateClient(nameof(EgoSmsProvider)))
                : null;
        }

        return null;
    }

    public ISmsProvider CreateFallback()
    {
        if (options.SmsProvider.Equals("AfricasTalking", StringComparison.OrdinalIgnoreCase))
        {
            return new AfricasTalkingSmsProvider(
                options.AfricasTalking,
                options.AfricasTalking.AutoSendTransactionalSmsEnabled,
                loggerFactory.CreateLogger<AfricasTalkingSmsProvider>(),
                httpClientFactory.CreateClient(nameof(AfricasTalkingSmsProvider)));
        }

        return new DisabledSmsProvider();
    }
}

/// <summary>
/// Validates Africa's Talking credentials via the Application Data endpoint, which returns the
/// account balance without sending a message or spending money — mirroring EgoSmsConnectionValidator.
/// </summary>
public sealed class AfricasTalkingConnectionValidator(IHttpClientFactory httpClientFactory) : IConnectionValidator
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient(nameof(AfricasTalkingConnectionValidator));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ConnectionType ConnectionType => ConnectionType.Sms;
    public string ProviderKey => "AfricasTalking";

    public async Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            return new ConnectionValidationResult(false, "An Africa's Talking username is required.");
        }

        if (!settings.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new ConnectionValidationResult(false, "An Africa's Talking API key is required.");
        }

        var baseUrl = settings.GetValueOrDefault("baseUrl", "https://api.africastalking.com");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/version1/user?username={Uri.EscapeDataString(username)}");
            request.Headers.TryAddWithoutValidation("apiKey", apiKey);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new ConnectionValidationResult(false, "Africa's Talking rejected these credentials. Verify the username and API key.");
            }

            var payload = await response.Content.ReadFromJsonAsync<UserDataResponse>(JsonOptions, timeout.Token);
            var balance = payload?.UserData?.Balance;
            var discovered = balance is not null
                ? new Dictionary<string, string> { ["balance"] = balance }
                : null;
            return new ConnectionValidationResult(true, null, discovered);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ConnectionValidationResult(false, "Could not reach Africa's Talking to verify these credentials.");
        }
    }

    private sealed record UserDataResponse([property: JsonPropertyName("UserData")] UserData? UserData);
    private sealed record UserData([property: JsonPropertyName("balance")] string? Balance);
}

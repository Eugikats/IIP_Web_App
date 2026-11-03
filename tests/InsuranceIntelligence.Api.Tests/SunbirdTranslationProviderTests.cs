using System.Net;
using System.Text.Json;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Translation;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The connector to Sunbird AI, configured per tenant from /app/admin/connections. The verbatim-only
/// instruction is the whole point of using the chat endpoint rather than Sunbird's own /translate,
/// so its wording is pinned here: a harmless-looking edit that softens it is exactly the regression
/// this guards against.
/// </summary>
public sealed class SunbirdTranslationProviderTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");

    private static SunbirdSettings Settings(string model = "sunflower-9b") =>
        new("secret-key", "https://api.sunbird.example.test", model, 30);

    [Fact]
    public void TheSystemPromptDemandsAVerbatimTranslationAndNothingElse()
    {
        var system = SunbirdTranslationProvider.BuildSystemPrompt("English", "Luganda");

        Assert.StartsWith("You are a literal, verbatim translation engine from English to Luganda.", system);
        Assert.Contains("Do not paraphrase, do not summarize, do not add information, do not remove information", system);
        Assert.Contains("If the input is a question, translate the question, do not answer it.", system);
        Assert.Contains("reproduce it unchanged", system);
        // TranslationService masks names and protected terms as [TKN0]-style placeholders and refuses
        // any translation that drops one, so the engine must be told to keep them.
        Assert.Contains("Copy every placeholder in square brackets, such as [TKN0], exactly as written", system);
        Assert.EndsWith("Your entire response must be the translation and nothing more.", system);
        Assert.DoesNotContain("cultural", system);
        Assert.DoesNotContain("nuances", system);
    }

    [Fact]
    public void TheSourceTextIsFencedOffFromTheInstruction()
    {
        var user = SunbirdTranslationProvider.BuildUserPrompt("English", "Luganda", "Please send photos.");

        Assert.Equal(
            "Text to translate from English to Luganda (between the markers, translate only what is between them):\n<<<\nPlease send photos.\n>>>",
            user);
    }

    [Fact]
    public void TheRequestNamesTheModelAndAsksForNoStreamingAndNoRandomness()
    {
        using var body = JsonDocument.Parse(
            SunbirdTranslationProvider.BuildRequestBody("Habari", SupportedLanguageCode.Sw, SupportedLanguageCode.En, "sunflower-14b"));
        var root = body.RootElement;

        Assert.Equal("sunflower-14b", root.GetProperty("model").GetString());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.False(root.GetProperty("stream").GetBoolean());
        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("from Swahili to English", messages[0].GetProperty("content").GetString());
        Assert.Contains("<<<\nHabari\n>>>", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ATranslationIsPostedToTheChatEndpointWithTheBearerKey()
    {
        var handler = new StubHandler(_ => Json("""{"choices":[{"message":{"content":"Weereza ebifaananyi."}}]}"""));
        var provider = new SunbirdTranslationProvider(Settings(), new HttpClient(handler));

        var text = await provider.TranslateAsync("Please send photos.", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope);

        Assert.Equal("Weereza ebifaananyi.", text);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.sunbird.example.test/tasks/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-key", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task TheModelTagNamesTheModelSoSwitchingModelsNeverReusesACachedTranslation()
    {
        var nine = new SunbirdTranslationProvider(Settings("sunflower-9b"), new HttpClient(new StubHandler(_ => Json("{}"))));
        var fourteen = new SunbirdTranslationProvider(Settings("sunflower-14b"), new HttpClient(new StubHandler(_ => Json("{}"))));

        Assert.Equal("sunbird:sunflower-9b", await nine.GetModelTagAsync(Scope));
        Assert.NotEqual(await nine.GetModelTagAsync(Scope), await fourteen.GetModelTagAsync(Scope));
    }

    [Fact]
    public async Task AnErrorFromSunbirdMakesTheServiceFallBackToTheOriginalText()
    {
        var provider = new SunbirdTranslationProvider(Settings(), new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        var service = new TranslationService(provider, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Habari yako", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.False(outcome.Translated);
        Assert.Equal("Habari yako", outcome.Text);
        Assert.Equal(TranslationFailureReason.ProviderUnavailable, outcome.Failure);
    }

    [Fact]
    public void SettingsNeedAnApiKeyAndDefaultTheEndpointAndModel()
    {
        Assert.False(SunbirdSettings.TryParse(new Dictionary<string, string>(), out _, out var error));
        Assert.Equal("A Sunbird AI API key is required.", error);

        Assert.True(SunbirdSettings.TryParse(new Dictionary<string, string> { ["apiKey"] = " key " }, out var parsed, out _));
        Assert.Equal("key", parsed!.ApiKey);
        Assert.Equal(SunbirdSettings.DefaultEndpoint, parsed.Endpoint);
        Assert.Equal(SunbirdSettings.DefaultModel, parsed.Model);

        Assert.True(SunbirdSettings.TryParse(
            new Dictionary<string, string> { ["apiKey"] = "key", ["endpoint"] = "https://proxy.example.test/", ["model"] = "sunflower-14b" },
            out var custom, out _));
        Assert.Equal("https://proxy.example.test", custom!.Endpoint);
        Assert.Equal("sunflower-14b", custom.Model);
    }

    [Fact]
    public async Task ValidationRunsARealSampleTranslationAndShowsIt()
    {
        var handler = new StubHandler(_ => Json("""{"choices":[{"message":{"content":"Weereza ebifaananyi by'okwonooneka."}}]}"""));
        var validator = new SunbirdConnectionValidator(new StubHttpClientFactory(handler));

        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["apiKey"] = "key", ["endpoint"] = "https://api.sunbird.example.test" });

        Assert.True(result.Success);
        Assert.Equal("Weereza ebifaananyi by'okwonooneka.", result.DiscoveredSettings!["sample (English to Luganda)"]);
        Assert.Equal("https://api.sunbird.example.test/tasks/chat/completions", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Sunbird AI rejected this API key. Verify it at sunbird.ai.")]
    [InlineData(HttpStatusCode.NotFound, "Sunbird AI did not accept the request. Check the endpoint and model name.")]
    public async Task ValidationFailsWithAPlainReasonNeverTheRawProviderError(HttpStatusCode status, string expected)
    {
        var validator = new SunbirdConnectionValidator(new StubHttpClientFactory(new StubHandler(_ =>
            new HttpResponseMessage(status) { Content = new StringContent("internal stack trace") })));

        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["apiKey"] = "key" });

        Assert.False(result.Success);
        Assert.Equal(expected, result.SafeError);
    }

    [Fact]
    public async Task ValidationFailsWhenSunbirdAnswersWithNoTranslation()
    {
        var validator = new SunbirdConnectionValidator(new StubHttpClientFactory(new StubHandler(_ => Json("""{"choices":[]}"""))));

        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["apiKey"] = "key" });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AnActiveTenantConnectionRoutesToSunbirdNotTheDeploymentFallback()
    {
        var sunbird = new RecordingProvider("sunbird:sunflower-9b");
        var fallback = new RecordingProvider("afrislm");
        var provider = TenantAware(Connection("Sunbird"), new StubFactory(fallback, sunbird));

        await provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope);

        Assert.True(sunbird.WasCalled);
        Assert.False(fallback.WasCalled);
        Assert.Equal("sunbird:sunflower-9b", await provider.GetModelTagAsync(Scope));
    }

    [Fact]
    public async Task WithNoTenantConnectionTheDeploymentFallbackIsUsed()
    {
        var sunbird = new RecordingProvider("sunbird:sunflower-9b");
        var fallback = new RecordingProvider("afrislm");
        var provider = TenantAware(connection: null, new StubFactory(fallback, sunbird));

        await provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope);

        Assert.True(fallback.WasCalled);
        Assert.False(sunbird.WasCalled);
    }

    [Fact]
    public async Task AConnectionToAnUnrecognisedProviderIsNotConfiguredRatherThanSilentlyUsingTheFallback()
    {
        var fallback = new RecordingProvider("afrislm");
        var provider = TenantAware(Connection("RemovedEngine"), new StubFactory(fallback, new RecordingProvider("sunbird")));

        Assert.False(await provider.IsConfiguredAsync(Scope));
        Assert.Equal(TranslationEngineStatus.NotConfigured, await provider.CheckAsync(Scope));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope));
        Assert.False(fallback.WasCalled);
    }

    private static TenantAwareTranslationProvider TenantAware(IntegrationConnection? connection, ITranslationProviderFactory factory) =>
        new(new StubConnectionRepository(connection), new StubProtector(), factory, NullLogger<TenantAwareTranslationProvider>.Instance);

    private static IntegrationConnection Connection(string providerKey) => new()
    {
        Scope = Scope,
        ConnectionType = ConnectionType.Translation,
        ProviderKey = providerKey,
        EncryptedSecretSettingsJson = "encrypted",
        IsActive = true,
        ConfiguredByUserId = "admin-1",
        ConfiguredAt = DateTimeOffset.UtcNow,
    };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class RecordingProvider(string tag) : ITranslationProvider, ITranslationEngineHealth
    {
        public bool WasCalled { get; private set; }
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(tag);
        public Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(TranslationEngineStatus.Healthy);

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult($"[{to}] {text}");
        }
    }

    private sealed class StubFactory(ITranslationProvider fallback, ITranslationProvider sunbird) : ITranslationProviderFactory
    {
        public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["Sunbird"];

        public ITranslationProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings) =>
            string.Equals(providerKey, "Sunbird", StringComparison.OrdinalIgnoreCase) ? sunbird : null;

        public ITranslationProvider CreateFallback() => fallback;
    }

    private sealed class StubConnectionRepository(IntegrationConnection? connection) : IIntegrationConnectionRepository
    {
        public Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) =>
            Task.FromResult(connectionType == ConnectionType.Translation ? connection : null);
        public Task SaveAsync(IntegrationConnection connectionToSave, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => "{}";
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public void CopiedSourceMarkers_NeverReachTheCustomer() =>
        Assert.Equal("Gyebale ko, nze Kato.", SunbirdTranslationProvider.WithoutMarkers("<<<\nGyebale ko, nze Kato.\n>>>"));
}

using System.Net;
using System.Text.Json;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Translation;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The connector to the AfriSLM model. The model holds its shape only near the prompt it was tuned
/// on, so the exact request is pinned here: paraphrasing "Swahili" as "Kiswahili" is the sort of
/// harmless-looking change that makes a 0.8B model start inventing text.
/// </summary>
public sealed class AfriSlmTranslationProviderTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");

    private static TranslationOptions Options(string? key = "secret") =>
        new() { Endpoint = "https://afrislm.example.test/", ApiKey = key, RequestTimeoutSeconds = 5 };

    [Fact]
    public void ThePromptIsTheOneTheModelWasTunedOn()
    {
        var system = AfriSlmTranslationProvider.BuildSystemPrompt("English", "Swahili");
        var user = AfriSlmTranslationProvider.BuildUserPrompt("English", "Swahili", "Please send photos.");

        Assert.Equal(
            "You are a professional English to Swahili translator. Your goal is to accurately convey the meaning and " +
            "nuances of the original English text while adhering to Swahili grammar, vocabulary, and cultural " +
            "sensitivities. Produce only the Swahili translation, without any additional explanations or commentary.",
            system);
        Assert.Equal("Please translate the following English text into Swahili: Please send photos..\n\nTranslation:", user);
    }

    [Theory]
    [InlineData(SupportedLanguageCode.En, "English")]
    [InlineData(SupportedLanguageCode.Lg, "Luganda")]
    [InlineData(SupportedLanguageCode.Sw, "Swahili")]
    public void LanguagesAreNamedTheWayTheModelCardNamesThem(SupportedLanguageCode code, string expected) =>
        Assert.Equal(expected, AfriSlmTranslationProvider.PromptName(code));

    [Theory]
    [InlineData("hello", 64)] // 1 word: 35, raised to the floor
    [InlineData("one two three four five six seven eight nine ten", 64)] // 10 words: 62, raised to the floor
    [InlineData("a b c d e f g h i j k l m n o p q r s t u v w x y z", 110)] // 26 words: 26*3+32
    public void TheTokenBudgetIsWordsTimesThreePlus32WithinTheBounds(string text, int expected) =>
        Assert.Equal(expected, AfriSlmTranslationProvider.TokenBudget(text));

    [Fact]
    public void ALongTextIsCappedSoALoopingModelStops()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 400));

        Assert.Equal(AfriSlmTranslationProvider.MaxTokens, AfriSlmTranslationProvider.TokenBudget(text));
    }

    [Fact]
    public void TheRequestAsksForNoStreamingAndNoRandomness()
    {
        using var body = JsonDocument.Parse(
            AfriSlmTranslationProvider.BuildRequestBody("Habari", SupportedLanguageCode.Sw, SupportedLanguageCode.En));
        var root = body.RootElement;

        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(64, root.GetProperty("max_tokens").GetInt32());
        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Swahili to English translator", messages[0].GetProperty("content").GetString());
        Assert.Contains("Habari", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ATranslationIsPostedWithTheBearerKeyAndTheContentIsReturned()
    {
        var handler = new StubHandler(_ => Json("""{"choices":[{"message":{"content":"Nimefungua madai yako."}}]}"""));
        var provider = new AfriSlmTranslationProvider(Options(), new HttpClient(handler));

        var text = await provider.TranslateAsync("I have opened your claim.", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);

        Assert.Equal("Nimefungua madai yako.", text);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://afrislm.example.test/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task WithoutAKeyNoAuthorisationHeaderIsSent()
    {
        var handler = new StubHandler(_ => Json("""{"choices":[{"message":{"content":"ok"}}]}"""));
        var provider = new AfriSlmTranslationProvider(Options(key: null), new HttpClient(handler));

        await provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);

        Assert.Null(Assert.Single(handler.Requests).Headers.Authorization);
    }

    [Fact]
    public async Task AnErrorFromTheEngineSurfacesAsAnException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var provider = new AfriSlmTranslationProvider(Options(), new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope));
    }

    [Fact]
    public async Task AnEmptyOrMalformedAnswerIsAnEmptyStringForTheJudgeToReject()
    {
        var handler = new StubHandler(_ => Json("""{"choices":[]}"""));
        var provider = new AfriSlmTranslationProvider(Options(), new HttpClient(handler));

        Assert.Equal(string.Empty, await provider.TranslateAsync("hello", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope));
    }

    [Fact]
    public async Task WithNoEndpointItIsNotConfiguredAndSaysSo()
    {
        var provider = new AfriSlmTranslationProvider(new TranslationOptions(), new HttpClient(new StubHandler(_ => Json("{}"))));

        Assert.False(provider.IsConfigured);
        Assert.Equal(TranslationEngineStatus.NotConfigured, await provider.CheckAsync(Scope));
    }

    [Fact]
    public async Task HealthIsHealthyWhenTheServerAnswersAndFailingWhenItDoesNot()
    {
        var healthy = new AfriSlmTranslationProvider(Options(), new HttpClient(new StubHandler(_ => Json("""{"status":"ok"}"""))));
        var failing = new AfriSlmTranslationProvider(Options(), new HttpClient(new StubHandler(_ => throw new HttpRequestException("down"))));

        Assert.Equal(TranslationEngineStatus.Healthy, await healthy.CheckAsync(Scope));
        Assert.Equal(TranslationEngineStatus.Failing, await failing.CheckAsync(Scope));
    }

    [Fact]
    public async Task AnUnreachableEngineMakesTheServiceFallBackToEnglishInsteadOfThrowing()
    {
        var provider = new AfriSlmTranslationProvider(
            Options(), new HttpClient(new StubHandler(_ => throw new HttpRequestException("down"))));
        var service = new TranslationService(provider, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Habari yako", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.False(outcome.Translated);
        Assert.Equal("Habari yako", outcome.Text);
        Assert.Equal(TranslationFailureReason.ProviderUnavailable, outcome.Failure);
    }

    /// <summary>Runs only when IIP_AFRISLM_TEST_ENDPOINT points at a real llama.cpp server holding
    /// the model, so the connector can be checked against the real thing by hand. Otherwise it does
    /// nothing, and is not evidence of anything.</summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task LiveEngine_TranslatesAKiswahiliClaimMessageIntoEnglish()
    {
        var endpoint = Environment.GetEnvironmentVariable("IIP_AFRISLM_TEST_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        var provider = new AfriSlmTranslationProvider(
            new TranslationOptions { Endpoint = endpoint, RequestTimeoutSeconds = 120 }, new HttpClient { Timeout = TimeSpan.FromSeconds(120) });
        var service = new TranslationService(provider, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Nimepata ajali na gari langu leo asubuhi, sera yangu ni NSA/MO/2026/00033",
            SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.True(outcome.Translated, $"Fell back: {outcome.Failure}");
        Assert.Contains("NSA/MO/2026/00033", outcome.Text);
        Assert.Contains("accident", outcome.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TranslationEngineStatus.Healthy, await provider.CheckAsync(Scope));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}

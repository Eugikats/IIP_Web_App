using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Translation;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class TranslationServiceTests
{
    [Fact]
    public async Task Same_source_and_target_language_is_not_needed()
    {
        var service = new TranslationService(new FakeTranslationProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Nina bima", SupportedLanguageCode.Sw, SupportedLanguageCode.Sw);

        Assert.False(outcome.Translated);
        Assert.Null(outcome.Failure);
        Assert.Equal("Nina bima", outcome.Text);
    }

    [Fact]
    public async Task An_unconfigured_provider_falls_back_to_the_original_text()
    {
        var service = new TranslationService(new FakeTranslationProvider { IsConfigured = false }, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("I had an accident", SupportedLanguageCode.En, SupportedLanguageCode.Lg);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProviderNotConfigured, outcome.Failure);
        Assert.Equal("I had an accident", outcome.Text);
    }

    [Fact]
    public async Task A_valid_translation_is_returned_as_translated_and_not_from_cache_the_first_time()
    {
        var service = new TranslationService(new FakeTranslationProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("I had an accident", SupportedLanguageCode.En, SupportedLanguageCode.Lg);

        Assert.True(outcome.Translated);
        Assert.False(outcome.FromCache);
        Assert.Null(outcome.Failure);
        Assert.Contains("I had an accident", outcome.Text);
    }

    [Fact]
    public async Task A_repeated_request_is_served_from_cache_without_calling_the_provider_again()
    {
        var provider = new CountingProvider();
        var service = new TranslationService(provider, new InMemoryTranslationCache());

        var first = await service.TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw);
        var second = await service.TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task A_provider_that_never_produces_a_valid_translation_falls_back_after_one_retry()
    {
        var provider = new EchoingProvider();
        var service = new TranslationService(provider, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Nina bima", SupportedLanguageCode.Sw, SupportedLanguageCode.En);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.Unchanged, outcome.Failure);
        Assert.Equal("Nina bima", outcome.Text);
        Assert.Equal(2, provider.CallCount); // one attempt, one retry, never a third call
    }

    [Fact]
    public async Task A_policy_number_survives_translation_unchanged()
    {
        var service = new TranslationService(new FakeTranslationProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Your claim on policy NSA/MO/2026/00033 is being reviewed.", SupportedLanguageCode.En, SupportedLanguageCode.Sw);

        Assert.True(outcome.Translated);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text);
    }

    [Fact]
    public void The_cache_key_changes_when_the_model_tag_changes()
    {
        var key = TranslationService.BuildCacheKey(SupportedLanguageCode.En, SupportedLanguageCode.Sw, "hello");

        var expected = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{TranslationService.ModelTag}|{SupportedLanguageCode.En}|{SupportedLanguageCode.Sw}|hello")));
        var withADifferentTag = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"a-different-model.gguf#1|{SupportedLanguageCode.En}|{SupportedLanguageCode.Sw}|hello")));

        Assert.Equal(expected, key);
        Assert.NotEqual(withADifferentTag, key);
    }

    [Fact]
    public async Task A_translation_that_drops_the_policy_number_is_refused_not_repaired()
    {
        var service = new TranslationService(new DroppingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Nimepata ajali, sera yangu ni NSA/MO/2026/00033", SupportedLanguageCode.Sw, SupportedLanguageCode.En);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProtectedTokenLost, outcome.Failure);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text); // the untouched original, number and all
    }

    [Fact]
    public async Task A_provider_that_throws_falls_back_instead_of_failing_the_message()
    {
        var service = new TranslationService(new ThrowingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Nimepata ajali", SupportedLanguageCode.Sw, SupportedLanguageCode.En);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProviderUnavailable, outcome.Failure);
        Assert.Equal("Nimepata ajali", outcome.Text);
    }

    [Fact]
    public async Task Protected_values_come_back_unchanged_when_the_model_keeps_them()
    {
        var service = new TranslationService(new KeepingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Nimepata ajali, sera yangu ni NSA/MO/2026/00033", SupportedLanguageCode.Sw, SupportedLanguageCode.En);

        Assert.True(outcome.Translated);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text);
        Assert.DoesNotContain("TKN", outcome.Text);
    }

    private sealed class DroppingProvider : ITranslationProvider
    {
        public bool IsConfigured => true;

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default) =>
            Task.FromResult("I had an accident and here is my policy");
    }

    private sealed class KeepingProvider : ITranslationProvider
    {
        public bool IsConfigured => true;

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default) =>
            Task.FromResult(text.Contains("[TKN0]") ? "I had an accident, my policy is [TKN0]" : "I had an accident");
    }

    private sealed class ThrowingProvider : ITranslationProvider
    {
        public bool IsConfigured => true;

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("engine down");
    }

    private sealed class CountingProvider : ITranslationProvider
    {
        public bool IsConfigured => true;
        public int CallCount { get; private set; }

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult($"[{to}] {text}");
        }
    }

    /// <summary>Always returns the input unchanged, the simplest documented failure mode, to
    /// prove the retry-then-fallback path calls the provider exactly twice and never more.</summary>
    private sealed class EchoingProvider : ITranslationProvider
    {
        public bool IsConfigured => true;
        public int CallCount { get; private set; }

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(text);
        }
    }
}

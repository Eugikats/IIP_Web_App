using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Translation;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class TranslationServiceTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");

    [Fact]
    public async Task Same_source_and_target_language_is_not_needed()
    {
        var service = new TranslationService(new FakeTranslationProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Nina bima", SupportedLanguageCode.Sw, SupportedLanguageCode.Sw, Scope);

        Assert.False(outcome.Translated);
        Assert.Null(outcome.Failure);
        Assert.Equal("Nina bima", outcome.Text);
    }

    [Fact]
    public async Task An_unconfigured_provider_falls_back_to_the_original_text()
    {
        var service = new TranslationService(new FakeTranslationProvider { IsConfigured = false }, new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("I had an accident", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProviderNotConfigured, outcome.Failure);
        Assert.Equal("I had an accident", outcome.Text);
    }

    [Fact]
    public async Task A_valid_translation_is_returned_as_translated_and_not_from_cache_the_first_time()
    {
        var service = new TranslationService(new FakeTranslationProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("I had an accident", SupportedLanguageCode.En, SupportedLanguageCode.Lg, Scope);

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

        var first = await service.TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);
        var second = await service.TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);

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

        var outcome = await service.TranslateAsync("Nina bima", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

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
            "Your claim on policy NSA/MO/2026/00033 is being reviewed.", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);

        Assert.True(outcome.Translated);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text);
    }

    [Fact]
    public void The_cache_key_changes_when_the_model_tag_changes()
    {
        var key = TranslationService.BuildCacheKey(AfriSlmTranslationProvider.ModelTag, SupportedLanguageCode.En, SupportedLanguageCode.Sw, "hello");

        var expected = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{AfriSlmTranslationProvider.ModelTag}|{SupportedLanguageCode.En}|{SupportedLanguageCode.Sw}|hello")));
        var withADifferentTag = TranslationService.BuildCacheKey("sunbird:sunflower-9b", SupportedLanguageCode.En, SupportedLanguageCode.Sw, "hello");

        Assert.Equal(expected, key);
        Assert.NotEqual(withADifferentTag, key);
    }

    [Fact]
    public async Task A_tenant_that_switches_engines_never_gets_the_previous_engines_cached_translation()
    {
        var cache = new InMemoryTranslationCache();
        var before = new TaggedProvider("afrislm", "old engine text");
        var after = new TaggedProvider("sunbird:sunflower-9b", "new engine text");

        await new TranslationService(before, cache).TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);
        var outcome = await new TranslationService(after, cache).TranslateAsync("I had an accident on Monday", SupportedLanguageCode.En, SupportedLanguageCode.Sw, Scope);

        Assert.False(outcome.FromCache);
        Assert.Equal("new engine text", outcome.Text);
    }

    private sealed class TaggedProvider(string tag, string output) : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(tag);

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(output);
    }

    [Fact]
    public async Task A_translation_that_drops_the_policy_number_is_refused_not_repaired()
    {
        var service = new TranslationService(new DroppingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Nimepata ajali, sera yangu ni NSA/MO/2026/00033", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProtectedTokenLost, outcome.Failure);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text); // the untouched original, number and all
    }

    [Fact]
    public async Task A_provider_that_throws_falls_back_instead_of_failing_the_message()
    {
        var service = new TranslationService(new ThrowingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync("Nimepata ajali", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.False(outcome.Translated);
        Assert.Equal(TranslationFailureReason.ProviderUnavailable, outcome.Failure);
        Assert.Equal("Nimepata ajali", outcome.Text);
    }

    [Fact]
    public async Task Protected_values_come_back_unchanged_when_the_model_keeps_them()
    {
        var service = new TranslationService(new KeepingProvider(), new InMemoryTranslationCache());

        var outcome = await service.TranslateAsync(
            "Nimepata ajali, sera yangu ni NSA/MO/2026/00033", SupportedLanguageCode.Sw, SupportedLanguageCode.En, Scope);

        Assert.True(outcome.Translated);
        Assert.Contains("NSA/MO/2026/00033", outcome.Text);
        Assert.DoesNotContain("TKN", outcome.Text);
    }

    private sealed class DroppingProvider : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("stub");

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult("I had an accident and here is my policy");
    }

    private sealed class KeepingProvider : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("stub");

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(text.Contains("[TKN0]") ? "I had an accident, my policy is [TKN0]" : "I had an accident");
    }

    private sealed class ThrowingProvider : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("stub");

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("engine down");
    }

    private sealed class CountingProvider : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("stub");
        public int CallCount { get; private set; }

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult($"[{to}] {text}");
        }
    }

    /// <summary>Always returns the input unchanged, the simplest documented failure mode, to
    /// prove the retry-then-fallback path calls the provider exactly twice and never more.</summary>
    private sealed class EchoingProvider : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("stub");
        public int CallCount { get; private set; }

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(text);
        }
    }
}

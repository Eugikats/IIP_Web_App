using InsuranceIntelligence.Api.Voice;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Api.Tests;

public sealed class StaffVoiceTranscriberFallbackTests
{
    private static readonly TenantScope Scope = new("tenant-1", "workspace-1");

    private sealed class Fake(bool available, string? text) : IStaffVoiceTranscriber
    {
        public int Calls { get; private set; }

        public Task<bool> IsStaffTranscriptionAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(available);

        public Task<string?> TranscribeAudioAsync(TenantScope scope, byte[] audio, string? contentType, SupportedLanguageCode? preferredLanguage, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(text);
        }
    }

    private static AzureSpeechStaffTranscriber Azure(string? endpoint) =>
        new(new VoiceLiveOptions { Endpoint = endpoint }, new global::Azure.Identity.DefaultAzureCredential(), new NoClientFactory());

    private sealed class NoClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No request is expected.");
    }

    [Fact]
    public async Task The_record_button_is_available_when_only_the_platform_speech_is_set_up()
    {
        var sut = new StaffVoiceTranscriberWithFallback(new Fake(available: false, text: null), Azure("https://ais-test.services.ai.azure.com"));

        Assert.True(await sut.IsStaffTranscriptionAvailableAsync(Scope));
    }

    [Fact]
    public async Task It_is_unavailable_only_when_neither_speech_service_is_set_up()
    {
        var sut = new StaffVoiceTranscriberWithFallback(new Fake(available: false, text: null), Azure(null));

        Assert.False(await sut.IsStaffTranscriptionAvailableAsync(Scope));
    }

    [Fact]
    public async Task The_workspaces_own_provider_is_used_first_and_the_platform_is_not_asked()
    {
        var primary = new Fake(available: true, text: "how many claims today");
        var sut = new StaffVoiceTranscriberWithFallback(primary, Azure(null));

        var text = await sut.TranscribeAudioAsync(Scope, [1, 2, 3], "audio/wav", null);

        Assert.Equal("how many claims today", text);
        Assert.Equal(1, primary.Calls);
    }
}

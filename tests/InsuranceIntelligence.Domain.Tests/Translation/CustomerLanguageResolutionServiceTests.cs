using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class CustomerLanguageResolutionServiceTests
{
    private static readonly TenantScope TenantA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope TenantB = TenantScope.Create("tenant-b", "workspace-b");
    private const string ContactWaId = "256772000000";

    private sealed class StubDetector(LanguageResolution result) : ILanguageDetector
    {
        public LanguageResolution Detect(string text) => result;
    }

    [Fact]
    public async Task The_choice_prompt_answer_is_saved_as_the_preference()
    {
        var repository = new InMemoryCustomerLanguagePreferenceRepository();
        var service = new CustomerLanguageResolutionService(
            repository, new StubDetector(new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, 0)));

        var resolution = await service.ResolveAsync(TenantA, ContactWaId, "2", previousMessageWasChoicePrompt: true);

        Assert.Equal(SupportedLanguageCode.Lg, resolution.Language);
        var saved = await repository.GetCurrentAsync(TenantA, ContactWaId);
        Assert.NotNull(saved);
        Assert.Equal(SupportedLanguageCode.Lg, saved!.Language);
    }

    [Fact]
    public async Task An_explicit_phrase_request_is_also_saved_as_the_preference()
    {
        var repository = new InMemoryCustomerLanguagePreferenceRepository();
        var service = new CustomerLanguageResolutionService(
            repository, new StubDetector(new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, 0)));

        await service.ResolveAsync(TenantA, ContactWaId, "please reply in Kiswahili", previousMessageWasChoicePrompt: false);

        var saved = await repository.GetCurrentAsync(TenantA, ContactWaId);
        Assert.NotNull(saved);
        Assert.Equal(SupportedLanguageCode.Sw, saved!.Language);
    }

    [Fact]
    public async Task A_resolution_that_only_reads_a_remembered_preference_does_not_write_a_new_row()
    {
        var repository = new InMemoryCustomerLanguagePreferenceRepository();
        await repository.SaveAsync(new CustomerLanguagePreference
        {
            PreferenceId = Guid.NewGuid(),
            Scope = TenantA,
            ContactWaId = ContactWaId,
            Language = SupportedLanguageCode.Sw,
            SetAt = DateTimeOffset.UtcNow,
        });
        var service = new CustomerLanguageResolutionService(
            repository, new StubDetector(new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, 0)));

        await service.ResolveAsync(TenantA, ContactWaId, "how is my claim going", previousMessageWasChoicePrompt: false);

        var current = await repository.GetCurrentAsync(TenantA, ContactWaId);
        Assert.Equal(SupportedLanguageCode.Sw, current!.Language);
    }

    [Fact]
    public async Task Preferences_are_isolated_per_tenant_for_the_same_contact_id()
    {
        var repository = new InMemoryCustomerLanguagePreferenceRepository();
        var service = new CustomerLanguageResolutionService(
            repository, new StubDetector(new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, 0)));

        await service.ResolveAsync(TenantA, ContactWaId, "reply in Luganda", previousMessageWasChoicePrompt: false);
        await service.ResolveAsync(TenantB, ContactWaId, "reply in Kiswahili", previousMessageWasChoicePrompt: false);

        var tenantAPreference = await repository.GetCurrentAsync(TenantA, ContactWaId);
        var tenantBPreference = await repository.GetCurrentAsync(TenantB, ContactWaId);

        Assert.Equal(SupportedLanguageCode.Lg, tenantAPreference!.Language);
        Assert.Equal(SupportedLanguageCode.Sw, tenantBPreference!.Language);
    }
}

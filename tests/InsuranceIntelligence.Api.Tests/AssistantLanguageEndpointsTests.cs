using System.Text.Json;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AssistantLanguageEndpointsTests
{
    private static readonly TenantScope ActiveScope = TenantScope.Create("tenant-demo", "workspace-claims");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Get_ReturnsTenantSelectionsAndTruthfulVoiceCapabilities()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        await repository.SaveAsync(TenantConfiguration.Demo() with { Scope = ActiveScope, AssistantLanguages = ["lg"] });
        var settings = new StubLanguageSettings([SupportedLanguageCode.Lg]);
        var result = await AssistantLanguageEndpoints.Get(
            repository, settings, new StubTranslationHealth(), Options(), Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<AssistantLanguageSettingsResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Workspace", body!.ConfigurationSource);
        Assert.Contains(body.Languages, language => language.Code == "lg" && language.Enabled && language.VoiceInput == "Available");
        Assert.Contains(body.Languages, language => language.Code == "ach" && !language.Enabled && language.VoiceInput == "Not configured");
        Assert.Equal(TranslationEngineStatus.Healthy.ToString(), body.TranslationProviderStatus);
    }

    [Fact]
    public async Task Update_SavesWorkspaceSelectionAndAuditsActorAndReason()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var audit = new InMemoryAuditRepository();
        var result = await AssistantLanguageEndpoints.Update(
            new UpdateAssistantLanguageSettingsRequest(["ach", "sw"]), repository, audit, Options(),
            Context("Enable verified customer language options."), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Workspace", body.GetProperty("configurationSource").GetString());
        Assert.Equal(new[] { "ach", "sw" }, body.GetProperty("enabledCodes").EnumerateArray().Select(code => code.GetString()).ToArray());
        Assert.Equal(new[] { "ach", "sw" }, (await repository.GetAsync(ActiveScope)).AssistantLanguages);
        var entry = Assert.Single(await audit.ListAsync(ActiveScope));
        Assert.Equal("admin.assistant_languages.updated", entry.ActionName);
        Assert.Equal("admin-1", entry.UserId);
        Assert.Contains("Enable verified customer language options.", entry.AuditReason);
    }

    [Fact]
    public async Task Update_RejectsUnknownOrEnglishCodesWithoutMutation()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var audit = new InMemoryAuditRepository();
        var result = await AssistantLanguageEndpoints.Update(
            new UpdateAssistantLanguageSettingsRequest(["en", "not-a-language"]), repository, audit, Options(),
            Context("Attempt invalid selection."), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Null((await repository.GetAsync(ActiveScope)).AssistantLanguages);
        Assert.Empty(await audit.ListAsync(ActiveScope));
    }

    private static InfrastructureOptions Options() => new() { AuthMode = "Development" };

    private static DefaultHttpContext Context(string? reason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "assistant-language-test" };
        context.Request.Headers["X-Tenant-Id"] = ActiveScope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = ActiveScope.WorkspaceId;
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        if (reason is not null) context.Request.Headers["X-Audit-Reason"] = reason;

        var tenant = new TenantContextAccessor();
        tenant.SetCurrent(new TenantContext(ActiveScope.TenantId, ActiveScope.WorkspaceId, "admin-1", ["ITAdmin"]));
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenant)
            .BuildServiceProvider();
        return context;
    }

    private static async Task<(int StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(context.Response.Body, JsonOptions);
        return (context.Response.StatusCode, body);
    }

    private sealed class StubLanguageSettings(IReadOnlyCollection<SupportedLanguageCode> enabled) : IAssistantLanguageSettings
    {
        public Task<IReadOnlyCollection<SupportedLanguageCode>> GetEnabledLanguagesAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(enabled);
        public bool AllowUnreviewedTranslations => false;
    }

    private sealed class StubTranslationHealth : ITranslationEngineHealth
    {
        public Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(TranslationEngineStatus.Healthy);
    }
}

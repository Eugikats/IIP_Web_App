using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Agents;

public sealed record AssistantLanguageOption(
    string Code,
    string Name,
    string TranslationQuality,
    bool Enabled,
    string VoiceInput,
    string VoiceOutput);

public sealed record AssistantLanguageSettingsResponse(
    IReadOnlyCollection<AssistantLanguageOption> Languages,
    IReadOnlyCollection<string> EnabledCodes,
    string TranslationProviderStatus,
    bool AllowUnreviewedTranslations,
    string ConfigurationSource);

public sealed record UpdateAssistantLanguageSettingsRequest(IReadOnlyCollection<string>? EnabledCodes);

/// <summary>Workspace language controls. Text translation coverage follows the current Sunflower
/// model catalog; voice controls only claim capabilities that IIP actually wires today.</summary>
public static class AssistantLanguageEndpoints
{
    public static async Task<IResult> Get(
        ITenantConfigurationRepository repository,
        IAssistantLanguageSettings settings,
        ITranslationEngineHealth translationHealth,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageAgents) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var configuration = await repository.GetAsync(scope, cancellationToken);
        var enabled = await settings.GetEnabledLanguagesAsync(scope, cancellationToken);
        var enabledCodes = enabled.Select(code => SupportedLanguage.For(code).Iso).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languages = SupportedLanguage.All
            .Where(language => language.Code != SupportedLanguageCode.En)
            .Select(language => new AssistantLanguageOption(
                language.Iso,
                language.DisplayName,
                language.TranslationTier.ToString(),
                enabledCodes.Contains(language.Iso),
                language.VoiceInputAvailable ? "Available" : "Not configured",
                language.VoiceOutputAvailable ? "Available" : "Not configured"))
            .ToArray();

        return Results.Ok(new AssistantLanguageSettingsResponse(
            languages,
            enabled.Select(code => SupportedLanguage.For(code).Iso).Order(StringComparer.Ordinal).ToArray(),
            (await translationHealth.CheckAsync(scope, cancellationToken)).ToString(),
            settings.AllowUnreviewedTranslations,
            configuration.AssistantLanguages is null ? "Deployment default" : "Workspace"));
    }

    public static async Task<IResult> Update(
        UpdateAssistantLanguageSettingsRequest request,
        ITenantConfigurationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageAgents) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var values = request.EnabledCodes ?? [];
        if (values.Count > SupportedLanguage.All.Count - 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["enabledCodes"] = ["Choose only languages in the supported catalog."] });
        }

        var languages = values
            .Select(SupportedLanguage.FromIso)
            .ToArray();
        if (languages.Any(language => language is null || language.Code == SupportedLanguageCode.En))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["enabledCodes"] = ["Choose supported non-English language codes from the catalog."] });
        }

        var selected = languages!
            .Select(language => language!.Iso)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var current = await repository.GetAsync(scope, cancellationToken);
        var oldEffective = current.AssistantLanguages?.Order(StringComparer.Ordinal).ToArray();
        if (oldEffective is not null && oldEffective.SequenceEqual(selected, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "The selected language configuration is already saved." });
        }

        var actor = DemoAuthorization.GetUserId(httpContext);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required."] });
        }

        var reason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = ["An audit reason is required to update assistant languages."] });
        }

        var updated = current with { AssistantLanguages = selected, UpdatedAt = DateTimeOffset.UtcNow };
        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), scope, null, null, actor, ActorType.User, "admin.assistant_languages.updated",
            DateTimeOffset.UtcNow, oldEffective is null ? "DeploymentDefault" : "Workspace", "Workspace",
            selected, "AssistantLanguages", null, "accepted", null,
            $"Assistant language enablement updated for {selected.Length} language(s). {reason}",
            Guid.NewGuid().ToString("N")), cancellationToken);

        return Results.Ok(new { enabledCodes = selected, configurationSource = "Workspace" });
    }

    private static bool TryResolveScope(HttpContext httpContext, out TenantScope scope, out string error)
    {
        if (httpContext.RequestServices.GetService(typeof(InsuranceIntelligence.Application.Core.ITenantContextAccessor))
            is InsuranceIntelligence.Application.Core.ITenantContextAccessor { HasCurrent: true } tenantContext)
        {
            scope = tenantContext.Current.Scope;
            error = string.Empty;
            return true;
        }

        scope = default!;
        error = "A resolved workspace context is required.";
        return false;
    }
}

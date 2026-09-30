using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Agents;

public sealed record TranslationStatusResponse(
    string Engine,
    string Status,
    IReadOnlyList<TranslationLanguageResponse> Languages,
    bool AllowUnreviewedTranslations,
    string Summary);

public sealed record TranslationLanguageResponse(string Code, string Name);

public sealed record TranslationTestRequest(string? Text, string? From, string? To);

public sealed record TranslationTestResponse(bool Translated, string Text, string? Failure, bool FromCache);

/// <summary>
/// Whether the customer assistant can really answer in Luganda and Kiswahili right now, in words a
/// person running the workspace can act on. It reports what the API can reach, not what was
/// configured, so a container that is down shows as failing rather than as set up. It never returns
/// the engine's address or key.
/// </summary>
public static class TranslationStatusEndpoints
{
    public static async Task<IResult> Get(
        ITranslationEngineHealth health,
        IIntegrationConnectionRepository connectionRepository,
        IAssistantLanguageSettings settings,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageAgents) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var status = await health.CheckAsync(scope, cancellationToken);
        var languages = settings.EnabledLanguages
            .Select(SupportedLanguage.For)
            .Select(language => new TranslationLanguageResponse(language.Iso, language.DisplayName))
            .ToArray();

        return Results.Ok(new TranslationStatusResponse(
            Engine: await ResolveEngineNameAsync(scope, connectionRepository, options, cancellationToken),
            Status: status.ToString(),
            Languages: languages,
            AllowUnreviewedTranslations: settings.AllowUnreviewedTranslations,
            Summary: Summarise(status, languages.Length, settings.AllowUnreviewedTranslations)));
    }

    /// <summary>Shows what a customer would get for a piece of text, so a person can check the
    /// engine before customers depend on it, and a native speaker can review real output. It runs the
    /// same service the assistant uses, with the same checks and the same fallback, and stores nothing.</summary>
    public static async Task<IResult> Test(
        TranslationTestRequest request,
        ITranslationService translation,
        IAssistantLanguageSettings settings,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageAgents) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var text = request.Text?.Trim();
        var from = SupportedLanguage.FromIso(request.From);
        var to = SupportedLanguage.FromIso(request.To);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTestTextLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [$"Enter between 1 and {MaxTestTextLength} characters."] });
        }

        if (from is null || to is null || from.Code == to.Code)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["language"] = ["Choose two different languages from English, Luganda and Kiswahili."] });
        }

        var enabled = settings.EnabledLanguages;
        if ((from.Code != SupportedLanguageCode.En && !enabled.Contains(from.Code)) || (to.Code != SupportedLanguageCode.En && !enabled.Contains(to.Code)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["language"] = ["That language is not switched on for this workspace."] });
        }

        var protectedTerms = AssistantGlossary.KeptInEnglish(to.Code);
        var outcome = await translation.TranslateAsync(text, from.Code, to.Code, scope, cancellationToken, protectedTerms);
        return Results.Ok(new TranslationTestResponse(outcome.Translated, outcome.Text, outcome.Failure?.ToString(), outcome.FromCache));
    }

    /// <summary>The connections screen's "try a translation" box: runs the same judged, fallback-
    /// guarded service customers get, through whichever engine this tenant has active, and stores
    /// nothing. Unlike <see cref="Test"/> it is not limited to languages already switched on for the
    /// workspace, because checking the engine is exactly what someone does before switching one on.</summary>
    public static async Task<IResult> TryConnection(
        TranslationTestRequest request,
        ITranslationService translation,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var text = request.Text?.Trim();
        var from = SupportedLanguage.FromIso(request.From);
        var to = SupportedLanguage.FromIso(request.To);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTestTextLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [$"Enter between 1 and {MaxTestTextLength} characters."] });
        }

        if (from is null || to is null || from.Code == to.Code)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["language"] = ["Choose two different languages from English, Luganda and Kiswahili."] });
        }

        var outcome = await translation.TranslateAsync(text, from.Code, to.Code, scope, cancellationToken, AssistantGlossary.KeptInEnglish(to.Code));
        return Results.Ok(new TranslationTestResponse(outcome.Translated, outcome.Text, outcome.Failure?.ToString(), outcome.FromCache));
    }

    private const int MaxTestTextLength = 600;

    /// <summary>Reports the tenant's actual active engine when one is connected through the
    /// dashboard (e.g. Sunbird AI), falling back to the deployment's environment-configured engine
    /// otherwise. Never silently keeps naming the deployment default once a tenant has switched.</summary>
    private static async Task<string> ResolveEngineNameAsync(
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        IIntegrationConnectionRepository connectionRepository,
        InfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        var connection = await connectionRepository.GetAsync(scope, ConnectionType.Translation, cancellationToken);
        return connection is { IsActive: true }
            ? EngineName(connection.ProviderKey)
            : EngineName(options.TranslationProvider);
    }

    public static string EngineName(string provider) => provider.ToLowerInvariant() switch
    {
        "afrislm" => "AfriSLM translation model",
        "sunbird" => "Sunbird AI translation model",
        "mock" => "Test translator (not real)",
        _ => "None",
    };

    public static string Summarise(TranslationEngineStatus status, int enabledLanguages, bool allowUnreviewed)
    {
        if (enabledLanguages == 0)
        {
            return "Only English is switched on. Customers who write in another language are handed to a person.";
        }

        return status switch
        {
            TranslationEngineStatus.NotConfigured =>
                "No translation engine is connected. Customers who write in another language are handed to a person, and replies go out in English.",
            TranslationEngineStatus.Failing =>
                "The translation engine cannot be reached. Customers who write in another language are handed to a person, and replies go out in English until it is back.",
            _ when !allowUnreviewed =>
                "The engine is working and customers are understood in their own language. Replies go out in English until translated replies are switched on, because no translation has been reviewed by a native speaker yet.",
            _ =>
                "The engine is working. Customers are understood in their own language and answered in it. Replies are machine translated and say so, and no native speaker has reviewed them yet.",
        };
    }
}

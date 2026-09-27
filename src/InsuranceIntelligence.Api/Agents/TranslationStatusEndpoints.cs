using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Administration;
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
        IAssistantLanguageSettings settings,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageAgents) is { } forbidden)
        {
            return forbidden;
        }

        var status = await health.CheckAsync(cancellationToken);
        var languages = settings.EnabledLanguages
            .Select(SupportedLanguage.For)
            .Select(language => new TranslationLanguageResponse(language.Iso, language.DisplayName))
            .ToArray();

        return Results.Ok(new TranslationStatusResponse(
            Engine: EngineName(options.TranslationProvider),
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
        var outcome = await translation.TranslateAsync(text, from.Code, to.Code, cancellationToken, protectedTerms);
        return Results.Ok(new TranslationTestResponse(outcome.Translated, outcome.Text, outcome.Failure?.ToString(), outcome.FromCache));
    }

    private const int MaxTestTextLength = 600;

    public static string EngineName(string provider) => provider.ToLowerInvariant() switch
    {
        "afrislm" => "AfriSLM translation model",
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

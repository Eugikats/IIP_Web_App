using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.CustomerAssistant;
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

using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// The Integration Center's data: one card per integration that exists, each with a state earned by a real check. It
/// reads only what is already stored (saved connections, webhook receipts, a person's own file connections). It
/// calls no provider, so opening the page costs nothing and cannot send anything. Secrets are never read into it.
/// </summary>
public static class IntegrationCatalogueEndpoints
{
    public static async Task<IResult> Get(
        IIntegrationConnectionRepository connections,
        IWhatsAppWebhookReceiptRepository receipts,
        IWhatsAppRuntimeSettingsProvider whatsAppSettings,
        IUserFileService files,
        ITenantContextSetter setter,
        TimeProvider clock,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, setter, options, out var error))
        {
            return error;
        }

        if (!Workflow.WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var now = clock.GetUtcNow();
        var recent = await receipts.ListAsync(scope, 100, cancellationToken);
        var lastAccepted = recent.Where(r => r.Outcome is "accepted" or "duplicate_only").Select(r => (DateTimeOffset?)r.ReceivedAt).DefaultIfEmpty(null).Max();
        var refusedInLastDay = recent.Count(r => r.Outcome == "refused_signature" && now - r.ReceivedAt < TimeSpan.FromDays(1));

        var email = await ConnectionFactsAsync(connections, scope, ConnectionType.Email, ProviderLive(options.EmailProvider), cancellationToken);
        var sms = await ConnectionFactsAsync(connections, scope, ConnectionType.Sms, ProviderLive(options.SmsProvider), cancellationToken);

        IReadOnlyList<FileConnectionStatus> fileStatus;
        try
        {
            fileStatus = await files.StatusAsync(cancellationToken);
        }
        catch (FileConnectionException)
        {
            fileStatus = [];
        }

        FileProviderFacts File(string provider) =>
            fileStatus.FirstOrDefault(s => string.Equals(s.Provider, provider, StringComparison.OrdinalIgnoreCase)) is { } s
                ? new FileProviderFacts(s.Configured, s.Connected, s.AccountEmail, s.Reason)
                : new FileProviderFacts(false, false, null, null);

        var whatsApp = new WhatsAppFacts(
            whatsAppSettings.AnyConfigured,
            (whatsAppSettings.Current ?? whatsAppSettings.All.FirstOrDefault())?.Source,
            recent.Select(r => (DateTimeOffset?)r.ReceivedAt).DefaultIfEmpty(null).Max(),
            lastAccepted,
            refusedInLastDay,
            !string.IsNullOrWhiteSpace(options.ApiPublicBaseUrl));

        var cards = IntegrationCatalogueBuilder.Build(new IntegrationFacts(
            now, whatsApp, email, sms, ProviderLive(options.NotificationProvider), File("google"), File("microsoft")));

        httpContext.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new
        {
            checkedAt = now,
            cards = cards.Select(card => new
            {
                key = card.Key,
                name = card.Name,
                group = card.Group,
                state = card.State.ToString(),
                stateLabel = IntegrationCardRules.Label(card.State),
                summary = card.Summary,
                setupHint = card.SetupHint,
                checks = card.Checks.Select(check => new { name = check.Name, passed = check.Passed, note = check.Note, blocking = check.Blocking }),
            }),
        });
    }

    // "Disabled" means nothing is wired; "Mock" is the built-in test sender, which sends nothing real. Neither is a service
    // a customer can rely on, so neither lets a card claim to be connected.
    private static bool ProviderLive(string? provider) =>
        !string.IsNullOrWhiteSpace(provider)
        && !provider.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
        && !provider.Equals("Mock", StringComparison.OrdinalIgnoreCase);

    private static async Task<ConnectionFacts> ConnectionFactsAsync(
        IIntegrationConnectionRepository connections, InsuranceIntelligence.Domain.Common.TenantScope scope, ConnectionType type, bool environmentProviderLive, CancellationToken cancellationToken)
    {
        var saved = await connections.GetAsync(scope, type, cancellationToken);
        return new ConnectionFacts(environmentProviderLive, saved is not null, saved?.IsActive ?? false, saved?.LastValidationStatus, saved?.LastValidatedAt, saved?.ProviderKey);
    }
}

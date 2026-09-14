using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Graph;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// Builds a live IEmailSendingProvider from a provider key and settings, the email equivalent of
/// SmsProviderFactory. Only "Smtp" is buildable from tenant connection settings; the Graph fallback
/// requires deployment-level Entra app registration and is only ever reached via CreateFallback.
/// </summary>
public sealed class EmailSendingProviderFactory(
    InfrastructureOptions options,
    ILoggerFactory loggerFactory,
    IGraphMailClient graphMailClient) : IEmailSendingProviderFactory
{
    public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["Smtp"];

    public IEmailSendingProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings)
    {
        if (!string.Equals(providerKey, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return SmtpSettings.TryParse(settings, out var smtpSettings, out _) && smtpSettings is not null
            ? new SmtpEmailProvider(smtpSettings, loggerFactory.CreateLogger<SmtpEmailProvider>())
            : null;
    }

    public IEmailSendingProvider CreateFallback() =>
        options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase)
            ? new GraphEmailSendingProvider(options, graphMailClient)
            : new DisabledEmailSendingProvider();
}

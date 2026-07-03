using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Health;

public static class HealthEndpoints
{
    public static IResult Ready(InfrastructureOptions options, IHostEnvironment environment)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        var errors = AzureProviderStartupValidator.Validate(options)
            .Concat(AzureProviderStartupValidator.ValidateProduction(options, runtimeProfile))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var body = new
        {
            status = errors.Length == 0 ? "ready" : "not_ready",
            environment = runtimeProfile,
            hostEnvironment = environment.EnvironmentName,
            storageProvider = options.StorageProvider,
            blobProvider = options.BlobProvider,
            aiProvider = options.AiProvider,
            documentProvider = options.DocumentProvider,
            retrievalProvider = options.RetrievalProvider,
            queueProvider = options.QueueProvider,
            emailProvider = options.EmailProvider,
            communicationProvider = options.CommunicationProvider,
            notificationProvider = options.NotificationProvider,
            seededBusinessData = options.SeedDemoData,
            errors
        };

        return errors.Length == 0
            ? Results.Ok(body)
            : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

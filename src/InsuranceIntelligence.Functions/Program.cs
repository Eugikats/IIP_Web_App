using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();
                services.Configure<LoggerFilterOptions>(filterOptions =>
                {
                    var defaultRule = filterOptions.Rules.FirstOrDefault(rule =>
                        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                    if (defaultRule is not null)
                    {
                        filterOptions.Rules.Remove(defaultRule);
                    }
                });

                var options = InfrastructureRegistration.BindOptions(context.Configuration);
                var runtimeProfile = options.RuntimeProfile ?? context.HostingEnvironment.EnvironmentName;
                var runtimeErrors = WorkflowShell.ValidateProtectedRuntime(options, runtimeProfile);
                if (runtimeErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Functions runtime configuration is invalid: {string.Join(" ", runtimeErrors)}");
                }

                services.AddInsuranceIntelligenceInfrastructure(context.Configuration, context.HostingEnvironment);

                // Add Teams webhook client if configured
                if (options.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
                {
                    services.Configure<InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProviderOptions>(
                        context.Configuration.GetSection("TeamsWebhook"));
                    services.AddHttpClient<InsuranceIntelligence.Domain.Notifications.ITeamsNotificationProvider, 
                        InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProvider>();
                }
                else
                {
                    services.AddSingleton<InsuranceIntelligence.Domain.Notifications.ITeamsNotificationProvider,
                        InsuranceIntelligence.Infrastructure.Graph.DisabledTeamsNotificationProvider>();
                }
            })
            .Build();

        host.Run();
    }
}

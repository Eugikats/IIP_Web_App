using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.Queue;
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
                var runtimeProfile = ProviderModeSupport.ResolveRuntimeProfile(
                    options,
                    context.HostingEnvironment.EnvironmentName);
                var runtimeErrors = WorkflowShell.ValidateProtectedRuntime(options, runtimeProfile);
                if (runtimeErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Functions runtime configuration is invalid: {string.Join(" ", runtimeErrors)}");
                }

                services.AddInsuranceIntelligenceInfrastructure(context.Configuration, context.HostingEnvironment);
                services.AddScoped<TenantContextAccessor>();
                services.AddScoped<ITenantContextAccessor>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
                services.AddScoped<ITenantContextSetter>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
                services.AddScoped<IPermissionService, PermissionService>();
                services.AddScoped<IAuditWriter, AuditWriter>();
                services.AddScoped<IAgentWorkflowService, AgentWorkflowService>();
                services.AddScoped<IAgentWorkflowNodeExecutor, AgentWorkflowNodeExecutor>();
                services.AddScoped<IAgentWorkflowExecutionWorker, AgentWorkflowExecutionWorker>();
                if (options.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
                {
                    services.AddSingleton<IMessageQueueDispatcher, FunctionsAzureServiceBusQueueDispatcher>();
                }
                else if (options.QueueProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
                {
                    services.AddSingleton<IMessageQueueDispatcher, FunctionsMockMessageQueueDispatcher>();
                }
                else
                {
                    services.AddSingleton<IMessageQueueDispatcher, FunctionsNotConfiguredMessageQueueDispatcher>();
                }

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

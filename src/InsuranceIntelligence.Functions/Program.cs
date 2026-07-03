using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Functions;

public class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices((context, services) =>
            {
                // In a full implementation, we'd wire up the real providers here
                // just like in Api's Program.cs. For now we will add the basics
                // so the TeamsNotificationDispatchFunction can be instantiated.
                
                var options = InfrastructureRegistration.BindOptions(context.Configuration);
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
                    // Fallback to mock
                    services.AddSingleton<InsuranceIntelligence.Domain.Notifications.ITeamsNotificationProvider, 
                        InsuranceIntelligence.Infrastructure.Notifications.MockTeamsNotificationProvider>();
                }
            })
            .Build();

        host.Run();
    }
}

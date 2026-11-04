using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Admin.Billing;

public static class MeteringRegistration
{
    /// <summary>
    /// Prices come from <c>INSURANCEINTELLIGENCE_AI_PRICING</c> (JSON, per model deployment) and the three model
    /// interfaces are wrapped so every call is counted. Call after the AI providers are registered.
    /// </summary>
    public static IServiceCollection AddAiUsageMetering(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(AiPricingOptions.Parse(configuration["INSURANCEINTELLIGENCE_AI_PRICING"]));
        services.AddSingleton<IAiCostModel, ConfiguredAiCostModel>();

        Wrap<IChatCompletionProvider>(services, (inner, sp) => new MeteredChatCompletionProvider(inner, sp.GetRequiredService<IWorkspaceAiUsageRecorder>(), sp.GetRequiredService<InsuranceIntelligence.Application.Core.ITenantContextAccessor>()));
        Wrap<IToolCallingChatProvider>(services, (inner, sp) => new MeteredToolCallingChatProvider(inner, sp.GetRequiredService<IWorkspaceAiUsageRecorder>(), sp.GetRequiredService<InsuranceIntelligence.Application.Core.ITenantContextAccessor>()));
        Wrap<IEmbeddingProvider>(services, (inner, sp) => new MeteredEmbeddingProvider(inner, sp.GetRequiredService<IWorkspaceAiUsageRecorder>(), sp.GetRequiredService<InsuranceIntelligence.Application.Core.ITenantContextAccessor>()));
        return services;
    }

    // The metering wrapper is scoped because the recorder and the request's tenant are. A provider that was a
    // singleton (it holds an HTTP client worth reusing) stays one: it is created once and shared by every wrapper.
    private static void Wrap<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> wrap)
        where TService : class
    {
        var original = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TService));
        if (original is null)
        {
            return;
        }

        services.Remove(original);
        if (original.Lifetime == ServiceLifetime.Singleton)
        {
            services.AddSingleton(sp => new Shared<TService>((TService)Create(sp, original)));
            services.AddScoped(sp => wrap(sp.GetRequiredService<Shared<TService>>().Instance, sp));
        }
        else
        {
            services.AddScoped(sp => wrap((TService)Create(sp, original), sp));
        }
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);

    private sealed record Shared<TService>(TService Instance) where TService : class;
}

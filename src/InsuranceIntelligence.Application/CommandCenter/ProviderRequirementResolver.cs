namespace InsuranceIntelligence.Application.CommandCenter;

public sealed record ProviderRequirement(string ProviderCode, string DisplayName);

public interface IProviderRequirementResolver
{
    IReadOnlyList<ProviderRequirement> GetRequiredProviders(IntentType intent);
}

public sealed class ProviderRequirementResolver : IProviderRequirementResolver
{
    public IReadOnlyList<ProviderRequirement> GetRequiredProviders(IntentType intent) => intent switch
    {
        IntentType.GenerateManagementSummary =>
        [
            new ProviderRequirement("azure_openai", "Azure OpenAI"),
            new ProviderRequirement("azure_ai_search", "Azure AI Search")
        ],

        IntentType.PrepareCustomerUpdateDraft =>
        [
            new ProviderRequirement("azure_openai", "Azure OpenAI"),
            new ProviderRequirement("azure_ai_search", "Azure AI Search")
        ],

        _ => Array.Empty<ProviderRequirement>()
    };
}

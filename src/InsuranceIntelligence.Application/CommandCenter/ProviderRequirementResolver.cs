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

        // Grounded policy answers need both the model and the retrieval index — without
        // retrieval this would be an ungrounded policy answer, which the product forbids.
        IntentType.AnswerPolicyQuestion =>
        [
            new ProviderRequirement("azure_openai", "Azure OpenAI"),
            new ProviderRequirement("azure_ai_search", "Azure AI Search")
        ],

        _ => Array.Empty<ProviderRequirement>()
    };
}

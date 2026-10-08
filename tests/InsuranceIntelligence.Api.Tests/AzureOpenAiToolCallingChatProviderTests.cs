using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Application.Ai;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The WhatsApp agent's request body. Reasoning effort is the main lever on how fast Kato answers,
/// and models without reasoning reject the parameter, so it is sent only when configured.
/// </summary>
public sealed class AzureOpenAiToolCallingChatProviderTests
{
    private static readonly IReadOnlyList<AgentChatMessage> Messages = [AgentChatMessage.System("You are Kato."), AgentChatMessage.User("Hello")];

    [Fact]
    public void Body_omits_reasoning_effort_when_not_configured()
    {
        var body = AzureOpenAiToolCallingChatProvider.BuildRequestBody(Messages, []);

        Assert.False(body.ContainsKey("reasoning_effort"));
    }

    [Fact]
    public void Body_carries_configured_reasoning_effort_in_lower_case()
    {
        var body = AzureOpenAiToolCallingChatProvider.BuildRequestBody(Messages, [], " Low ");

        Assert.Equal("low", body["reasoning_effort"]!.GetValue<string>());
    }
}

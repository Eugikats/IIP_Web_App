using System.Net;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The fraud agent reads a claim against the Uganda playbook and writes findings that cite the
/// claim's facts. Nothing it writes is kept unless it holds up, and none of it decides anything.</summary>
public sealed class FraudAgentApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private sealed class ScriptedChat(string? content) : IChatCompletionProvider
    {
        public string? LastPrompt { get; private set; }

        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            if (content is null)
            {
                throw new InvalidOperationException("The model is unavailable.");
            }

            return Task.FromResult(new ChatCompletionResult(content, "Scripted", "scripted-chat", DateTimeOffset.UtcNow, "scripted-v1", 1, 1));
        }
    }

    private static string Reply(string facts, string observation = "The incident was reported soon after the cover began, which may be a coincidence.") =>
        "{\"findings\":[{\"indicator\":\"MOT-EARLY-LOSS\",\"title\":\"Loss close to the start of cover\",\"observation\":\"" + observation +
        "\",\"check\":\"Compare the premium receipt time with the incident time.\",\"severity\":\"potential\",\"facts\":" + facts + "}]}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private async Task<(WebApplicationFactoryHost Host, HttpClient Staff, CaseInfo Claim, ScriptedChat Chat)> ArrangeAsync(string? reply)
    {
        var chat = new ScriptedChat(reply);
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IChatCompletionProvider>(chat))));
        var tenant = MockedApiFactory.NewTenant();
        var staff = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        return (new WebApplicationFactoryHost(host), staff, claim, chat);
    }

    /// <summary>Owns the derived factory so a test can dispose it with a using.</summary>
    private sealed class WebApplicationFactoryHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host) : IAsyncDisposable
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Inner { get; } = host;

        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }

    [Fact]
    public async Task BeforeItHasRunTheReviewSaysSoAndTheAgentIsOn()
    {
        var (host, staff, claim, _) = await ArrangeAsync("{\"findings\":[]}");
        await using var _h = host;
        using var _s = staff;

        var review = await ReadAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/fraud-review"), HttpStatusCode.OK);

        Assert.False(review.GetProperty("ran").GetBoolean());
        Assert.True(review.GetProperty("agentEnabled").GetBoolean());
        Assert.Empty(review.GetProperty("findings").EnumerateArray());
        Assert.Contains("not findings that anyone has done wrong", review.GetProperty("notice").GetString());
    }

    [Fact]
    public async Task AFindingThatCitesRealFactsIsKeptShownWithItsFactsAndAddedToTheReviewScore()
    {
        var (host, staff, claim, chat) = await ArrangeAsync(Reply("[\"F1\",\"F2\"]"));
        await using var _h = host;
        using var _s = staff;

        var review = await ReadAsync(await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null), HttpStatusCode.OK);

        Assert.True(review.GetProperty("ran").GetBoolean());
        Assert.False(review.GetProperty("stale").GetBoolean());
        var finding = Assert.Single(review.GetProperty("findings").EnumerateArray());
        Assert.Equal("MOT-EARLY-LOSS", finding.GetProperty("indicatorCode").GetString());
        Assert.Equal("Motor", finding.GetProperty("line").GetString());
        Assert.Equal(2, finding.GetProperty("facts").GetArrayLength());
        Assert.Contains("Incident date", finding.GetProperty("facts")[1].GetProperty("text").GetString());
        Assert.NotNull(chat.LastPrompt);

        // The same review is returned later without calling the model again.
        var later = await ReadAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/fraud-review"), HttpStatusCode.OK);
        Assert.Single(later.GetProperty("findings").EnumerateArray());

        // And it counts in the review score, labelled as the fraud agent's.
        var assessment = await ReadAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment"), HttpStatusCode.OK);
        var factor = Assert.Single(assessment.GetProperty("score").GetProperty("factors").EnumerateArray());
        Assert.Equal("Fraud agent", factor.GetProperty("sourceLabel").GetString());
        Assert.True(assessment.GetProperty("score").GetProperty("value").GetInt32() > 0);

        Assert.Contains("claim.fraud_agent_review.prepared", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"));
    }

    [Fact]
    public async Task ThePromptCarriesThePlaybookAndFactsButNotTheCustomersNameOrContactDetails()
    {
        var (host, staff, claim, chat) = await ArrangeAsync("{\"findings\":[]}");
        await using var _h = host;
        using var _s = staff;

        await ReadAsync(await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null), HttpStatusCode.OK);

        var prompt = chat.LastPrompt!;
        Assert.StartsWith("TASK: fraud-agent-review", prompt);
        Assert.Contains("MOT-EARLY-LOSS", prompt);
        Assert.Contains("MED-INFLATED", prompt);
        Assert.Contains("boda boda", prompt);
        Assert.Contains("F1: Claim CLM-", prompt);
        Assert.DoesNotContain("Test Customer", prompt);
        Assert.DoesNotContain("@example.com", prompt);
        Assert.DoesNotContain((char)0x2014, prompt);
    }

    [Fact]
    public async Task AFindingThatCitesNothingRealOrAccusesSomeoneIsLeftOutAndCounted()
    {
        var (host, staff, claim, _) = await ArrangeAsync(Reply("[\"F99\"]"));
        await using var _h = host;
        using var _s = staff;

        var review = await ReadAsync(await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null), HttpStatusCode.OK);

        Assert.Empty(review.GetProperty("findings").EnumerateArray());
        Assert.Equal(1, review.GetProperty("leftOut").GetInt32());

        var (host2, staff2, claim2, _) = await ArrangeAsync(Reply("[\"F1\"]", "The customer committed fraud."));
        await using var _h2 = host2;
        using var _s2 = staff2;
        var accusing = await ReadAsync(await staff2.PostAsync($"/api/v1/cases/{claim2.CaseId}/fraud-review/run", null), HttpStatusCode.OK);
        Assert.Empty(accusing.GetProperty("findings").EnumerateArray());
        Assert.Equal(1, accusing.GetProperty("leftOut").GetInt32());
    }

    [Theory]
    [InlineData("I could not find anything unusual.")]
    [InlineData(null)]
    public async Task AnUnreadableReplyOrAFailedModelKeepsNothingAndLeavesTheRulesAlone(string? reply)
    {
        var (host, staff, claim, _) = await ArrangeAsync(reply);
        await using var _h = host;
        using var _s = staff;

        var response = await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var review = await ReadAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/fraud-review"), HttpStatusCode.OK);
        Assert.False(review.GetProperty("ran").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/fraud/signals")).StatusCode);
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    [InlineData("Client")]
    public async Task OnlyRolesThatSeeFraudSignalsCanReadOrRunTheReview(string role)
    {
        var (host, staff, claim, _) = await ArrangeAsync("{\"findings\":[]}");
        await using var _h = host;
        using var _s = staff;
        var tenant = claim.TenantId;
        using var other = host.Inner.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/cases/{claim.CaseId}/fraud-review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null)).StatusCode);
    }

    [Fact]
    public async Task AnotherTenantCannotReadOrRunTheReviewAndAnUnknownClaimIsNotFound()
    {
        var (host, staff, claim, _) = await ArrangeAsync("{\"findings\":[]}");
        await using var _h = host;
        using var _s = staff;
        using var intruder = host.Inner.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/cases/{claim.CaseId}/fraud-review")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/v1/cases/{claim.CaseId}/fraud-review/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/cases/{Guid.NewGuid()}/fraud-review")).StatusCode);
    }
}

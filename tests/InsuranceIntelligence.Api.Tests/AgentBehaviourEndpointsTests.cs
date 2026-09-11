using System.Text.Json;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AgentBehaviourEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task SaveVersion_ForUnknownAgentKey_ReturnsNotFound()
    {
        var result = await AgentBehaviourEndpoints.SaveVersion(
            "not-a-real-agent", MinimalRequest(), new InMemoryAgentBehaviourProfileRepository(), Options(), Context(auditReason: "Testing."), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task SaveVersion_WithoutAnAuditReason_IsRejected()
    {
        var result = await AgentBehaviourEndpoints.SaveVersion(
            "claims-summary", MinimalRequest(), new InMemoryAgentBehaviourProfileRepository(), Options(), Context(), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task SaveVersion_ForPolicyIntelligenceWithCitationDisabled_IsRejected()
    {
        // The one hard rule this endpoint enforces itself, not just documents: an agent whose
        // entire purpose is answering coverage questions from approved sources cannot have
        // citations turned off by a tenant.
        var request = MinimalRequest() with { RequireCitation = false };

        var result = await AgentBehaviourEndpoints.SaveVersion(
            "policy-intelligence", request, new InMemoryAgentBehaviourProfileRepository(), Options(), Context(auditReason: "Trying to disable citations."), CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("requireCitation", body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task SaveVersion_ForPolicyIntelligenceWithCitationEnabled_Succeeds()
    {
        var request = MinimalRequest() with { RequireCitation = true };
        var repository = new InMemoryAgentBehaviourProfileRepository();

        var result = await AgentBehaviourEndpoints.SaveVersion(
            "policy-intelligence", request, repository, Options(), Context(auditReason: "Setting the initial tone."), CancellationToken.None);

        var (status, body) = await ExecuteAsync<AgentBehaviourResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(body!.IsCustomized);
        Assert.Equal(1, body.CurrentVersion!.Version);
        // The immutable preamble must always be present in what the endpoint reports back.
        Assert.StartsWith(AgentBehaviourComposer.PlatformPreamble, body.EffectivePreamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveVersion_CalledTwice_IncrementsVersionAndKeepsBothInHistory()
    {
        var repository = new InMemoryAgentBehaviourProfileRepository();

        await AgentBehaviourEndpoints.SaveVersion(
            "claims-summary", MinimalRequest() with { Persona = "Formal" }, repository, Options(), Context(auditReason: "First version."), CancellationToken.None);
        var secondResult = await AgentBehaviourEndpoints.SaveVersion(
            "claims-summary", MinimalRequest() with { Persona = "Plain-language" }, repository, Options(), Context(auditReason: "Switching to plain language for pilot feedback."), CancellationToken.None);

        var (_, secondBody) = await ExecuteAsync<AgentBehaviourResponse>(secondResult);
        Assert.Equal(2, secondBody!.CurrentVersion!.Version);

        var versionsResult = await AgentBehaviourEndpoints.ListVersions("claims-summary", repository, Options(), Context(), CancellationToken.None);
        var (_, versionsBody) = await ExecuteAsync<JsonElement>(versionsResult);
        var versions = versionsBody.GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(2, versions.Length);
        // Newest first, and the first version's reason is still there — nothing was overwritten.
        Assert.Equal(2, versions[0].GetProperty("version").GetInt32());
        Assert.Equal(1, versions[1].GetProperty("version").GetInt32());
        Assert.Equal("First version.", versions[1].GetProperty("changeReason").GetString());
    }

    [Fact]
    public async Task GetCurrent_WithNoSavedProfile_StillReturnsAComposedPromptCarryingThePreamble()
    {
        var result = await AgentBehaviourEndpoints.GetCurrent(
            "claims-summary", new InMemoryAgentBehaviourProfileRepository(), Options(), Context(), CancellationToken.None);

        var (status, body) = await ExecuteAsync<AgentBehaviourResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(body!.IsCustomized);
        Assert.Null(body.CurrentVersion);
        Assert.StartsWith(AgentBehaviourComposer.PlatformPreamble, body.EffectivePreamble, StringComparison.Ordinal);
    }

    private static SaveAgentBehaviourRequest MinimalRequest() => new(
        SystemPromptOverride: null,
        Persona: "Plain-language",
        Language: "en",
        AllowedTopics: [],
        RefusalTopics: [],
        EscalationMessage: null,
        MaxResponseCharacters: null,
        RequireCitation: true);

    private static InfrastructureOptions Options() => new() { AuthMode = "Development" };

    private static DefaultHttpContext Context(string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-agent-behaviour" };
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private sealed class InMemoryAgentBehaviourProfileRepository : IAgentBehaviourProfileRepository
    {
        private readonly List<AgentBehaviourProfile> versions = [];

        public Task<AgentBehaviourProfile?> GetCurrentAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Version)
                .FirstOrDefault());

        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListVersionsAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Version)
                .ToArray());

        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListCurrentForAllAgentsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>(versions
                .Where(v => v.Scope.Matches(scope))
                .GroupBy(v => v.AgentKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(v => v.Version).First())
                .ToArray());

        public Task SaveAsync(AgentBehaviourProfile version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
    }
}

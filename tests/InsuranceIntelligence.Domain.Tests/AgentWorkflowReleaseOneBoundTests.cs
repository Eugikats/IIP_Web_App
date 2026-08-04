using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Seam: Release 1 orchestration allow-list — agents, tools, and graph keys that
/// may publish or execute on the current path (RF-04).
/// </summary>
public sealed class AgentWorkflowReleaseOneBoundTests
{
    [Fact]
    public void Allow_list_documents_release_one_agents_tools_and_canonical_graph()
    {
        Assert.Equal(1, AgentWorkflowReleaseOneBound.ExecutableReleaseNumber);
        Assert.Equal("document-to-decision", AgentWorkflowReleaseOneBound.CanonicalWorkflowKey);
        Assert.Contains("document-checker", AgentWorkflowReleaseOneBound.AllowedAgentKeys);
        Assert.Contains("workflow.evidence-quality", AgentWorkflowReleaseOneBound.AllowedToolKeys);
        Assert.Contains("documents.read", AgentWorkflowReleaseOneBound.AllowedToolKeys);
        Assert.DoesNotContain("claims-summary", AgentWorkflowReleaseOneBound.AllowedAgentKeys);
        Assert.DoesNotContain("policies.read", AgentWorkflowReleaseOneBound.AllowedToolKeys);
    }

    [Fact]
    public void Canonical_release_one_graph_is_within_allow_list()
    {
        var releaseOne = Assert.Single(AgentWorkflowCatalog.Releases, flow => flow.Release == 1);

        var exception = Record.Exception(() =>
        {
            AgentWorkflowReleaseOneBound.EnsureNodesWithinAllowList(releaseOne.Nodes);
            AgentWorkflowReleaseOneBound.EnsureExecutable(releaseOne);
        });

        Assert.Null(exception);
        Assert.Equal("Executable", AgentWorkflowReleaseOneBound.AvailabilityStatus(releaseOne));
    }

    [Theory]
    [InlineData(2, "fnol-claim-readiness")]
    [InlineData(3, "claims-triage-sla")]
    [InlineData(4, "policy-customer-response")]
    [InlineData(5, "underwriting-management-insight")]
    public void Held_releases_are_visible_but_not_executable(int release, string key)
    {
        var held = Assert.Single(AgentWorkflowCatalog.Releases, flow => flow.Release == release && flow.Key == key);

        Assert.Equal("Held", AgentWorkflowReleaseOneBound.AvailabilityStatus(held));
        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowReleaseOneBound.EnsureExecutable(held));

        Assert.Equal("workflowKey", exception.Field);
        Assert.Contains("Release 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("held", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(release.ToString(), exception.Message);
    }

    [Fact]
    public void Out_of_scope_agent_is_rejected_from_publish_allow_list()
    {
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger),
            Node("brief", AgentWorkflowNodeType.Agent, agentKey: "claims-summary"),
            Node("review", AgentWorkflowNodeType.HumanReview),
            Node("complete", AgentWorkflowNodeType.Complete)
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowReleaseOneBound.EnsureNodesWithinAllowList(nodes));

        Assert.Equal("nodes", exception.Field);
        Assert.Contains("claims-summary", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Release 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Out_of_scope_tool_is_rejected_from_publish_allow_list()
    {
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger),
            Node("policy", AgentWorkflowNodeType.Tool, toolKey: "policies.read"),
            Node("review", AgentWorkflowNodeType.HumanReview),
            Node("complete", AgentWorkflowNodeType.Complete)
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowReleaseOneBound.EnsureNodesWithinAllowList(nodes));

        Assert.Contains("policies.read", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Release 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentWorkflowNode Node(
        string key,
        AgentWorkflowNodeType type,
        string? agentKey = null,
        string? toolKey = null) =>
        new(
            key,
            key,
            key,
            type,
            agentKey,
            toolKey,
            type == AgentWorkflowNodeType.HumanReview
                ? AgentWorkflowSideEffect.ApprovalRequired
                : AgentWorkflowSideEffect.ReadOnly,
            new AgentWorkflowPosition(0, 0),
            RequiresHumanReview: type == AgentWorkflowNodeType.HumanReview,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}

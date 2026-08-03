using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Agents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AgentWorkflowCatalogTests
{
    [Fact]
    public void Canonical_agent_catalog_contains_exactly_ten_governed_agents()
    {
        Assert.Equal(10, AgentCatalog.Agents.Count);
        Assert.Equal(10, AgentCatalog.Agents.Select(agent => agent.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "command-center");
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "underwriting-intelligence");
    }

    [Fact]
    public void Release_catalog_contains_published_releases_one_through_five()
    {
        Assert.Equal([1, 2, 3, 4, 5], AgentWorkflowCatalog.Releases.Select(flow => flow.Release).ToArray());
        Assert.All(AgentWorkflowCatalog.Releases, flow =>
        {
            Assert.True(flow.IsSystemDefinition);
            Assert.Equal(AgentWorkflowDefinitionStatus.Published, flow.Status);
            Assert.Single(flow.Nodes, node => node.Type == AgentWorkflowNodeType.Trigger);
            Assert.Contains(flow.Nodes, node => node.Type == AgentWorkflowNodeType.HumanReview);
            Assert.Contains(flow.Nodes, node => node.Type == AgentWorkflowNodeType.Complete);
        });
    }

    [Fact]
    public void Release_flows_cannot_send_externally_or_reference_noncanonical_agents_and_tools()
    {
        foreach (var flow in AgentWorkflowCatalog.Releases)
        {
            Assert.DoesNotContain(flow.Nodes, node => node.SideEffect == AgentWorkflowSideEffect.ExternalSend);
            Assert.All(
                flow.Nodes.Where(node => node.Type == AgentWorkflowNodeType.Agent),
                node => Assert.NotNull(AgentCatalog.Find(node.AgentKey!)));
            Assert.All(
                flow.Nodes.Where(node => node.Type is AgentWorkflowNodeType.Tool or AgentWorkflowNodeType.Decision),
                node => Assert.Contains(node.ToolKey!, AgentWorkflowToolCatalog.Tools));
        }
    }

    [Fact]
    public void Every_approval_required_release_node_has_human_review_on_all_paths_to_complete()
    {
        foreach (var flow in AgentWorkflowCatalog.Releases)
        {
            var exception = Record.Exception(() =>
                AgentWorkflowHumanReviewInvariant.Ensure(flow.Nodes, flow.Edges));
            Assert.Null(exception);
        }
    }
}

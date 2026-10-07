using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Agents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AgentWorkflowCatalogTests
{
    [Fact]
    public void Canonical_agent_catalog_contains_the_ten_staff_agents_plus_the_customer_facing_assistant()
    {
        // The architecture doc's own ten-staff-agent count (docs/03-design-architecture/
        // iip_ai_agent_architecture_tool_registry_updated.md §3.1) is unchanged; customer-assistant
        // is an eleventh, deliberately separate entry, a customer-facing agent, not a staff one,
        // added so it gets the same kill-switch/governance discipline the other ten already have.
        // customer-summary (September 2026) is a twelfth: a staff agent for the Customer 360 page.
        // conversation-insights (October 2026) is a thirteenth: it keeps a prospect's record current
        // from their WhatsApp conversation.
        Assert.Equal(14, AgentCatalog.Agents.Count);
        Assert.Equal(14, AgentCatalog.Agents.Select(agent => agent.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "conversation-insights");
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "customer-summary");
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "command-center");
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "underwriting-intelligence");
        Assert.Contains(AgentCatalog.Agents, agent => agent.Key == "customer-assistant");
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

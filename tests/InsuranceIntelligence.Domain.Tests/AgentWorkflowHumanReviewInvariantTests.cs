using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Seam: agent-workflow publish/save validation — every consequential path must
/// reach HumanReview before Complete (all-paths, not exists-path).
/// </summary>
public sealed class AgentWorkflowHumanReviewInvariantTests
{
    [Fact]
    public void Canonical_release_one_graph_satisfies_all_paths_human_review_invariant()
    {
        var releaseOne = Assert.Single(AgentWorkflowCatalog.Releases, flow => flow.Release == 1);

        var exception = Record.Exception(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(releaseOne.Nodes, releaseOne.Edges));

        Assert.Null(exception);
    }

    [Fact]
    public void Bypass_complete_without_human_review_is_rejected()
    {
        // Trigger -> Agent(ApprovalRequired) -> Complete (no HumanReview)
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
            Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "document-checker"),
            Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
        };
        var edges = new[]
        {
            Edge("trigger", "prepare"),
            Edge("prepare", "complete")
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges));

        Assert.Equal("nodes", exception.Field);
        Assert.Contains("human review", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prepare", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Branch_that_bypasses_human_review_is_rejected_even_when_another_branch_is_safe()
    {
        // prepare -> review -> complete  (safe)
        // prepare -> complete            (bypass) — exists-path check would incorrectly pass
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
            Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "document-checker"),
            Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
            Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
        };
        var edges = new[]
        {
            Edge("trigger", "prepare"),
            Edge("prepare", "review"),
            Edge("review", "complete"),
            Edge("prepare", "complete")
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges));

        Assert.Contains("human review", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prepare", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cycle_without_human_review_before_complete_is_rejected()
    {
        // prepare -> loop-a -> loop-b -> prepare (cycle), and loop-b -> complete
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
            Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "document-checker"),
            Node("loop-a", AgentWorkflowNodeType.Tool, AgentWorkflowSideEffect.ReadOnly, toolKey: "documents.read"),
            Node("loop-b", AgentWorkflowNodeType.Decision, AgentWorkflowSideEffect.ReadOnly, toolKey: "workflow.evidence-quality"),
            Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
        };
        var edges = new[]
        {
            Edge("trigger", "prepare"),
            Edge("prepare", "loop-a"),
            Edge("loop-a", "loop-b"),
            Edge("loop-b", "prepare"),
            Edge("loop-b", "complete")
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unreachable_consequential_node_is_rejected()
    {
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
            Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "document-checker"),
            Node("orphan", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "routing"),
            Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
            Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
        };
        var edges = new[]
        {
            Edge("trigger", "prepare"),
            Edge("prepare", "review"),
            Edge("review", "complete")
            // orphan has no edges
        };

        var exception = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges));

        Assert.Contains("unreachable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orphan", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safe_graph_with_human_review_on_every_consequential_path_passes()
    {
        var nodes = new[]
        {
            Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
            Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, agentKey: "document-checker"),
            Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
            Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
        };
        var edges = new[]
        {
            Edge("trigger", "prepare"),
            Edge("prepare", "review"),
            Edge("review", "complete")
        };

        var exception = Record.Exception(() =>
            AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges));

        Assert.Null(exception);
    }

    private static AgentWorkflowNode Node(
        string key,
        AgentWorkflowNodeType type,
        AgentWorkflowSideEffect sideEffect,
        string? agentKey = null,
        string? toolKey = null) =>
        new(
            key,
            key,
            key,
            type,
            agentKey,
            toolKey,
            sideEffect,
            new AgentWorkflowPosition(0, 0),
            RequiresHumanReview: type == AgentWorkflowNodeType.HumanReview || sideEffect == AgentWorkflowSideEffect.ApprovalRequired,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static AgentWorkflowEdge Edge(string source, string target) =>
        new(source, target, Outcome: null, Label: null);
}

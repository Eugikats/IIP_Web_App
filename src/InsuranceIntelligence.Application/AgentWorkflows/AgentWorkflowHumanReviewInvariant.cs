using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Application.AgentWorkflows;

/// <summary>
/// Publish/save invariant: every executable path from a consequential or external
/// side-effect node to Complete must pass through HumanReview. Existence of one
/// safe path is not enough, bypass branches, unsafe cycles, and unreachable
/// consequential nodes fail publication.
/// </summary>
public static class AgentWorkflowHumanReviewInvariant
{
    public static void Ensure(
        IReadOnlyList<AgentWorkflowNode> nodes,
        IReadOnlyList<AgentWorkflowEdge> edges)
    {
        var byKey = nodes.ToDictionary(node => node.Key, StringComparer.OrdinalIgnoreCase);
        var adjacency = nodes.ToDictionary(
            node => node.Key,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges)
        {
            adjacency[edge.SourceNodeKey].Add(edge.TargetNodeKey);
        }

        var trigger = nodes.Single(node => node.Type == AgentWorkflowNodeType.Trigger);
        var reachableFromTrigger = ReachableKeys(trigger.Key, adjacency);

        foreach (var governed in nodes.Where(IsConsequentialNonReview))
        {
            if (!reachableFromTrigger.Contains(governed.Key))
            {
                throw new AgentWorkflowValidationException(
                    nameof(nodes),
                    $"Consequential node '{governed.Key}' is unreachable from the workflow trigger.");
            }

            EnsureAllPathsToCompletePassHumanReview(governed.Key, byKey, adjacency);
        }
    }

    private static bool IsConsequentialNonReview(AgentWorkflowNode node) =>
        node.Type != AgentWorkflowNodeType.HumanReview
        && node.SideEffect is AgentWorkflowSideEffect.ApprovalRequired
            or AgentWorkflowSideEffect.InternalWrite
            or AgentWorkflowSideEffect.ExternalSend
            or AgentWorkflowSideEffect.DraftOnly;

    private static void EnsureAllPathsToCompletePassHumanReview(
        string startKey,
        IReadOnlyDictionary<string, AgentWorkflowNode> byKey,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var foundComplete = false;
        var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Dfs(string key, bool seenHumanReview)
        {
            if (!path.Add(key))
            {
                if (!seenHumanReview)
                {
                    throw new AgentWorkflowValidationException(
                        "nodes",
                        $"Node '{startKey}' participates in a cycle that can reach completion without a human review gate.");
                }

                return;
            }

            var node = byKey[key];
            var reviewed = seenHumanReview || node.Type == AgentWorkflowNodeType.HumanReview;

            if (node.Type == AgentWorkflowNodeType.Complete)
            {
                if (!reviewed)
                {
                    throw new AgentWorkflowValidationException(
                        "nodes",
                        $"Node '{startKey}' has an executable path to Complete that bypasses human review.");
                }

                foundComplete = true;
                path.Remove(key);
                return;
            }

            var successors = adjacency[key];
            if (successors.Count == 0)
            {
                path.Remove(key);
                return;
            }

            foreach (var next in successors)
            {
                Dfs(next, reviewed);
            }

            path.Remove(key);
        }

        Dfs(startKey, seenHumanReview: false);

        if (!foundComplete)
        {
            throw new AgentWorkflowValidationException(
                "nodes",
                $"Node '{startKey}' requires a downstream human review gate before completion.");
        }
    }

    private static HashSet<string> ReachableKeys(
        string startKey,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(startKey);
        while (stack.Count > 0)
        {
            var key = stack.Pop();
            if (!visited.Add(key))
            {
                continue;
            }

            foreach (var next in adjacency[key])
            {
                stack.Push(next);
            }
        }

        return visited;
    }
}

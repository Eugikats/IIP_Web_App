using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Application.AgentWorkflows;

/// <summary>
/// RF-05: collect the correction root and every graph descendant that must be
/// invalidated when upstream extraction evidence changes.
/// </summary>
public static class AgentWorkflowDescendantInvalidation
{
    /// <summary>
    /// Prefer the HumanReview node's <c>correctionTarget</c>; otherwise the first Agent node.
    /// </summary>
    public static string ResolveEvidenceCorrectionRoot(AgentWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var review in definition.Nodes.Where(node => node.Type == AgentWorkflowNodeType.HumanReview))
        {
            if (review.Configuration.TryGetValue("correctionTarget", out var target)
                && !string.IsNullOrWhiteSpace(target)
                && definition.Nodes.Any(node => node.Key.Equals(target.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return target.Trim();
            }
        }

        var firstAgent = definition.Nodes.FirstOrDefault(node => node.Type == AgentWorkflowNodeType.Agent);
        if (firstAgent is not null)
        {
            return firstAgent.Key;
        }

        throw new AgentWorkflowValidationException(
            "correctionRoot",
            "Workflow definition has no evidence-correction root (HumanReview correctionTarget or Agent node).");
    }

    public static IReadOnlyList<string> CollectRootAndDescendants(
        AgentWorkflowDefinition definition,
        string rootNodeKey)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(rootNodeKey))
        {
            throw new ArgumentException("A root node key is required.", nameof(rootNodeKey));
        }

        var normalizedRoot = rootNodeKey.Trim();
        if (!definition.Nodes.Any(node => node.Key.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentWorkflowValidationException(nameof(rootNodeKey), "Correction root node was not found on the workflow definition.");
        }

        var adjacency = definition.Edges
            .GroupBy(edge => edge.SourceNodeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.TargetNodeKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(normalizedRoot);
        seen.Add(normalizedRoot);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);
            if (!adjacency.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (seen.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return ordered;
    }
}

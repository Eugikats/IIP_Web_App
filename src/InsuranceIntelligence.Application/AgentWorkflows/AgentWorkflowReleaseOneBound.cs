using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Application.AgentWorkflows;

/// <summary>
/// RF-04 — Release 1 orchestration allow-list.
///
/// Decision: retain the orchestration control plane only as a governed Release 1
/// capability. Releases 2–5 remain catalog-visible (held) and cannot publish into
/// or execute on the Release 1 path until their providers and earlier application
/// services are proven. Custom tenant graphs may publish/run only when every
/// agent and typed tool node is within this allow-list.
///
/// Canonical executable graph: <c>document-to-decision</c> (Document Checker →
/// evidence quality gate → human extraction review).
/// </summary>
public static class AgentWorkflowReleaseOneBound
{
    public const int ExecutableReleaseNumber = 1;

    public const string CanonicalWorkflowKey = "document-to-decision";

    public const string HeldStatus = "Held";

    public const string ExecutableStatus = "Executable";

    /// <summary>Agents permitted on the Release 1 orchestration path.</summary>
    public static readonly IReadOnlySet<string> AllowedAgentKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "document-checker"
        };

    /// <summary>
    /// Typed tools permitted on the Release 1 path. Broader catalog tools
    /// (policies.read, claims.read, …) remain defined for later releases but
    /// cannot enter publish or execute contracts here.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedToolKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "documents.read",
            "workflow.evidence-quality"
        };

    public static string AvailabilityStatus(AgentWorkflowDefinition definition)
    {
        if (IsHeldSystemRelease(definition))
        {
            return HeldStatus;
        }

        return ExecutableStatus;
    }

    public static bool IsHeldSystemRelease(AgentWorkflowDefinition definition) =>
        definition.IsSystemDefinition && definition.Release > ExecutableReleaseNumber;

    /// <summary>
    /// Publish/save boundary: every agent and typed tool/decision node must be
    /// on the Release 1 allow-list.
    /// </summary>
    public static void EnsureNodesWithinAllowList(IReadOnlyList<AgentWorkflowNode> nodes)
    {
        foreach (var node in nodes.Where(node => node.Type == AgentWorkflowNodeType.Agent))
        {
            if (string.IsNullOrWhiteSpace(node.AgentKey) || !AllowedAgentKeys.Contains(node.AgentKey))
            {
                throw new AgentWorkflowValidationException(
                    nameof(nodes),
                    $"Agent node '{node.Key}' references '{node.AgentKey ?? "(missing)"}', which is outside the Release 1 orchestration allow-list. Only [{string.Join(", ", AllowedAgentKeys.OrderBy(k => k, StringComparer.Ordinal))}] may publish or run on the current path.");
            }
        }

        foreach (var node in nodes.Where(node => node.Type is AgentWorkflowNodeType.Tool or AgentWorkflowNodeType.Decision))
        {
            if (string.IsNullOrWhiteSpace(node.ToolKey) || !AllowedToolKeys.Contains(node.ToolKey))
            {
                throw new AgentWorkflowValidationException(
                    nameof(nodes),
                    $"Tool node '{node.Key}' references '{node.ToolKey ?? "(missing)"}', which is outside the Release 1 orchestration allow-list. Only [{string.Join(", ", AllowedToolKeys.OrderBy(k => k, StringComparer.Ordinal))}] may publish or run on the current path.");
            }
        }
    }

    /// <summary>
    /// Execute/start boundary: held system releases cannot start; executable
    /// definitions must still satisfy the node allow-list.
    /// </summary>
    public static void EnsureExecutable(AgentWorkflowDefinition definition)
    {
        if (IsHeldSystemRelease(definition))
        {
            throw new AgentWorkflowValidationException(
                "workflowKey",
                $"Release {definition.Release} workflow '{definition.Key}' is held and cannot run on the Release 1 orchestration path. Only Release {ExecutableReleaseNumber} ('{CanonicalWorkflowKey}') and tenant graphs within the Release 1 allow-list are executable.");
        }

        EnsureNodesWithinAllowList(definition.Nodes);
    }

    /// <summary>
    /// Queue dispatch boundary: reject out-of-scope agent/tool keys before a
    /// worker message is published.
    /// </summary>
    public static void EnsureDispatchAllowed(AgentWorkflowNode node)
    {
        switch (node.Type)
        {
            case AgentWorkflowNodeType.Agent:
                if (string.IsNullOrWhiteSpace(node.AgentKey) || !AllowedAgentKeys.Contains(node.AgentKey))
                {
                    throw new AgentWorkflowValidationException(
                        nameof(node),
                        $"Cannot queue agent '{node.AgentKey ?? "(missing)"}' on node '{node.Key}': outside the Release 1 orchestration allow-list.");
                }
                break;
            case AgentWorkflowNodeType.Tool:
            case AgentWorkflowNodeType.Decision:
                if (string.IsNullOrWhiteSpace(node.ToolKey) || !AllowedToolKeys.Contains(node.ToolKey))
                {
                    throw new AgentWorkflowValidationException(
                        nameof(node),
                        $"Cannot queue tool '{node.ToolKey ?? "(missing)"}' on node '{node.Key}': outside the Release 1 orchestration allow-list.");
                }
                break;
            case AgentWorkflowNodeType.Trigger:
            case AgentWorkflowNodeType.HumanReview:
            case AgentWorkflowNodeType.Complete:
                break;
            default:
                throw new AgentWorkflowValidationException(
                    nameof(node),
                    $"Cannot queue node '{node.Key}' of type '{node.Type}' on the Release 1 orchestration path.");
        }
    }

    public static string HeldCatalogPreface(int release) =>
        $"[Held — Release {release} is not executable on the Release 1 orchestration path until its providers and earlier application services are proven.] ";
}

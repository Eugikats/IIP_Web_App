using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AgentWorkflowDescendantInvalidationTests
{
    [Fact]
    public void ResolveEvidenceCorrectionRoot_Uses_HumanReview_CorrectionTarget()
    {
        var definition = AgentWorkflowCatalog.Find("document-to-decision")!;
        Assert.Equal("document-check", AgentWorkflowDescendantInvalidation.ResolveEvidenceCorrectionRoot(definition));
    }

    [Fact]
    public void CollectRootAndDescendants_Includes_Root_And_All_Downstream_Nodes()
    {
        var definition = AgentWorkflowCatalog.Find("document-to-decision")!;
        var keys = AgentWorkflowDescendantInvalidation.CollectRootAndDescendants(definition, "document-check");

        Assert.Equal(
            ["document-check", "quality-gate", "extraction-review", "complete"],
            keys);
    }

    [Fact]
    public void CollectRootAndDescendants_From_Review_Includes_Only_Descendants()
    {
        var definition = AgentWorkflowCatalog.Find("document-to-decision")!;
        var keys = AgentWorkflowDescendantInvalidation.CollectRootAndDescendants(definition, "extraction-review");

        Assert.Equal(["extraction-review", "complete"], keys);
    }

    [Fact]
    public void CollectRootAndDescendants_Rejects_Unknown_Root()
    {
        var definition = AgentWorkflowCatalog.Find("document-to-decision")!;
        var ex = Assert.Throws<AgentWorkflowValidationException>(() =>
            AgentWorkflowDescendantInvalidation.CollectRootAndDescendants(definition, "missing-node"));
        Assert.Equal("rootNodeKey", ex.Field);
    }
}

using InsuranceIntelligence.Domain.Queue;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class Phase7DomainTests
{
    [Fact]
    public void QueueMessageEnvelope_CarriesTenantWorkspaceAndCorrelation()
    {
        var envelope = new QueueMessageEnvelope(
            "document-processing",
            "DocumentUploaded",
            "tenant-demo",
            "workspace-claims",
            "corr-phase7",
            new { documentId = Guid.NewGuid() });

        Assert.Equal("tenant-demo", envelope.TenantId);
        Assert.Equal("workspace-claims", envelope.WorkspaceId);
        Assert.Equal("corr-phase7", envelope.CorrelationId);
        Assert.DoesNotContain("secret", envelope.QueueName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", envelope.MessageType, StringComparison.OrdinalIgnoreCase);
    }
}

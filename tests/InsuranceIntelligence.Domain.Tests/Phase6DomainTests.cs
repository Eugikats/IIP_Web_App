using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class Phase6DomainTests
{
    [Fact]
    public void EmailToCaseMatchResult_CanRepresentExistingCaseLink()
    {
        var caseId = Guid.NewGuid();
        var result = new EmailToCaseMatchResult(caseId, "case_id", false);

        Assert.Equal(caseId, result.CaseId);
        Assert.False(result.CreateNewCase);
    }

    [Fact]
    public void EmailToCaseMatchResult_CanRepresentNewCase()
    {
        var result = new EmailToCaseMatchResult(null, "no_match", true);

        Assert.True(result.CreateNewCase);
        Assert.Null(result.CaseId);
    }

    [Fact]
    public void SensitiveCommunication_IsNotApprovedByDefault()
    {
        var request = new CommunicationSendRequest(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            Guid.NewGuid(),
            CommunicationChannel.Email,
            "customer@example.test",
            "Please review your claim update.",
            "local-service-agent",
            Approved: false,
            LowRisk: false,
            "corr");

        Assert.False(request.Approved);
        Assert.False(request.LowRisk);
    }

    [Fact]
    public void CommunicationHistory_CanCarryCustomerSafeMessage()
    {
        var communication = new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = Guid.NewGuid(),
            Channel = CommunicationChannel.Email,
            Direction = "Outbound",
            MessageBody = "We received your claim documents and will contact you with the next update.",
            SentBy = "local-service-agent",
            DeliveryStatus = "SimulatedSent",
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

        Assert.DoesNotContain("internal note", communication.MessageBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw prompt", communication.MessageBody, StringComparison.OrdinalIgnoreCase);
    }
}

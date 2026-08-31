using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests.Cases;

public sealed class ClaimGovernedPathServiceTests
{
    private readonly ClaimGovernedPathService _service = new();

    [Fact]
    public void Route_Exposes_ClaimType_Matrix_For_Governed_Path()
    {
        Assert.Equal(ClaimPathMode.HappyPath, _service.Route("Own damage").PathMode);
        Assert.Equal(ClaimRoutingPolicy.ThirdPartyDeskTeam, _service.Route("Third-party property").AssignedTeam);
        Assert.True(_service.Route("Theft").RequiresDesignatedHumanBeforeExport);
        Assert.Equal(CaseStatus.Escalated, _service.Route("Fatality").InitialStatus);
    }

    [Fact]
    public void RequiredEvidence_Varies_By_Claim_Type_On_Same_Path()
    {
        var ownDamage = Sample("Own damage");
        var injury = Sample("Bodily injury");

        Assert.DoesNotContain(DocumentType.MedicalReport, _service.RequiredEvidence(ownDamage).RequiredDocuments);
        Assert.Contains(DocumentType.MedicalReport, _service.RequiredEvidence(injury).RequiredDocuments);
    }

    [Fact]
    public void Ai_Cannot_Clear_Fraud_Escalation()
    {
        var routing = _service.Route("Own damage", signals: new ClaimRoutingSignals(FraudSignalPresent: true));

        var exception = Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureDesignatedHumanMayClearEscalation(routing, DecisionActorType.Agent));

        Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ClaimEscalationTrigger.FraudSignal, routing.EscalationTriggers);
    }

    private static ClaimCase Sample(string claimType) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = claimType,
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Test",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-gov-path"
        };
}

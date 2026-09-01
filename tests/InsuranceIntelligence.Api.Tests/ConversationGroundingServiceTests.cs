using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ConversationGroundingServiceTests
{
    [Fact]
    public async Task CompareDetectsClaimEvidenceVersionChange()
    {
        var scope = TenantScope.Create("tenant-grounding", "workspace-grounding");
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var service = new ConversationGroundingService(cases, documents);
        var now = DateTimeOffset.UtcNow;
        var claim = NewClaim(scope, now);
        await cases.SaveAsync(claim);
        var response = new CommandCenterQueryResponse(
            [new ClaimsTableBlock([new ClaimRow(claim.CaseId.ToString(), claim.CustomerName, "Motor", "UnderReview", "at_risk", 80m, "owner-1", $"/app/claims/{claim.CaseId}")])],
            Guid.NewGuid(), "corr-grounding", "Deterministic", "v1", "Internal", false, now);
        var captured = await service.CaptureAsync(response, scope);
        var conversation = new CommandCenterConversation
        {
            ConversationId = Guid.NewGuid(), Scope = scope, OwnerUserId = "owner-1", Title = "Review claim",
            TitleSource = CommandCenterConversationTitleSource.SystemGenerated, Status = CommandCenterConversationStatus.Active,
            Turns = [new CommandCenterConversationTurn(Guid.NewGuid(), "Review claim", response, now)],
            CreatedAt = now, LastActivityAt = now, Version = 1,
            PrimaryFocus = captured.Focus, EvidenceSnapshot = captured.Snapshot,
        };

        Assert.Equal("current", (await service.CompareAsync(conversation)).Status);
        await cases.SaveAsync(claim with { CompletenessScore = 90m, UpdatedAt = now.AddMinutes(5) });

        var changed = await service.CompareAsync(conversation);
        Assert.Equal("changed", changed.Status);
        Assert.NotEqual(changed.HistoricalVersion, changed.CurrentVersion);
        Assert.Contains("Recheck", changed.SafeChangeSummary);
    }

    private static ClaimCase NewClaim(TenantScope scope, DateTimeOffset now) => new()
    {
        CaseId = Guid.NewGuid(), Scope = scope, CustomerName = "Test customer", PolicyNumber = "POL-1",
        ProductLine = "Motor", ClaimType = "Accident", Channel = "WebPortal",
        IncidentDate = new DateOnly(2026, 8, 31), IncidentDescription = "Test collision",
        Status = CaseStatus.MissingDocuments, CompletenessScore = 80m, AssignedUserId = "owner-1",
        CreatedByUserId = "owner-1", CreatedAt = now, UpdatedAt = now, CorrelationId = "corr-grounding",
    };
}

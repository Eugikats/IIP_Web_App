using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests.Cases;

public sealed class CaseAssignmentPolicyTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public void SelectAssignee_NoTeamHistory_ReturnsNull()
    {
        var assignee = CaseAssignmentPolicy.SelectAssignee("Claims Intake", []);

        Assert.Null(assignee);
    }

    [Fact]
    public void SelectAssignee_BlankTeam_ReturnsNull()
    {
        var assignee = CaseAssignmentPolicy.SelectAssignee(" ", [Seed("officer-a", "Claims Intake", CaseStatus.New)]);

        Assert.Null(assignee);
    }

    [Fact]
    public void SelectAssignee_PicksMemberWithFewestOpenCases()
    {
        ClaimCase[] cases =
        [
            Seed("officer-busy", "Claims Intake", CaseStatus.InReview),
            Seed("officer-busy", "Claims Intake", CaseStatus.PendingApproval),
            Seed("officer-quiet", "Claims Intake", CaseStatus.Closed),
        ];

        var assignee = CaseAssignmentPolicy.SelectAssignee("Claims Intake", cases);

        Assert.Equal("officer-quiet", assignee);
    }

    [Fact]
    public void SelectAssignee_ClosedAndSettledCasesDoNotCountAsLoad()
    {
        ClaimCase[] cases =
        [
            Seed("officer-a", "Claims Intake", CaseStatus.Closed),
            Seed("officer-a", "Claims Intake", CaseStatus.Settled),
            Seed("officer-b", "Claims Intake", CaseStatus.InReview),
        ];

        var assignee = CaseAssignmentPolicy.SelectAssignee("Claims Intake", cases);

        Assert.Equal("officer-a", assignee); // zero open cases beats one
    }

    [Fact]
    public void SelectAssignee_EqualLoad_PicksLeastRecentlyAssigned()
    {
        var now = DateTimeOffset.UtcNow;
        ClaimCase[] cases =
        [
            Seed("officer-a", "Claims Intake", CaseStatus.New, now.AddHours(-1)),
            Seed("officer-b", "Claims Intake", CaseStatus.New, now.AddHours(-5)),
        ];

        var assignee = CaseAssignmentPolicy.SelectAssignee("Claims Intake", cases);

        Assert.Equal("officer-b", assignee);
    }

    [Fact]
    public void SelectAssignee_IgnoresOtherTeamsAndUnassignedCases()
    {
        ClaimCase[] cases =
        [
            Seed("officer-fraud", "Designated Fraud Escalation", CaseStatus.New),
            Seed(null, "Claims Intake", CaseStatus.New),
        ];

        var assignee = CaseAssignmentPolicy.SelectAssignee("Claims Intake", cases);

        Assert.Null(assignee);
    }

    private static ClaimCase Seed(string? assignedUserId, string team, CaseStatus status, DateTimeOffset? updatedAt = null) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Seed Customer",
            PolicyNumber = "POL-SEED",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 1),
            IncidentDescription = "Seeded for CaseAssignmentPolicy test.",
            Status = status,
            CompletenessScore = 50,
            AssignedUserId = assignedUserId,
            AssignedTeam = team,
            CreatedByUserId = "seed",
            CreatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            CorrelationId = "seed-correlation",
        };
}

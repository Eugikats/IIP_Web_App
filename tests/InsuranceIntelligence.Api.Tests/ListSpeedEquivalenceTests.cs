using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Two lists were made faster by looking things up in an index instead of comparing everything with everything. These
/// pin that the answers did not change.
/// </summary>
public sealed class ListSpeedEquivalenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly string[] Numbers =
    [
        "0776678989", "+256776678989", "256776678989", "256 776 678 989", "776678989", "0776 678 989",
        "0701234567", "256701234567", "+44 7700 900123", "07700900123", "1234567", "12345", "", "n/a",
    ];

    [Fact]
    public void NumbersThatAreTheSame_AlwaysShareTheTailTheIndexUses()
    {
        // If SameNumber ever said yes for two numbers with different tails, the index would silently miss a match.
        foreach (var first in Numbers)
        {
            foreach (var second in Numbers)
            {
                if (!PhoneNumbers.SameNumber(first, second)) continue;
                Assert.Equal(PhoneNumbers.Tail(PhoneNumbers.Digits(first)), PhoneNumbers.Tail(PhoneNumbers.Digits(second)));
            }
        }
    }

    [Fact]
    public async Task CustomerList_CountsOpenClaimsOncePerClaim_AndOnlyForTheRightPerson()
    {
        await using var db = new InsuranceIntelligenceDbContext(
            new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var scope = new TenantScope("tenant-a", "workspace-a");
        db.Customers.AddRange(
            CustomerRow(scope, "c-1", "Amina Example", "amina@example.test", "0776678989"),
            CustomerRow(scope, "c-2", "Brian Example", null, "0701234567"),
            CustomerRow(scope, "c-3", "Carol Example", null, null));
        db.Cases.AddRange(
            // Matches Amina by both email and phone: still one claim.
            ClaimRow(scope, "Amina Example", "amina@example.test", "+256776678989", CaseStatus.MissingDocuments),
            // Matches Amina by phone only, the same digits written with spaces (the list matches phones on digits exactly).
            ClaimRow(scope, "A. Example", null, "0776 678 989", CaseStatus.ReadyForReview),
            // Amina's, but closed: not counted.
            ClaimRow(scope, "Amina Example", "amina@example.test", null, CaseStatus.Closed),
            // Matches Brian by phone.
            ClaimRow(scope, "Brian Example", null, "0701234567", CaseStatus.Escalated),
            // Carol has no contact details, so she matches on name.
            ClaimRow(scope, "Carol Example", null, null, CaseStatus.MissingDocuments),
            // A stranger with Brian's name but a different phone must not count for Brian.
            ClaimRow(scope, "Brian Example", null, "0799999999", CaseStatus.MissingDocuments));
        await db.SaveChangesAsync();

        var list = await new EfCustomerReadRepository(db).ListCustomersAsync(scope.TenantId, scope.WorkspaceId);

        Assert.Equal(2, list.Single(item => item.CustomerId == "c-1").ActiveClaims);
        Assert.Equal(1, list.Single(item => item.CustomerId == "c-2").ActiveClaims);
        Assert.Equal(1, list.Single(item => item.CustomerId == "c-3").ActiveClaims);
    }

    private static PersistedCustomer CustomerRow(TenantScope scope, string id, string name, string? email, string? phone) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = scope.TenantId,
            WorkspaceId = scope.WorkspaceId,
            PayloadJson = JsonSerializer.Serialize(new InsuranceIntelligence.Domain.Customers.Customer(id, scope.TenantId, scope.WorkspaceId, name, email, phone), Json),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PersistedAggregate ClaimRow(TenantScope scope, string name, string? email, string? phone, CaseStatus status)
    {
        var claim = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = name,
            CustomerEmail = email,
            CustomerPhone = phone,
            PolicyNumber = "POL-0001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Email",
            IncidentDate = new DateOnly(2026, 8, 1),
            IncidentDescription = "Minor vehicle damage.",
            Status = status,
            CompletenessScore = 50m,
            RiskLevel = "Medium",
            Priority = CasePriority.Normal,
            SlaDueAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            AssignedUserId = "user-1",
            AssignedTeam = "Claims",
            CreatedByUserId = "user-1",
            CreatedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            CorrelationId = "corr-1",
        };
        return new PersistedAggregate
        {
            Id = claim.CaseId,
            TenantId = scope.TenantId,
            WorkspaceId = scope.WorkspaceId,
            PayloadJson = JsonSerializer.Serialize(claim, Json),
            UpdatedAt = claim.UpdatedAt,
        };
    }
}

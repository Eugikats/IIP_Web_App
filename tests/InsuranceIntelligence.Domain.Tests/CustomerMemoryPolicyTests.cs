using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 26: what a returning customer is offered again, and only for the same customer.</summary>
public sealed class CustomerMemoryPolicyTests
{
    private static readonly TenantScope Scope = TenantScope.Create("t", "w");

    private static ClaimCase Claim(string policy = "POL-1", string phone = "+256772000111", string email = "a@example.com", DateTimeOffset? created = null, Dictionary<string, string>? values = null) => new()
    {
        CaseId = Guid.NewGuid(),
        Scope = Scope,
        CustomerName = "Customer",
        CustomerEmail = email,
        CustomerPhone = phone,
        PolicyNumber = policy,
        ProductLine = "Motor",
        ClaimType = "Own damage",
        Channel = "Phone",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        IncidentDescription = "x",
        Status = CaseStatus.New,
        CreatedAt = created ?? DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        FieldValues = values,
        CompletenessScore = 0m,
        CorrelationId = "c",
        CreatedByUserId = "u",
    };

    [Fact]
    public void TheSameCustomerIsRecognisedByPolicyPhoneOrEmailAndNeverByChance()
    {
        var current = Claim("POL-1", "+256772000111", "a@example.com");

        Assert.True(CustomerMemoryPolicy.SameCustomer(current, Claim("pol-1", "+256700000000", "z@example.com")));
        Assert.True(CustomerMemoryPolicy.SameCustomer(current, Claim("POL-9", "0772000111", "z@example.com")));
        Assert.True(CustomerMemoryPolicy.SameCustomer(current, Claim("POL-9", "+256700000000", "A@Example.com")));
        Assert.False(CustomerMemoryPolicy.SameCustomer(current, Claim("POL-9", "+256700000000", "z@example.com")));
        Assert.False(CustomerMemoryPolicy.SameCustomer(current, current));
    }

    [Fact]
    public void AClaimWithNoContactDetailsMatchesNobody()
    {
        var current = Claim("", "", "");

        Assert.False(CustomerMemoryPolicy.SameCustomer(current, Claim("POL-9", "", "")));
        Assert.False(CustomerMemoryPolicy.SameCustomer("", "", "", Claim("POL-9", "", "")));
    }

    [Fact]
    public void TheMostRecentAnswerWinsAndOnlyNamedDetailsThatTheClaimAsksAboutAreOffered()
    {
        var older = Claim(created: DateTimeOffset.UtcNow.AddDays(-30), values: new() { ["drivingPermitNumber"] = "OLD-1", ["accidentLocation"] = "Kampala Road" });
        var newer = Claim(created: DateTimeOffset.UtcNow.AddDays(-5), values: new() { ["drivingPermitNumber"] = "NEW-2", ["vehicleRegistration"] = "UAX 123A" });
        var labels = new Dictionary<string, string> { ["drivingPermitNumber"] = "Driving permit number", ["vehicleRegistration"] = "Vehicle registration", ["accidentLocation"] = "Where it happened" };

        var offered = CustomerMemoryPolicy.Values(
            new Dictionary<string, string>(), [older, newer], CustomerMemoryPolicy.DefaultFieldKeys, key => labels.GetValueOrDefault(key), caseId => $"CLM-{caseId.ToString("N")[..4]}");

        Assert.Equal(["drivingPermitNumber", "vehicleRegistration"], offered.Select(value => value.Key).ToArray());
        Assert.Equal("NEW-2", offered[0].Value);
        Assert.Equal("Driving permit number", offered[0].Label);
        Assert.DoesNotContain(offered, value => value.Key == "accidentLocation");
    }

    [Fact]
    public void ADetailTheClaimAlreadyHasIsNotOfferedAgainAndOneItDoesNotAskAboutIsLeftOut()
    {
        var earlier = Claim(values: new() { ["drivingPermitNumber"] = "DP-1", ["vehicleRegistration"] = "UAX 123A" });

        var offered = CustomerMemoryPolicy.Values(
            new Dictionary<string, string> { ["drivingPermitNumber"] = "DP-2" }, [earlier], CustomerMemoryPolicy.DefaultFieldKeys,
            key => key == "vehicleRegistration" ? "Vehicle registration" : null, caseId => "CLM-1");

        Assert.DoesNotContain(offered, value => value.Key == "drivingPermitNumber");
        Assert.Single(offered);
    }

    [Fact]
    public void BlankAnswersAreNeverOffered()
    {
        var earlier = Claim(values: new() { ["drivingPermitNumber"] = "   " });

        Assert.Empty(CustomerMemoryPolicy.Values(new Dictionary<string, string>(), [earlier], CustomerMemoryPolicy.DefaultFieldKeys, _ => "Label", _ => "CLM-1"));
    }

    private static CaseDocument Document(Guid caseId, string key, DateTimeOffset at, bool superseded = false, DocumentProcessingStatus status = DocumentProcessingStatus.Processed) => new()
    {
        DocumentId = Guid.NewGuid(),
        CaseId = caseId,
        Scope = Scope,
        OriginalFileName = $"{key}-{at:HHmmss}.jpg",
        ContentType = "image/jpeg",
        FileSizeBytes = 10,
        ObjectKey = "k",
        DocumentType = DocumentType.Unknown,
        DocumentTypeKey = key,
        ProcessingStatus = status,
        Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "x", DateTimeOffset.UtcNow),
        Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "mock", DateTimeOffset.UtcNow, false, null, null),
        ValidationWarnings = [],
        UploadedByUserId = "u",
        UploadedAt = at,
        CorrelationId = "c",
        IsSuperseded = superseded,
    };

    [Fact]
    public void TheNewestUsableDocumentOfEachNamedKindIsOfferedOnlyWhereTheClaimAsksForItAndHasNothingYet()
    {
        var first = Claim(created: DateTimeOffset.UtcNow.AddDays(-60));
        var second = Claim(created: DateTimeOffset.UtcNow.AddDays(-10));
        var oldId = Document(first.CaseId, "CustomerId", DateTimeOffset.UtcNow.AddDays(-60));
        var newId = Document(second.CaseId, "CustomerId", DateTimeOffset.UtcNow.AddDays(-10));
        var photo = Document(second.CaseId, "PhotoEvidence", DateTimeOffset.UtcNow.AddDays(-10));
        var asked = new Dictionary<string, string> { ["CustomerId"] = "Your ID", ["PhotoEvidence"] = "Photos" };

        var offered = CustomerMemoryPolicy.Documents(
            [(first, [oldId]), (second, [newId, photo])], CustomerMemoryPolicy.DefaultDocumentKeys, asked, new HashSet<string>(), _ => "CLM-X");

        var only = Assert.Single(offered);
        Assert.Equal(newId.DocumentId, only.DocumentId);
        Assert.Equal("Your ID", only.SlotLabel);

        Assert.Empty(CustomerMemoryPolicy.Documents([(second, [newId])], CustomerMemoryPolicy.DefaultDocumentKeys, asked, new HashSet<string> { "CustomerId" }, _ => "CLM-X"));
        Assert.Empty(CustomerMemoryPolicy.Documents([(second, [newId])], CustomerMemoryPolicy.DefaultDocumentKeys, new Dictionary<string, string>(), new HashSet<string>(), _ => "CLM-X"));
    }

    [Fact]
    public void ADocumentThatWasReplacedOrCouldNotBeReadIsNeverOffered()
    {
        var earlier = Claim();
        var replaced = Document(earlier.CaseId, "CustomerId", DateTimeOffset.UtcNow.AddDays(-3), superseded: true);
        var failed = Document(earlier.CaseId, "CustomerId", DateTimeOffset.UtcNow.AddDays(-2), status: DocumentProcessingStatus.Failed);

        Assert.Empty(CustomerMemoryPolicy.Documents([(earlier, [replaced, failed])], CustomerMemoryPolicy.DefaultDocumentKeys, new Dictionary<string, string> { ["CustomerId"] = "Your ID" }, new HashSet<string>(), _ => "CLM-X"));
    }

    [Fact]
    public void TheWorkspacesOwnKeysReplaceTheDefaultsAndAnEmptyListMeansTheDefaults()
    {
        Assert.Equal(["a"], CustomerMemoryPolicy.KeysOrDefault(["a"], CustomerMemoryPolicy.DefaultFieldKeys));
        Assert.Equal(CustomerMemoryPolicy.DefaultFieldKeys, CustomerMemoryPolicy.KeysOrDefault([], CustomerMemoryPolicy.DefaultFieldKeys));
        Assert.Equal(CustomerMemoryPolicy.DefaultDocumentKeys, CustomerMemoryPolicy.KeysOrDefault(null, CustomerMemoryPolicy.DefaultDocumentKeys));
        Assert.Equal(["CustomerId"], CustomerMemoryPolicy.DefaultDocumentKeys);
    }
}

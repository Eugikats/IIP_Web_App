using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AuthenticityRulesTests
{
    private static readonly DateOnly Incident = new(2026, 10, 8);
    private static readonly DateTimeOffset Reported = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    private const string HashA = "f0f0f0f0f0f0f0f0";

    private static ImageForensics Photo(
        bool hasExif = true,
        DateTimeOffset? captured = null,
        DateTimeOffset? modified = null,
        string? make = "TestCam",
        string? model = "One",
        string? software = null,
        double? lat = null,
        double? lon = null,
        int width = 800,
        int height = 600,
        int? exifWidth = null,
        int? exifHeight = null,
        string? hash = null) =>
        new(hasExif, captured ?? new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero), modified, make, model, software, lat, lon, width, height, exifWidth, exifHeight, hash);

    private static IReadOnlyList<AuthenticityFinding> Run(ImageForensics forensics, DocumentKind? kind = DocumentKind.Evidence, params KnownPicture[] others) =>
        AuthenticityRules.Evaluate(forensics, kind, Incident, Reported, others);

    private static string[] Codes(IEnumerable<AuthenticityFinding> findings) => findings.Select(finding => finding.Code).ToArray();

    [Fact]
    public void ACleanPhotoTakenOnTheDayOfTheIncidentHasNoFindings()
    {
        Assert.Empty(Run(Photo()));
    }

    [Fact]
    public void AMissingMetadataOnEvidenceIsOnlyALowNudge_AndNotRaisedForPaperwork()
    {
        var evidence = Run(Photo(hasExif: false), DocumentKind.Evidence);
        var paperwork = Run(Photo(hasExif: false), DocumentKind.Document);

        Assert.Equal(["no_metadata"], Codes(evidence));
        Assert.Equal(AuthenticitySeverity.Low, evidence[0].Severity);
        Assert.Empty(paperwork);
    }

    [Theory]
    [InlineData(-20, true)]
    [InlineData(-2, true)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    public void APhotoTakenBeforeTheIncidentIsAHighFinding(int daysFromIncident, bool flagged)
    {
        var captured = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero).AddDays(daysFromIncident);

        var findings = Run(Photo(captured: captured));

        Assert.Equal(flagged, Codes(findings).Contains("captured_before_incident"));
        if (flagged)
        {
            Assert.Equal(AuthenticitySeverity.High, findings.Single(finding => finding.Code == "captured_before_incident").Severity);
        }
    }

    [Fact]
    public void APhotoDatedAfterTheClaimWasReportedIsMedium()
    {
        var findings = Run(Photo(captured: Reported.AddDays(4)));

        Assert.Equal(AuthenticitySeverity.Medium, findings.Single(finding => finding.Code == "captured_in_future").Severity);
    }

    [Theory]
    [InlineData("Adobe Photoshop 25.1", true)]
    [InlineData("GIMP 2.10", true)]
    [InlineData("Snapseed", true)]
    [InlineData("Samsung Galaxy S24 firmware", false)]
    [InlineData(null, false)]
    public void KnownEditingSoftwareIsMedium(string? software, bool flagged)
    {
        Assert.Equal(flagged, Codes(Run(Photo(software: software))).Contains("edited_with_software"));
    }

    [Fact]
    public void ChangedLongAfterItWasTakenAndResizedAfterCaptureAreMedium()
    {
        var captured = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

        var changed = Run(Photo(captured: captured, modified: captured.AddDays(5)));
        var sameDay = Run(Photo(captured: captured, modified: captured.AddHours(3)));
        var resized = Run(Photo(width: 800, height: 600, exifWidth: 4000, exifHeight: 3000));
        var untouched = Run(Photo(width: 800, height: 600, exifWidth: 800, exifHeight: 600));

        Assert.Contains("modified_after_capture", Codes(changed));
        Assert.DoesNotContain("modified_after_capture", Codes(sameDay));
        Assert.Contains("size_differs_from_camera", Codes(resized));
        Assert.DoesNotContain("size_differs_from_camera", Codes(untouched));
    }

    [Fact]
    public void ALocationIsOnlyInformation()
    {
        var findings = Run(Photo(lat: 0.3476, lon: 32.5825));

        var finding = Assert.Single(findings);
        Assert.Equal("location_recorded", finding.Code);
        Assert.Equal(AuthenticitySeverity.Info, finding.Severity);
    }

    [Fact]
    public void ASimilarPictureOnAnotherClaimIsHigh_InTheSameClaimInAnotherSlotMedium_InTheSameSlotNothing()
    {
        var otherClaim = new KnownPicture(Guid.NewGuid(), Guid.NewGuid(), "f0f0f0f0f0f0f0f1", IsSameClaim: false, IsSameSlot: false);
        var sameClaimOtherSlot = new KnownPicture(Guid.NewGuid(), Guid.NewGuid(), HashA, IsSameClaim: true, IsSameSlot: false);
        var sameSlot = new KnownPicture(Guid.NewGuid(), Guid.NewGuid(), HashA, IsSameClaim: true, IsSameSlot: true);
        var different = new KnownPicture(Guid.NewGuid(), Guid.NewGuid(), "0f0f0f0f0f0f0f0f", IsSameClaim: false, IsSameSlot: false);

        var findings = Run(Photo(hash: HashA), DocumentKind.Evidence, otherClaim, sameClaimOtherSlot, sameSlot, different);

        Assert.Equal(AuthenticitySeverity.High, findings.Single(finding => finding.Code == "picture_used_on_another_claim").Severity);
        Assert.Equal(otherClaim.CaseId, findings.Single(finding => finding.Code == "picture_used_on_another_claim").RelatedCaseId);
        Assert.Single(findings, finding => finding.Code == "same_picture_in_claim");
        Assert.Equal(sameClaimOtherSlot.DocumentId, findings.Single(finding => finding.Code == "same_picture_in_claim").RelatedDocumentId);
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void FindingsAreOrderedWorstFirst()
    {
        var captured = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        var findings = Run(Photo(captured: captured, software: "GIMP", lat: 1, lon: 1));

        Assert.Equal(AuthenticitySeverity.High, findings[0].Severity);
        Assert.Equal(AuthenticitySeverity.Info, findings[^1].Severity);
    }

    [Theory]
    [InlineData("0000000000000000", "0000000000000000", 0)]
    [InlineData("0000000000000000", "ffffffffffffffff", 64)]
    [InlineData("f0f0f0f0f0f0f0f0", "f0f0f0f0f0f0f0f1", 1)]
    public void TheDistanceBetweenHashesCountsDifferingBits(string a, string b, int expected)
    {
        Assert.Equal(expected, PerceptualHash.Distance(a, b));
    }

    [Theory]
    [InlineData(0UL, false)]
    [InlineData(ulong.MaxValue, false)]
    [InlineData(0x0F0F0F0F0F0F0F0FUL, true)]
    [InlineData(0x1UL, false)]
    public void APictureWithNoDetailHasNoUsableHash(ulong hash, bool usable)
    {
        Assert.Equal(usable, PerceptualHash.HasDetail(hash));
    }

    [Fact]
    public void ADispositionIsNeverMadeByTheRules_AndNeedsLookClearsOnceAPersonHasLooked()
    {
        var report = new AuthenticityReport
        {
            DocumentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Scope = InsuranceIntelligence.Domain.Common.TenantScope.Create("t", "w"),
            Findings = [new AuthenticityFinding("edited_with_software", AuthenticitySeverity.Medium, "r")], AnalysedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(AuthenticityDisposition.NotReviewed, report.Disposition);
        Assert.True(report.NeedsLook);
        Assert.False((report with { Disposition = AuthenticityDisposition.NoConcern }).NeedsLook);
        Assert.False((report with { Findings = [new AuthenticityFinding("location_recorded", AuthenticitySeverity.Info, "r")] }).NeedsLook);
    }
}

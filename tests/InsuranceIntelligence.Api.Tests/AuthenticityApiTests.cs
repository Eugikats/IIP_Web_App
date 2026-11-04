using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageMagick;
using ImageMagick.Drawing;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 16: pictures are checked for camera details, signs of editing and reuse;
/// the result is advice for a person, who gives a disposition that is audited.</summary>
public sealed class AuthenticityApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    /// <summary>A picture with enough detail to hash, made from a seed so two seeds differ and the same
    /// seed at another size is recognisably the same picture.</summary>
    private static MagickImage Picture(int seed, int width = 800, int height = 600)
    {
        var random = new Random(seed);
        var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        for (var i = 0; i < 14; i++)
        {
            var x = random.Next(0, 90) / 100.0 * width;
            var y = random.Next(0, 90) / 100.0 * height;
            var w = random.Next(8, 40) / 100.0 * width;
            var h = random.Next(8, 40) / 100.0 * height;
            var colour = new MagickColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            image.Draw(new DrawableFillColor(colour), new DrawableRectangle(x, y, x + w, y + h));
        }

        return image;
    }

    private static byte[] Jpeg(MagickImage image, Action<ExifProfile>? exif = null, int quality = 90)
    {
        if (exif is not null)
        {
            var profile = new ExifProfile();
            exif(profile);
            image.SetProfile(profile);
        }

        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)quality;
        return image.ToByteArray();
    }

    private static Action<ExifProfile> Camera(DateTime taken, string? software = null, DateTime? modified = null) => profile =>
    {
        profile.SetValue(ExifTag.Make, "TestCam");
        profile.SetValue(ExifTag.Model, "Evidence 1");
        profile.SetValue(ExifTag.DateTimeOriginal, taken.ToString("yyyy:MM:dd HH:mm:ss"));
        if (software is not null)
        {
            profile.SetValue(ExifTag.Software, software);
        }

        if (modified is not null)
        {
            profile.SetValue(ExifTag.DateTime, modified.Value.ToString("yyyy:MM:dd HH:mm:ss"));
        }
    };

    private static DateTime Yesterday => DateTime.UtcNow.Date.AddDays(-1).AddHours(10);

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, byte[] bytes, string contentType = "image/jpeg")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }

    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim, HttpClient Claimant)> ArrangeAsync(string? tenant = null)
    {
        tenant ??= MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);
        return (tenant, staff, claim, claimant);
    }

    private static async Task<JsonElement[]> ReportsAsync(HttpClient staff, Guid caseId) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/authenticity"))).EnumerateArray().ToArray();

    private static string[] Codes(JsonElement report) =>
        report.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("code").GetString()!).ToArray();

    [Fact]
    public async Task AGoodPhotoTakenAfterTheIncidentHasNoFindings()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "IMG_1.jpg", Jpeg(Picture(1), Camera(Yesterday)));

        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Empty(Codes(report));
        Assert.True(report.GetProperty("hasExif").GetBoolean());
        Assert.Equal("TestCam", report.GetProperty("make").GetString());
        Assert.False(report.GetProperty("needsLook").GetBoolean());
        Assert.Equal("NotReviewed", report.GetProperty("disposition").GetString());
    }

    [Fact]
    public async Task APhotoWithNoCameraDetailsIsALowNudge()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "IMG_2.jpg", Jpeg(Picture(2)));

        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Equal(["no_metadata"], Codes(report));
        Assert.Equal("Low", report.GetProperty("highestSeverity").GetString());
        Assert.False(report.GetProperty("needsLook").GetBoolean());
    }

    [Fact]
    public async Task APhotoTakenWeeksBeforeTheIncidentNeedsALook()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "IMG_3.jpg", Jpeg(Picture(3), Camera(DateTime.UtcNow.Date.AddDays(-30).AddHours(11))));

        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Contains("captured_before_incident", Codes(report));
        Assert.True(report.GetProperty("needsLook").GetBoolean());
        Assert.Equal("High", report.GetProperty("highestSeverity").GetString());
        Assert.Contains("days before the incident", report.GetProperty("findings").EnumerateArray().First().GetProperty("reason").GetString());
    }

    [Fact]
    public async Task SignsOfEditingAreReportedWithTheirReasons()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var taken = Yesterday;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "edited.jpg", Jpeg(Picture(4), Camera(taken, software: "Adobe Photoshop 25", modified: taken.AddDays(4))));

        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Contains("edited_with_software", Codes(report));
        Assert.Contains("modified_after_capture", Codes(report));
        Assert.Equal("Adobe Photoshop 25", report.GetProperty("software").GetString());
    }

    [Fact]
    public async Task AResizedPictureDoesNotMatchTheSizeTheCameraRecorded()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "small.jpg", Jpeg(Picture(5, 640, 480), profile =>
        {
            Camera(Yesterday)(profile);
            profile.SetValue(ExifTag.PixelXDimension, new Number(4000u));
            profile.SetValue(ExifTag.PixelYDimension, new Number(3000u));
        }));

        Assert.Contains("size_differs_from_camera", Codes(Assert.Single(await ReportsAsync(staff, claim.CaseId))));
    }

    [Fact]
    public async Task ARecordedLocationIsInformationNotAConcern()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "gps.jpg", Jpeg(Picture(6), profile =>
        {
            Camera(Yesterday)(profile);
            profile.SetValue(ExifTag.GPSLatitudeRef, "N");
            profile.SetValue(ExifTag.GPSLatitude, [new Rational(0, 1), new Rational(20, 1), new Rational(51, 1)]);
            profile.SetValue(ExifTag.GPSLongitudeRef, "E");
            profile.SetValue(ExifTag.GPSLongitude, [new Rational(32, 1), new Rational(34, 1), new Rational(57, 1)]);
        }));

        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Equal(["location_recorded"], Codes(report));
        Assert.True(report.GetProperty("hasGps").GetBoolean());
        Assert.False(report.GetProperty("needsLook").GetBoolean());
    }

    [Fact]
    public async Task ThePictureOfAnotherClaimInTheSameWorkspaceIsFound_EvenResizedAndRecompressed()
    {
        var tenant = MockedApiFactory.NewTenant();
        var first = await ArrangeAsync(tenant);
        var second = await ArrangeAsync(tenant);
        using var _a = first.Staff; using var _b = first.Claimant; using var _c = second.Staff; using var _d = second.Claimant;
        await UploadAsync(first.Claimant, first.Claim.CaseId, "PhotoEvidence", "original.jpg", Jpeg(Picture(7, 1200, 900), Camera(Yesterday)));

        await UploadAsync(second.Claimant, second.Claim.CaseId, "PhotoEvidence", "reused.jpg", Jpeg(Picture(7, 640, 480), Camera(Yesterday), quality: 55));

        var report = Assert.Single(await ReportsAsync(second.Staff, second.Claim.CaseId));
        var finding = report.GetProperty("findings").EnumerateArray().Single(candidate => candidate.GetProperty("code").GetString() == "picture_used_on_another_claim");
        Assert.Equal("High", finding.GetProperty("severity").GetString());
        Assert.Equal($"CLM-{first.Claim.CaseId.ToString("N")[..8].ToUpperInvariant()}", finding.GetProperty("relatedClaimReference").GetString());
        Assert.True(report.GetProperty("needsLook").GetBoolean());
    }

    [Fact]
    public async Task AnotherWorkspacesPicturesAreNeverComparedOrRevealed()
    {
        var first = await ArrangeAsync();
        var second = await ArrangeAsync();
        using var _a = first.Staff; using var _b = first.Claimant; using var _c = second.Staff; using var _d = second.Claimant;
        var bytes = Jpeg(Picture(8), Camera(Yesterday));
        await UploadAsync(first.Claimant, first.Claim.CaseId, "PhotoEvidence", "a.jpg", bytes);

        await UploadAsync(second.Claimant, second.Claim.CaseId, "PhotoEvidence", "a_copy.jpg", bytes);

        Assert.Empty(Codes(Assert.Single(await ReportsAsync(second.Staff, second.Claim.CaseId))));
        Assert.Equal(HttpStatusCode.NotFound, (await second.Staff.GetAsync($"/api/v1/cases/{first.Claim.CaseId}/authenticity")).StatusCode);
    }

    [Fact]
    public async Task TheSamePictureInTwoDocumentsOfOneClaimIsMedium_ButTheSameSlotTwiceIsNot()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var bytes = Jpeg(Picture(9), Camera(Yesterday));

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "photo1.jpg", bytes);
        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "photo2.jpg", bytes);
        Assert.All(await ReportsAsync(staff, claim.CaseId), report => Assert.DoesNotContain("same_picture_in_claim", Codes(report)));

        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "letter.jpg", bytes);

        var reports = await ReportsAsync(staff, claim.CaseId);
        Assert.Single(reports, report => Codes(report).Contains("same_picture_in_claim"));
    }

    [Fact]
    public async Task APlainColourPictureHasNoHashAndIsNeverACopyOfAnother()
    {
        var tenant = MockedApiFactory.NewTenant();
        var first = await ArrangeAsync(tenant);
        var second = await ArrangeAsync(tenant);
        using var _a = first.Staff; using var _b = first.Claimant; using var _c = second.Staff; using var _d = second.Claimant;
        byte[] Flat(MagickColor colour) { using var flat = new MagickImage(colour, 600, 400); return Jpeg(flat, Camera(Yesterday)); }

        await UploadAsync(first.Claimant, first.Claim.CaseId, "PhotoEvidence", "flat1.jpg", Flat(MagickColors.SteelBlue));
        await UploadAsync(second.Claimant, second.Claim.CaseId, "PhotoEvidence", "flat2.jpg", Flat(MagickColors.SteelBlue));

        Assert.Empty(Codes(Assert.Single(await ReportsAsync(second.Staff, second.Claim.CaseId))));
    }

    [Fact]
    public async Task APdfHasNoAuthenticityReport()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "letter.pdf", System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 IIP-MOCK-DOC:PoliceAbstract"), "application/pdf");

        Assert.Empty(await ReportsAsync(staff, claim.CaseId));
    }

    [Fact]
    public async Task ADispositionIsAPersonsReading_NeedsANoteUnlessNoConcern_IsAuditedAndSurvivesReprocessing()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "old.jpg", Jpeg(Picture(10), Camera(DateTime.UtcNow.Date.AddDays(-30).AddHours(9))));
        var report = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        var documentId = report.GetProperty("documentId").GetGuid();
        var url = $"/api/v1/cases/{claim.CaseId}/documents/{documentId}/authenticity/disposition";

        var noNote = await staff.PostAsJsonAsync(url, new { disposition = "NeedsFollowUp" });
        var notReviewed = await staff.PostAsJsonAsync(url, new { disposition = "NotReviewed", note = "x" });
        var nonsense = await staff.PostAsJsonAsync(url, new { disposition = "Guilty", note = "x" });
        var ok = await staff.PostAsJsonAsync(url, new { disposition = "NeedsFollowUp", note = "Ask the customer for the original photo." });

        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notReviewed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var after = await MockedApiFactory.ReadJsonAsync(ok);
        Assert.Equal("NeedsFollowUp", after.GetProperty("disposition").GetString());
        Assert.Equal("test-admin", after.GetProperty("dispositionByUserId").GetString());
        Assert.False(after.GetProperty("needsLook").GetBoolean());

        await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/reprocess", null);
        var again = Assert.Single(await ReportsAsync(staff, claim.CaseId));
        Assert.Equal("NeedsFollowUp", again.GetProperty("disposition").GetString());
        Assert.Contains("captured_before_incident", Codes(again));

        var audit = await staff.GetStringAsync("/api/v1/audit/events");
        Assert.Contains("document.authenticity.analysed", audit);
        Assert.Contains("document.authenticity.disposition", audit);
        Assert.DoesNotContain("Ask the customer for the original photo", audit);
    }

    [Fact]
    public async Task OnlyStaffOfTheWorkspaceWhoMayViewClaimsCanSeeOrJudge_AndTheCustomerSeesNothing()
    {
        var (tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var upload = await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "p.jpg", Jpeg(Picture(11), Camera(Yesterday)));
        var documentId = Assert.Single(await ReportsAsync(staff, claim.CaseId)).GetProperty("documentId").GetGuid();
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var broker = factory.CreateScopedClient(tenant, role: "Broker");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/cases/{claim.CaseId}/authenticity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/authenticity/disposition", new { disposition = "NoConcern" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.GetAsync($"/api/v1/cases/{claim.CaseId}/authenticity")).StatusCode);
        var text = await upload.Content.ReadAsStringAsync();
        Assert.DoesNotContain("authenticity", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("perceptual", text, StringComparison.OrdinalIgnoreCase);
    }
}

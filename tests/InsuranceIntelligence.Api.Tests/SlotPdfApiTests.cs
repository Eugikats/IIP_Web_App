using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ImageMagick;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 12: pictures of paperwork become one PDF, one page each in the order the
/// customer arranged, with every original kept alongside.</summary>
public sealed class SlotPdfApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Stage = "acknowledgement_and_document_request";

    private static readonly object[] ClaimTypes = [new { name = "Own damage", active = true }];

    private static object Doc(string key, string label, bool multiple, string kind = "Document") => new
    {
        documentKey = key,
        customerLabel = label,
        reason = "Needed to assess the claim.",
        isRequired = true,
        requiredAtStageKey = Stage,
        isRegulatoryOrWordingMandated = false,
        claimTypeKeys = Array.Empty<string>(),
        kind,
        allowsMultiplePages = multiple,
        maxFiles = multiple ? 5 : (int?)null,
        acceptedFormats = Array.Empty<string>(),
    };

    /// <summary>A page picture of a distinctive size, so the order in the PDF can be read back.</summary>
    private static byte[] Page(int width, int height, string format = "jpg")
    {
        using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        image.Format = format == "png" ? MagickFormat.Png : MagickFormat.Jpeg;
        return image.ToByteArray();
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, byte[] bytes, string contentType = "image/jpeg")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        return await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
    }

    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim, HttpClient Claimant)> ArrangeAsync(params object[] matrix)
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes, documentMatrix: matrix);
        var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);
        return (tenant, staff, claim, claimant);
    }

    /// <summary>The page sizes (width in points) of a PDF, in page order.</summary>
    private static int[] PageWidths(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"/MediaBox\s*\[\s*0\s+0\s+(\d+(?:\.\d+)?)\s+\d+(?:\.\d+)?\s*\]")
            .Select(match => (int)Math.Round(double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static string SlotPdfUrl(JsonElement documents) =>
        documents.EnumerateArray().First(document => document.GetProperty("slotPdfUrl").ValueKind == JsonValueKind.String).GetProperty("slotPdfUrl").GetString()!;

    [Fact]
    public async Task PicturesOfAPageBecomeOnePdfWithAPagePerPictureInTheOrderSent()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page2.jpg", Page(700, 900));
        var third = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page3.png", Page(800, 1000, "png"), "image/png");

        var row = (await MockedApiFactory.ReadJsonAsync(third)).GetProperty("requiredDocuments").EnumerateArray().Single();
        Assert.Equal(3, row.GetProperty("pageCount").GetInt32());
        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"));
        var pdfResponse = await staff.GetAsync(SlotPdfUrl(documents));
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType!.MediaType);
        var pdf = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.Latin1.GetString(pdf, 0, 5));
        var widths = PageWidths(pdf);
        Assert.Equal(3, widths.Length);
        Assert.True(widths[0] < widths[1] && widths[1] < widths[2], string.Join(",", widths));
        Assert.Equal([1, 2, 3], documents.EnumerateArray().OrderBy(d => d.GetProperty("slotPdfPage").GetInt32()).Select(d => d.GetProperty("slotPdfPage").GetInt32()).ToArray());
        Assert.All(documents.EnumerateArray(), document => Assert.Equal(3, document.GetProperty("slotPdfPageCount").GetInt32()));
    }

    [Fact]
    public async Task EveryOriginalIsKeptAlongsideThePdf()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        var first = Page(600, 800);
        var second = Page(700, 900);

        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", first);
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page2.jpg", second);

        var documents = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray().ToArray();
        Assert.Equal(2, documents.Length);
        Assert.All(documents, document => Assert.False(document.GetProperty("isSuperseded").GetBoolean()));
        var originals = new List<byte[]>();
        foreach (var document in documents)
        {
            originals.Add(await staff.GetByteArrayAsync($"/api/v1/cases/{claim.CaseId}/documents/{document.GetProperty("documentId").GetGuid()}/content"));
        }

        Assert.Contains(originals, bytes => bytes.SequenceEqual(first));
        Assert.Contains(originals, bytes => bytes.SequenceEqual(second));
    }

    [Fact]
    public async Task RearrangingThePagesRebuildsThePdfInTheNewOrder()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page2.jpg", Page(700, 900));
        var added = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page3.jpg", Page(800, 1000));
        var files = (await MockedApiFactory.ReadJsonAsync(added)).GetProperty("requiredDocuments").EnumerateArray().Single().GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(["page1.jpg", "page2.jpg", "page3.jpg"], files.Select(file => file.GetProperty("fileName").GetString()!).ToArray());

        var ids = files.Select(file => file.GetProperty("documentId").GetGuid()).ToArray();
        var reordered = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/reorder", new { documentTypeKey = "PoliceAbstract", documentIds = new[] { ids[2], ids[0], ids[1] } });

        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        var after = (await MockedApiFactory.ReadJsonAsync(reordered)).GetProperty("requiredDocuments").EnumerateArray().Single();
        Assert.Equal(["page3.jpg", "page1.jpg", "page2.jpg"], after.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("fileName").GetString()!).ToArray());
        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"));
        var widths = PageWidths(await staff.GetByteArrayAsync(SlotPdfUrl(documents)));
        Assert.True(widths[0] > widths[1] && widths[1] < widths[2], string.Join(",", widths));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("stranger")]
    public async Task AnOrderThatDoesNotListEveryPageOnceIsRefused(string mistake)
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        var two = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page2.jpg", Page(700, 900));
        var ids = (await MockedApiFactory.ReadJsonAsync(two)).GetProperty("requiredDocuments").EnumerateArray().Single().GetProperty("files").EnumerateArray().Select(file => file.GetProperty("documentId").GetGuid()).ToArray();
        var order = mistake switch
        {
            "missing" => new[] { ids[0] },
            "duplicate" => new[] { ids[0], ids[0] },
            _ => new[] { ids[0], Guid.NewGuid() },
        };

        var response = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/reorder", new { documentTypeKey = "PoliceAbstract", documentIds = order });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovingOnePageKeepsItsFileAndRebuildsThePdfWithoutIt_RemovingTheLastRemovesThePdf()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        var two = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page2.jpg", Page(700, 900));
        var ids = (await MockedApiFactory.ReadJsonAsync(two)).GetProperty("requiredDocuments").EnumerateArray().Single().GetProperty("files").EnumerateArray().Select(file => file.GetProperty("documentId").GetGuid()).ToArray();

        var removed = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/remove", new { documentTypeKey = "PoliceAbstract", documentId = ids[0] });

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var row = (await MockedApiFactory.ReadJsonAsync(removed)).GetProperty("requiredDocuments").EnumerateArray().Single();
        Assert.Equal(1, row.GetProperty("fileCount").GetInt32());
        Assert.Equal(1, row.GetProperty("pageCount").GetInt32());
        var documents = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray().ToArray();
        Assert.Equal(2, documents.Length);
        Assert.Single(documents, document => document.GetProperty("isSuperseded").GetBoolean());

        await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/remove", new { documentTypeKey = "PoliceAbstract", documentId = ids[1] });
        var gone = await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PoliceAbstract/pdf");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task APdfOrAnEvidenceSlotNeverGetsACombinedPdf()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(
            Doc("PoliceAbstract", "Police report", multiple: true),
            Doc("PhotoEvidence", "Photos", multiple: true, kind: "Evidence"));
        using var _s = staff;
        using var _c = claimant;

        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "damage.jpg", Page(900, 600));
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "letter.pdf", Encoding.UTF8.GetBytes("%PDF-1.4 IIP-MOCK-DOC:PoliceAbstract"), "application/pdf");
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page.jpg", Page(600, 800));

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PhotoEvidence/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PoliceAbstract/pdf")).StatusCode);
    }

    [Fact]
    public async Task AFileThatLooksLikeAPictureButIsNotLeavesNoPdfAndStillUploads()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;

        var upload = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "scan.jpg", Encoding.UTF8.GetBytes("not a picture"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(upload)).GetProperty("requiredDocuments").EnumerateArray().Single().GetProperty("pageCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PoliceAbstract/pdf")).StatusCode);
    }

    [Fact]
    public async Task AnotherWorkspaceCannotReadThePdf_AndOnlyStaffWhoViewClaimsCan()
    {
        var (tenant, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var broker = factory.CreateScopedClient(tenant, role: "Broker");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PoliceAbstract/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.GetAsync($"/api/v1/cases/{claim.CaseId}/document-slots/PoliceAbstract/pdf")).StatusCode);
    }

    [Fact]
    public async Task APageCannotBeReorderedOrRemovedByAnotherCustomer()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync(Doc("PoliceAbstract", "Police report", multiple: true));
        using var _s = staff;
        using var _c = claimant;
        var one = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "page1.jpg", Page(600, 800));
        var id = (await MockedApiFactory.ReadJsonAsync(one)).GetProperty("requiredDocuments").EnumerateArray().Single().GetProperty("files").EnumerateArray().Single().GetProperty("documentId").GetGuid();
        var otherTenant = MockedApiFactory.NewTenant();
        using var otherStaff = factory.CreateScopedClient(otherTenant);
        var other = await TestData.CreateCaseAsync(otherStaff, otherTenant);
        using var otherClaimant = await TestData.ClaimantClientAsync(factory, otherStaff, other);

        var reorder = await otherClaimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/reorder", new { documentTypeKey = "PoliceAbstract", documentIds = new[] { id } });
        var remove = await otherClaimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/remove", new { documentTypeKey = "PoliceAbstract", documentId = id });

        Assert.True(reorder.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized, reorder.StatusCode.ToString());
        Assert.True(remove.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized, remove.StatusCode.ToString());
        var documents = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray().ToArray();
        Assert.False(documents.Single().GetProperty("isSuperseded").GetBoolean());
    }
}

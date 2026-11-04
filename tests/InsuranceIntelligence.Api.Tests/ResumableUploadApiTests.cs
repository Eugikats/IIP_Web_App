using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Uploads;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 23: a file sent in pieces survives a dropped connection, goes through exactly
/// the same checks as a normal upload, and is limited to the claimant's own claim.</summary>
public sealed class ResumableUploadApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static byte[] Bytes(int length, int seed = 1)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim, HttpClient Claimant)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        return (tenant, staff, claim, await TestData.ClaimantClientAsync(factory, staff, claim));
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient claimant, Guid caseId, string fileName, long size, string? slot = "PhotoEvidence", string? fingerprint = null, string contentType = "image/jpeg") =>
        claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{caseId}/uploads", new { fileName, contentType, size, documentTypeKey = slot, fingerprint });

    private static async Task<HttpResponseMessage> PutChunkAsync(HttpClient claimant, Guid caseId, Guid uploadId, int index, byte[] data)
    {
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await claimant.PutAsync($"/api/v1/client-portal/claims/{caseId}/uploads/{uploadId}/chunks/{index}", content);
    }

    private static byte[] Piece(byte[] file, int index) =>
        file.Skip(index * ResumableUploadPolicy.ChunkSize).Take(ResumableUploadPolicy.ChunkSize).ToArray();

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient claimant, Guid caseId, Guid uploadId, string? sha256 = null) =>
        claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{caseId}/uploads/{uploadId}/complete", new { sha256 });

    private static async Task<JsonElement> StatusAsync(HttpClient claimant, Guid caseId, Guid uploadId) =>
        await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{caseId}/uploads/{uploadId}"));

    [Fact]
    public async Task APhotoSentInPiecesArrivesIdenticalToTheOneThatWasChosen()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize * 3 + 1234, seed: 7);

        var start = await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_1.jpg", file.Length));
        var uploadId = start.GetProperty("uploadId").GetGuid();
        Assert.Equal(4, start.GetProperty("totalChunks").GetInt32());
        Assert.Equal(ResumableUploadPolicy.ChunkSize, start.GetProperty("chunkSize").GetInt32());
        Assert.Empty(start.GetProperty("received").EnumerateArray());

        // Out of order, as a flaky connection might deliver them.
        foreach (var index in new[] { 2, 0, 3, 1 })
        {
            Assert.Equal(HttpStatusCode.OK, (await PutChunkAsync(claimant, claim.CaseId, uploadId, index, Piece(file, index))).StatusCode);
        }

        var done = await CompleteAsync(claimant, claim.CaseId, uploadId, Sha(file));

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var slot = (await MockedApiFactory.ReadJsonAsync(done)).GetProperty("requiredDocuments").EnumerateArray()
            .Single(row => row.GetProperty("documentTypeKey").GetString() == "PhotoEvidence");
        Assert.Equal(1, slot.GetProperty("fileCount").GetInt32());
        var documents = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray().ToArray();
        var stored = Assert.Single(documents);
        Assert.Equal(Sha(file), stored.GetProperty("sha256").GetString());
        Assert.Equal("IMG_1.jpg", stored.GetProperty("originalFileName").GetString());
    }

    [Fact]
    public async Task AfterADroppedConnectionTheSameFileResumesFromWhatArrived()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize * 2 + 10, seed: 3);
        var fingerprint = "IMG_2.jpg:" + file.Length + ":1700000000000:PhotoEvidence";

        var first = await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_2.jpg", file.Length, fingerprint: fingerprint));
        var uploadId = first.GetProperty("uploadId").GetGuid();
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));
        // The connection drops here. After a reload the browser starts the same file again.

        var second = await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_2.jpg", file.Length, fingerprint: fingerprint));

        Assert.Equal(uploadId, second.GetProperty("uploadId").GetGuid());
        Assert.Equal(new[] { 0 }, second.GetProperty("received").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 1, Piece(file, 1));
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 2, Piece(file, 2));
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(claimant, claim.CaseId, uploadId, Sha(file))).StatusCode);
    }

    [Fact]
    public async Task SendingAPieceAgainIsHarmlessAndCompletingTwiceStoresTheFileOnce()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize + 500, seed: 5);
        var uploadId = (await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_3.jpg", file.Length))).GetProperty("uploadId").GetGuid();

        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 1, Piece(file, 1));
        Assert.Equal(2, (await StatusAsync(claimant, claim.CaseId, uploadId)).GetProperty("received").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(claimant, claim.CaseId, uploadId)).StatusCode);
        // The answer was lost, so the browser asks again.
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(claimant, claim.CaseId, uploadId)).StatusCode);

        Assert.Single((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray());
    }

    [Fact]
    public async Task APieceOfTheWrongSizeIsRefusedAndAnIncompleteFileCannotBeFinished()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize * 2, seed: 9);
        var uploadId = (await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_4.jpg", file.Length))).GetProperty("uploadId").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, new byte[100])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, new byte[ResumableUploadPolicy.ChunkSize + 1])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutChunkAsync(claimant, claim.CaseId, uploadId, 5, Piece(file, 0))).StatusCode);
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));

        var early = await CompleteAsync(claimant, claim.CaseId, uploadId);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal(new[] { 1 }, (await MockedApiFactory.ReadJsonAsync(early)).GetProperty("missing").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray());
    }

    [Fact]
    public async Task AFileThatArrivedChangedIsRefusedAndNothingIsStored()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize + 7, seed: 11);
        var uploadId = (await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_5.jpg", file.Length))).GetProperty("uploadId").GetGuid();
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 1, Piece(file, 1));

        var done = await CompleteAsync(claimant, claim.CaseId, uploadId, Sha(Bytes(file.Length, seed: 99)));

        Assert.Equal(HttpStatusCode.Conflict, done.StatusCode);
        Assert.Contains("damaged", (await MockedApiFactory.ReadJsonAsync(done)).GetProperty("message").GetString());
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Gone, (await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0))).StatusCode);
    }

    [Fact]
    public async Task TheSameRulesApplyAsToANormalUpload()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;

        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(claimant, claim.CaseId, "empty.jpg", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(claimant, claim.CaseId, "huge.jpg", 26L * 1024 * 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(claimant, claim.CaseId, "virus.exe", 1000)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(claimant, claim.CaseId, "a.jpg", 1000, slot: "NotARealSlot")).StatusCode);
        // A one-file slot that takes only PDFs refuses a photo before any bytes are sent.
        var slots = (await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"))).GetProperty("requiredDocuments").EnumerateArray().ToArray();
        var pdfOnly = slots.FirstOrDefault(row => row.TryGetProperty("acceptedFormats", out var formats) && formats.ValueKind == JsonValueKind.Array && formats.GetArrayLength() > 0 && formats.EnumerateArray().All(format => format.GetString() == "pdf"));
        if (pdfOnly.ValueKind == JsonValueKind.Object)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(claimant, claim.CaseId, "a.jpg", 1000, slot: pdfOnly.GetProperty("documentTypeKey").GetString())).StatusCode);
        }
    }

    [Fact]
    public async Task NothingIsAcceptedUntilThePrivacyNoticeIsAgreed()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim, agreeToPrivacyNotice: false);

        var response = await StartAsync(claimant, claim.CaseId, "IMG_6.jpg", 5000);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("consent_required", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnotherClaimantOrWorkspaceCannotSeeAddToOrFinishSomeoneElsesUpload()
    {
        var (tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var file = Bytes(ResumableUploadPolicy.ChunkSize + 1, seed: 13);
        var uploadId = (await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_7.jpg", file.Length))).GetProperty("uploadId").GetGuid();
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, Piece(file, 0));

        // A second claimant in the same workspace, on their own claim, using the first claimant's upload id.
        var otherClaim = await TestData.CreateCaseAsync(staff, tenant);
        using var other = await TestData.ClaimantClientAsync(factory, staff, otherClaim);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}/uploads/{uploadId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/client-portal/claims/{otherClaim.CaseId}/uploads/{uploadId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutChunkAsync(other, otherClaim.CaseId, uploadId, 1, Piece(file, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CompleteAsync(other, otherClaim.CaseId, uploadId)).StatusCode);

        // The owner's upload is untouched.
        Assert.Equal(new[] { 0 }, (await StatusAsync(claimant, claim.CaseId, uploadId)).GetProperty("received").EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task CancellingThrowsTheUploadAwayAndAnUnknownUploadIsNotFound()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        var uploadId = (await MockedApiFactory.ReadJsonAsync(await StartAsync(claimant, claim.CaseId, "IMG_8.jpg", 1000))).GetProperty("uploadId").GetGuid();
        await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, new byte[1000]);

        Assert.Equal(HttpStatusCode.NoContent, (await claimant.DeleteAsync($"/api/v1/client-portal/claims/{claim.CaseId}/uploads/{uploadId}")).StatusCode);

        Assert.Empty((await StatusAsync(claimant, claim.CaseId, uploadId)).GetProperty("received").EnumerateArray());
        Assert.Equal("Cancelled", (await StatusAsync(claimant, claim.CaseId, uploadId)).GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Gone, (await PutChunkAsync(claimant, claim.CaseId, uploadId, 0, new byte[1000])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}/uploads/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ASmallFileStillWorksAsASingleRequestAndNothingElseChanged()
    {
        var (_, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        using var form = TestData.FileForm("police.pdf", "IIP-MOCK-DOC:PoliceAbstract", "PoliceAbstract", "application/pdf");

        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents", form);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}

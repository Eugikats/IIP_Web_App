using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Compliance;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 24: one zip holding the claim summary, the renamed documents, the untouched
/// evidence and a manifest of hashes.</summary>
public sealed class ClaimsPackApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static string Ref(Guid caseId) => $"CLM-{caseId.ToString("N")[..8].ToUpperInvariant()}";

    private static async Task UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<(string Tenant, HttpClient Admin, CaseInfo Claim, HttpClient Claimant)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var admin = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(admin, tenant);
        return (tenant, admin, claim, await TestData.ClaimantClientAsync(factory, admin, claim));
    }

    private static Dictionary<string, byte[]> Entries(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<byte[]> DownloadAsync(HttpClient client, Guid caseId)
    {
        var response = await client.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task ThePackHoldsASummaryRenamedDocumentsUntouchedEvidenceAndAManifestOfHashes()
    {
        var (_, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        var police = Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract The police abstract text.");
        var photo = new byte[4000];
        new Random(5).NextBytes(photo);
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "scan0001.pdf", police, "application/pdf");
        await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "IMG_0042.jpg", photo, "image/jpeg");

        var zip = await DownloadAsync(admin, claim.CaseId);
        var entries = Entries(zip);

        var reference = Ref(claim.CaseId);
        Assert.Contains($"00_ClaimSummary_{reference}.pdf", entries.Keys);
        Assert.Contains("manifest.json", entries.Keys);
        var document = Assert.Single(entries.Keys, key => key.StartsWith("01_Documents/", StringComparison.Ordinal));
        Assert.Equal($"01_Documents/PoliceAbstract_{reference}.pdf", document);
        Assert.Equal(police, entries[document]);
        var evidence = Assert.Single(entries.Keys, key => key.StartsWith("02_Evidence/", StringComparison.Ordinal));
        Assert.Equal(photo, entries[evidence]);

        // The manifest ties every file to its hash, and the hashes are right.
        var manifest = JsonDocument.Parse(entries["manifest.json"]).RootElement;
        Assert.Equal(reference, manifest.GetProperty("claimReference").GetString());
        foreach (var file in manifest.GetProperty("files").EnumerateArray())
        {
            var path = file.GetProperty("path").GetString()!;
            Assert.Equal(Sha(entries[path]), file.GetProperty("sha256").GetString());
            Assert.False(string.IsNullOrWhiteSpace(file.GetProperty("originalName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(file.GetProperty("newName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(file.GetProperty("type").GetString()));
        }

        var photoEntry = manifest.GetProperty("files").EnumerateArray().Single(file => file.GetProperty("path").GetString() == evidence);
        Assert.Equal("IMG_0042.jpg", photoEntry.GetProperty("originalName").GetString());
        Assert.Equal(Sha(photo), photoEntry.GetProperty("sha256").GetString());
        Assert.Equal("Evidence", photoEntry.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task TheSummaryReadsLikeAFileNoteAndLabelsEverythingThePlatformPrepared()
    {
        var (_, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract text"), "application/pdf");
        Assert.True((await admin.PostAsync($"/api/v1/cases/{claim.CaseId}/summary", null)).IsSuccessStatusCode);

        var entries = Entries(await DownloadAsync(admin, claim.CaseId));
        var pdf = entries[$"00_ClaimSummary_{Ref(claim.CaseId)}.pdf"];
        var text = Encoding.ASCII.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains($"Claim summary {Ref(claim.CaseId)}", text);
        Assert.Contains("The customer", text);
        Assert.Contains("Test Customer", text);
        Assert.Contains(claim.PolicyNumber, text);
        Assert.Contains("Documents asked for", text);
        Assert.Contains("Police report", text);
        Assert.Contains(@"required\): Received", text);
        Assert.Contains(@"required\): Missing", text);
        Assert.Contains("What happened, in order", text);
        Assert.Contains("Summary of the claim", text);
        Assert.Contains("Review score and recommendation", text);
        Assert.Contains("Checks on the pictures", text);
        Assert.Contains("Files in this pack", text);
        // Every part the platform prepared carries the advisory label.
        Assert.True(CountOf(text, ClaimsPackBuilder.AdvisoryNotice[..60]) >= 3, "each prepared part is labelled advisory");
        Assert.DoesNotContain('\u2014', Encoding.UTF8.GetString(pdf));
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    [Fact]
    public async Task AReplacedDocumentIsNotInThePackAndTheJsonAuditPackStillWorks()
    {
        var (_, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "first.pdf", Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract first"), "application/pdf");
        var second = Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract second version");
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "second.pdf", second, "application/pdf");

        var entries = Entries(await DownloadAsync(admin, claim.CaseId));

        var document = Assert.Single(entries.Keys, key => key.StartsWith("01_Documents/", StringComparison.Ordinal));
        Assert.Equal(second, entries[document]);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/compliance/cases/{claim.CaseId}/audit-pack")).StatusCode);
    }

    [Fact]
    public async Task EveryDownloadIsAuditedWithWhatItHeldButNotTheContent()
    {
        var (_, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        await DownloadAsync(admin, claim.CaseId);

        var audit = await admin.GetStringAsync("/api/v1/audit/events?pageSize=200");

        Assert.Contains("claims_pack.exported", audit);
        Assert.DoesNotContain("The police abstract text", audit);
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    [InlineData("Client")]
    public async Task OnlyRolesThatMayExportEvidenceCanDownloadAPack(string role)
    {
        var (tenant, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/compliance/cases/{claim.CaseId}/claims-pack")).StatusCode);
    }

    [Theory]
    [InlineData("ClaimsSupervisor")]
    [InlineData("Compliance")]
    public async Task SupervisorsAndComplianceCanDownloadAPack(string role)
    {
        var (tenant, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.NotEmpty(Entries(await DownloadAsync(other, claim.CaseId)));
    }

    [Fact]
    public async Task AnotherWorkspaceCannotDownloadThisClaimsPack()
    {
        var (tenant, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;
        using var intruder = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var otherWorkspace = factory.CreateScopedClient(tenant, workspaceId: "workspace-other");

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/compliance/cases/{claim.CaseId}/claims-pack")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherWorkspace.GetAsync($"/api/v1/compliance/cases/{claim.CaseId}/claims-pack")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/compliance/cases/{Guid.NewGuid()}/claims-pack")).StatusCode);
    }

    [Fact]
    public async Task AClaimWithNothingUploadedStillGivesASummaryAndAManifest()
    {
        var (_, admin, claim, claimant) = await ArrangeAsync();
        using var _a = admin; using var _c = claimant;

        var entries = Entries(await DownloadAsync(admin, claim.CaseId));

        Assert.Equal(2, entries.Count);
        Assert.Contains("manifest.json", entries.Keys);
    }
}

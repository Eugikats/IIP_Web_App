using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ImageMagick;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 11: an upload is stored byte for byte (EXIF included), its SHA-256 is
/// kept, and a separate preview is made for looking at; the original is never replaced.</summary>
public sealed class EvidenceOriginalsApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static byte[] PhotoWithExif(int width = 3000, int height = 2000)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "TestCam");
        exif.SetValue(ExifTag.Model, "Evidence 1");
        exif.SetValue(ExifTag.DateTimeOriginal, "2026:10:08 09:15:00");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        image.Quality = 90;
        return image.ToByteArray();
    }

    private static MultipartFormDataContent Form(string fileName, byte[] bytes, string contentType, string documentTypeKey)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(documentTypeKey), "documentTypeKey");
        return form;
    }

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests", "fixtures", name)))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "fixtures", name);
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private async Task<(HttpClient Staff, HttpClient Claimant, CaseInfo Info, TenantScope Scope)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        var claimant = await TestData.ClaimantClientAsync(factory, staff, info);
        return (staff, claimant, info, TenantScope.Create(tenant, "workspace-main"));
    }

    private async Task<byte[]> StoredOriginalAsync(TenantScope scope, Guid caseId, Guid documentId)
    {
        await using var arrange = factory.Services.CreateAsyncScope();
        var document = (await arrange.ServiceProvider.GetRequiredService<ICaseDocumentRepository>().GetAsync(caseId, documentId, scope))!;
        await using var stream = await arrange.ServiceProvider.GetRequiredService<IDocumentStorageProvider>().ReadAsync(scope, document.ObjectKey);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    private static async Task<JsonElement> OnlyDocumentAsync(HttpClient staff, Guid caseId)
    {
        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/documents"));
        return documents.EnumerateArray().Last();
    }

    [Fact]
    public async Task AnEvidencePhotoIsStoredByteForByteWithItsExifAndItsHashIsKept()
    {
        var (staff, claimant, info, scope) = await ArrangeAsync();
        var original = PhotoWithExif();

        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_1001.jpg", original, "image/jpeg", "PhotoEvidence"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var document = await OnlyDocumentAsync(staff, info.CaseId);
        Assert.Equal(Sha(original), document.GetProperty("sha256").GetString());
        Assert.Equal("Evidence", document.GetProperty("slotKind").GetString());

        var stored = await StoredOriginalAsync(scope, info.CaseId, document.GetProperty("documentId").GetGuid());
        Assert.Equal(original, stored);
        using var reread = new MagickImage(stored);
        var exif = reread.GetExifProfile();
        Assert.NotNull(exif);
        Assert.Equal("TestCam", exif!.GetValue(ExifTag.Make)!.Value);
        Assert.Equal("2026:10:08 09:15:00", exif.GetValue(ExifTag.DateTimeOriginal)!.Value);
        Assert.Equal(3000u, reread.Width);
    }

    [Fact]
    public async Task ASeparateSmallerPreviewIsMadeWithoutLocationOrCameraDetails()
    {
        var (staff, claimant, info, _) = await ArrangeAsync();
        var original = PhotoWithExif();
        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_1002.jpg", original, "image/jpeg", "PhotoEvidence"));
        var document = await OnlyDocumentAsync(staff, info.CaseId);

        var url = document.GetProperty("imagePreviewUrl").GetString()!;
        var preview = await staff.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/jpeg", preview.Content.Headers.ContentType!.MediaType);
        var bytes = await preview.Content.ReadAsByteArrayAsync();
        using var picture = new MagickImage(bytes);
        Assert.True(picture.Width <= 1600 && picture.Height <= 1600);
        Assert.Equal(1600u, picture.Width);
        Assert.Null(picture.GetExifProfile());
        Assert.True(bytes.Length < original.Length * 2);
        Assert.NotEqual(Sha(original), Sha(bytes));

        // The original is still served untouched by the content route.
        var content = await staff.GetByteArrayAsync($"/api/v1/cases/{info.CaseId}/documents/{document.GetProperty("documentId").GetGuid()}/content");
        Assert.Equal(original, content);
    }

    [Fact]
    public async Task AHeicPhotoIsKeptAsSentAndGetsAJpegPreview()
    {
        var (staff, claimant, info, scope) = await ArrangeAsync();
        // A real HEIC file (the encoder is not available to Magick.NET, so the fixture was made
        // once with pillow-heif): 640 by 480.
        var heic = File.ReadAllBytes(FixturePath("damage_photo.heic"));

        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_2001.HEIC", heic, "image/heic", "PhotoEvidence"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var document = await OnlyDocumentAsync(staff, info.CaseId);
        var stored = await StoredOriginalAsync(scope, info.CaseId, document.GetProperty("documentId").GetGuid());
        Assert.Equal(heic, stored);
        Assert.Equal(Sha(heic), document.GetProperty("sha256").GetString());
        var preview = await staff.GetByteArrayAsync(document.GetProperty("imagePreviewUrl").GetString()!);
        using var picture = new MagickImage(preview);
        Assert.Equal(MagickFormat.Jpeg, picture.Format);
        Assert.Equal(640u, picture.Width);
    }

    [Fact]
    public async Task APdfIsStoredAndHashedWithNoPreviewPicture()
    {
        var (staff, claimant, info, _) = await ArrangeAsync();
        var pdf = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 IIP-MOCK-DOC:PoliceAbstract");

        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("police.pdf", pdf, "application/pdf", "PoliceAbstract"));

        var document = await OnlyDocumentAsync(staff, info.CaseId);
        Assert.Equal(Sha(pdf), document.GetProperty("sha256").GetString());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("imagePreviewUrl").ValueKind);
        Assert.Equal("Document", document.GetProperty("slotKind").GetString());
    }

    [Fact]
    public async Task AFileThatIsNotReallyAPictureStillUploadsAndHasNoPreview()
    {
        var (staff, claimant, info, scope) = await ArrangeAsync();
        var broken = System.Text.Encoding.UTF8.GetBytes("this is not a jpeg at all");

        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_9.jpg", broken, "image/jpeg", "PhotoEvidence"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var document = await OnlyDocumentAsync(staff, info.CaseId);
        Assert.Equal(JsonValueKind.Null, document.GetProperty("imagePreviewUrl").ValueKind);
        Assert.Equal(broken, await StoredOriginalAsync(scope, info.CaseId, document.GetProperty("documentId").GetGuid()));
    }

    [Fact]
    public async Task EveryPhotoOfAMultiPhotoSlotKeepsItsOwnHash()
    {
        var (staff, claimant, info, _) = await ArrangeAsync();
        var first = PhotoWithExif(800, 600);
        var second = PhotoWithExif(900, 600);

        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("a.jpg", first, "image/jpeg", "PhotoEvidence"));
        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("b.jpg", second, "image/jpeg", "PhotoEvidence"));

        var documents = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/documents"))).EnumerateArray().ToArray();
        Assert.Equal([Sha(first), Sha(second)], documents.Select(document => document.GetProperty("sha256").GetString()!).OrderBy(sha => sha == Sha(first) ? 0 : 1).ToArray());
    }

    [Fact]
    public async Task AStaffUploadAlsoKeepsTheOriginalAndItsHash()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        var original = PhotoWithExif(1200, 800);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(original);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "files", "staff_photo.jpg");

        var upload = await staff.PostAsync($"/api/v1/cases/{info.CaseId}/documents", form);

        Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());
        var document = await OnlyDocumentAsync(staff, info.CaseId);
        Assert.Equal(Sha(original), document.GetProperty("sha256").GetString());
        Assert.NotEqual(JsonValueKind.Null, document.GetProperty("imagePreviewUrl").ValueKind);
    }

    [Fact]
    public async Task AnotherWorkspaceCannotReadAPreview()
    {
        var (staff, claimant, info, _) = await ArrangeAsync();
        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_3.jpg", PhotoWithExif(800, 600), "image/jpeg", "PhotoEvidence"));
        var document = await OnlyDocumentAsync(staff, info.CaseId);
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await stranger.GetAsync(document.GetProperty("imagePreviewUrl").GetString()!);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Broker")]
    public async Task OnlyStaffWhoMayViewTheClaimCanReadAPreview(string role)
    {
        var (staff, claimant, info, _) = await ArrangeAsync();
        await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_4.jpg", PhotoWithExif(800, 600), "image/jpeg", "PhotoEvidence"));
        var document = await OnlyDocumentAsync(staff, info.CaseId);
        using var other = factory.CreateScopedClient(info.TenantId, role: role);

        var response = await other.GetAsync(document.GetProperty("imagePreviewUrl").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheClaimantsOwnResponseNeverShowsTheHashOrStorageDetails()
    {
        var (_, claimant, info, _) = await ArrangeAsync();

        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", Form("IMG_5.jpg", PhotoWithExif(800, 600), "image/jpeg", "PhotoEvidence"));

        var text = await upload.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sha256", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("derived", text, StringComparison.OrdinalIgnoreCase);
    }
}

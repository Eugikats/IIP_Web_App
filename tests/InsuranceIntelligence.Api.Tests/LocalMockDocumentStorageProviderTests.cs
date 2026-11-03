using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Tests;

public sealed class LocalMockDocumentStorageProviderTests
{
    [Fact]
    public async Task StoreAndRead_RoundTripsContentWithinTheSameTenantWorkspace()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"iip-document-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            var provider = CreateProvider(storageRoot);
            var scope = new TenantScope("tenant-local-test", "workspace-local-test");
            var content = "synthetic test document"u8.ToArray();
            var stored = await provider.StoreAsync(new DocumentStorageRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                scope,
                "claim-form.pdf",
                "application/pdf",
                content.Length,
                new MemoryStream(content)));

            await using var read = await provider.ReadAsync(scope, stored.ObjectKey);
            using var output = new MemoryStream();
            await read.CopyToAsync(output);

            Assert.Equal(content, output.ToArray());
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Read_RejectsKeysFromAnotherTenantOrWorkspace()
    {
        var provider = CreateProvider(Path.Combine(Path.GetTempPath(), $"iip-document-storage-{Guid.NewGuid():N}"));
        var key = "tenant-a/workspace-a/case-a/document.pdf";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            provider.ReadAsync(new TenantScope("tenant-b", "workspace-b"), key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            provider.ReadAsync(new TenantScope("tenant-a", "workspace-a"), "tenant-a/workspace-a/../escape.pdf"));
    }

    private static LocalMockDocumentStorageProvider CreateProvider(string storageRoot)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"] = storageRoot
            })
            .Build();
        return new LocalMockDocumentStorageProvider(new TestWebHostEnvironment(storageRoot), configuration);
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

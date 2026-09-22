using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentQualityServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-quality", "workspace-claims");

    [Fact]
    public async Task GetAccuracyByTypeAsync_RequiresManageProductsPermission()
    {
        var service = new DocumentQualityService(
            Context(),
            new StubPermissionService(allowed: false),
            new StubRepository([]));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetAccuracyByTypeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetAccuracyByTypeAsync_ReturnsTheDomainRulesComputation()
    {
        var examples = Enumerable.Range(0, 5)
            .Select(_ => new DocumentLabelledExample(
                Guid.NewGuid(), Scope, Guid.NewGuid(), Guid.NewGuid(),
                DocumentType.Invoice, 0.7m, DocumentType.Invoice,
                new Dictionary<string, string>(), DocumentLabelSource.Reclassification,
                "user-1", DateTimeOffset.UtcNow, "corr-1"))
            .ToArray();
        var service = new DocumentQualityService(
            Context(),
            new StubPermissionService(allowed: true),
            new StubRepository(examples));

        var result = await service.GetAccuracyByTypeAsync(CancellationToken.None);

        var row = Assert.Single(result);
        Assert.Equal("Invoice", row.DocumentType);
        Assert.Equal(5, row.SampleCount);
        Assert.Equal(1m, row.Accuracy);
        Assert.Equal(DocumentClassificationAccuracy.MinimumSampleCount, row.MinimumSampleCount);
    }

    private static ITenantContextAccessor Context()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "user-1", ["ClaimsSupervisor"]));
        return accessor;
    }

    private sealed class StubRepository(IReadOnlyCollection<DocumentLabelledExample> examples) : IDocumentLabelledExampleRepository
    {
        public Task SaveAsync(DocumentLabelledExample example, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<DocumentLabelledExample>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(examples);
    }

    private sealed class StubPermissionService(bool allowed) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public async Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!await HasPermissionAsync(permission, cancellationToken))
            {
                throw new PermissionDeniedException(permission);
            }
        }
    }
}

using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests.Products;

public sealed class ProductCommandServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    [Fact]
    public async Task CreateDraftAsync_RequiresManageProductsPermission()
    {
        var service = BuildService("author-1", out _, out _, permissions: []);

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup")));
    }

    [Fact]
    public async Task CreateDraftAsync_RejectsBlankName()
    {
        var service = BuildService("author-1", out _, out _);

        var ex = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.CreateDraftAsync(new CreateProductRequest(" ", "Motor", "Initial setup")));

        Assert.Equal("Name", ex.Field);
    }

    [Fact]
    public async Task CreateDraftAsync_StartsAtVersionOneAsDraftWithDefaultStageLibrary()
    {
        var service = BuildService("author-1", out _, out _);

        var product = await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));

        Assert.Equal(1, product.Version);
        Assert.Equal(nameof(ProductStatus.Draft), product.Status);
        Assert.Equal(8, product.Stages.Count);
        Assert.False(product.CanPublish);
    }

    [Fact]
    public async Task CreateDraftAsync_WritesAnAuditEvent()
    {
        var service = BuildService("author-1", out _, out var audit);

        await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));

        var written = Assert.Single(audit.Events);
        Assert.Equal("product.created", written.ActionName);
        Assert.Equal("author-1", written.UserId);
    }

    [Fact]
    public async Task UpdateDraftAsync_IncrementsVersionAndNeverOverwritesTheOldOne()
    {
        var service = BuildService("author-1", out var repository, out _);
        var created = await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));

        var updated = await service.UpdateDraftAsync(
            created.ProductId,
            new UpdateProductRequest(null, null, "IRA/2026/0042", new DateOnly(2026, 1, 1), null, null, null, "Recorded IRA approval reference."));

        Assert.Equal(2, updated.Version);
        Assert.Equal("IRA/2026/0042", updated.IraApprovalReference);
        Assert.Equal(2, repository.SavedVersions.Count(v => v.ProductId == created.ProductId));
        // The original version 1 row must still exist, unmodified.
        var version1 = repository.SavedVersions.Single(v => v.ProductId == created.ProductId && v.Version == 1);
        Assert.Null(version1.IraApprovalReference);
    }

    [Fact]
    public async Task SubmitForPublishApprovalAsync_RejectsAProductWithNoIraApprovalReference()
    {
        var service = BuildService("author-1", out _, out _);
        var created = await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));

        var ex = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.SubmitForPublishApprovalAsync(created.ProductId, new SubmitProductRequest("Ready to publish")));

        Assert.Equal("iraApprovalReference", ex.Field);
        Assert.Contains("cannot be activated without an IRA approval reference", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovePublishAsync_RejectsTheSameUserWhoSubmittedIt()
    {
        var service = BuildService("author-1", out _, out _);
        var created = await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));
        await service.UpdateDraftAsync(
            created.ProductId,
            new UpdateProductRequest(null, null, "IRA/2026/0042", new DateOnly(2026, 1, 1), null, null, null, "Recorded IRA approval reference."));
        var submitted = await service.SubmitForPublishApprovalAsync(created.ProductId, new SubmitProductRequest("Ready to publish"));

        Assert.Equal("author-1", submitted.PublishSubmittedByUserId);

        var ex = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.ApprovePublishAsync(created.ProductId, new ApproveProductPublishRequest("Approved")));

        Assert.Equal("reviewerUserId", ex.Field);
    }

    [Fact]
    public async Task ApprovePublishAsync_WithASecondPerson_PublishesTheProduct()
    {
        var repository = new InMemoryProductRepository();
        var audit = new RecordingAuditWriter();
        var authorService = new ProductCommandService(
            CurrentContext("author-1"), new FixedPermissionService(Permission.ManageProducts), repository, audit);
        var reviewerService = new ProductCommandService(
            CurrentContext("reviewer-1"), new FixedPermissionService(Permission.ManageProducts), repository, audit);

        var created = await authorService.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));
        await authorService.UpdateDraftAsync(
            created.ProductId,
            new UpdateProductRequest(null, null, "IRA/2026/0042", new DateOnly(2026, 1, 1), null, null, null, "Recorded IRA approval reference."));
        await authorService.SubmitForPublishApprovalAsync(created.ProductId, new SubmitProductRequest("Ready to publish"));

        var published = await reviewerService.ApprovePublishAsync(created.ProductId, new ApproveProductPublishRequest("Looks correct"));

        Assert.Equal(nameof(ProductStatus.Published), published.Status);
        Assert.Equal("reviewer-1", published.PublishApprovedByUserId);
    }

    [Fact]
    public async Task RejectPublishAsync_RequiresAReasonAndReturnsToDraft()
    {
        var service = BuildService("author-1", out _, out _);
        var created = await service.CreateDraftAsync(new CreateProductRequest("Motor Comprehensive", "Motor", "Initial setup"));
        await service.UpdateDraftAsync(
            created.ProductId,
            new UpdateProductRequest(null, null, "IRA/2026/0042", new DateOnly(2026, 1, 1), null, null, null, "Recorded IRA approval reference."));
        await service.SubmitForPublishApprovalAsync(created.ProductId, new SubmitProductRequest("Ready to publish"));

        var ex = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.RejectPublishAsync(created.ProductId, new RejectProductPublishRequest(null)));
        Assert.Equal("Reason", ex.Field);

        var rejected = await service.RejectPublishAsync(created.ProductId, new RejectProductPublishRequest("Document matrix is incomplete."));
        Assert.Equal(nameof(ProductStatus.Draft), rejected.Status);
        Assert.Equal("Document matrix is incomplete.", rejected.PublishRejectionReason);
    }

    private static ProductCommandService BuildService(
        string userId,
        out InMemoryProductRepository repository,
        out RecordingAuditWriter audit,
        IReadOnlyList<Permission>? permissions = null)
    {
        repository = new InMemoryProductRepository();
        audit = new RecordingAuditWriter();
        var effectivePermissions = permissions ?? [Permission.ManageProducts];
        return new ProductCommandService(CurrentContext(userId), new FixedPermissionService([.. effectivePermissions]), repository, audit);
    }

    private static TenantContextAccessor CurrentContext(string userId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, userId, []));
        return accessor;
    }

    private sealed class FixedPermissionService(params Permission[] permissions) : IPermissionService
    {
        private readonly HashSet<Permission> _permissions = permissions.ToHashSet();

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(_permissions.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!_permissions.Contains(permission))
            {
                throw new PermissionDeniedException(permission);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        public List<Product> SavedVersions { get; } = [];

        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedVersions
                .Where(version => version.Scope.Matches(scope) && version.ProductId == productId)
                .OrderByDescending(version => version.Version)
                .FirstOrDefault());

        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedVersions
                .FirstOrDefault(v => v.Scope.Matches(scope) && v.ProductId == productId && v.Version == version));

        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(SavedVersions
                .Where(version => version.Scope.Matches(scope) && version.ProductId == productId)
                .OrderByDescending(version => version.Version)
                .ToArray());

        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(SavedVersions
                .Where(version => version.Scope.Matches(scope))
                .GroupBy(version => version.ProductId)
                .Select(group => group.OrderByDescending(version => version.Version).First())
                .ToArray());

        public Task SaveAsync(Product version, CancellationToken cancellationToken = default)
        {
            SavedVersions.Add(version);
            return Task.CompletedTask;
        }
    }
}

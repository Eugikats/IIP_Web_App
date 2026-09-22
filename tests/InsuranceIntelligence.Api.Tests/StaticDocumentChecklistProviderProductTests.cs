using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Tests;

public sealed class StaticDocumentChecklistProviderProductTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task BuildChecklistAsync_WithNoProductRepository_UsesTheBuiltInClaimTypeRules()
    {
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService());

        var checklist = await provider.BuildChecklistAsync(MotorClaim(productId: null, version: null), []);

        Assert.Contains(DocumentType.ClaimForm, checklist.RequiredDocuments);
    }

    [Fact]
    public async Task BuildChecklistAsync_WithAPinnedProductAndAMatrix_UsesTheProductsOwnDocumentMatrix()
    {
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("MedicalReport", "Medical report", "Required for injury assessment", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);

        var checklist = await provider.BuildChecklistAsync(MotorClaim(product.ProductId, product.Version), []);

        Assert.Single(checklist.RequiredDocuments);
        Assert.Contains(DocumentType.MedicalReport, checklist.RequiredDocuments);
        // Not the built-in Motor rule set, which would include PoliceAbstract instead.
        Assert.DoesNotContain(DocumentType.PoliceAbstract, checklist.RequiredDocuments);
    }

    [Fact]
    public async Task BuildChecklistAsync_WithAPinnedProductButNoMatrix_FallsBackToBuiltInRules()
    {
        var repository = new FakeProductRepository();
        var product = BaseProduct([]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);

        var checklist = await provider.BuildChecklistAsync(MotorClaim(product.ProductId, product.Version), []);

        Assert.Contains(DocumentType.ClaimForm, checklist.RequiredDocuments);
        Assert.Contains(DocumentType.PoliceAbstract, checklist.RequiredDocuments);
    }

    [Fact]
    public async Task BuildChecklistAsync_WithAProductIdThatNoLongerResolves_FallsBackToBuiltInRules()
    {
        var repository = new FakeProductRepository();
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);

        var checklist = await provider.BuildChecklistAsync(MotorClaim(Guid.NewGuid(), 1), []);

        Assert.Contains(DocumentType.ClaimForm, checklist.RequiredDocuments);
    }

    private static ClaimCase MotorClaim(Guid? productId, int? version) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ProductId = productId,
            ProductVersion = version,
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision.",
            Status = CaseStatus.New,
            CompletenessScore = 0,
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-checklist-product-tests",
        };

    private static Product BaseProduct(IReadOnlyList<ProductDocumentRequirement> documentMatrix) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = "Motor Comprehensive",
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = documentMatrix,
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Test setup.",
        };

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> versions = [];

        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions
                .Where(v => v.Scope.Matches(scope) && v.ProductId == productId)
                .OrderByDescending(v => v.Version)
                .FirstOrDefault());

        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions.FirstOrDefault(v => v.Scope.Matches(scope) && v.ProductId == productId && v.Version == version));

        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions.Where(v => v.Scope.Matches(scope) && v.ProductId == productId).ToArray());

        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions.Where(v => v.Scope.Matches(scope)).ToArray());

        public Task SaveAsync(Product version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
    }
}

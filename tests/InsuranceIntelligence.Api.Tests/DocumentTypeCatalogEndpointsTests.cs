using System.Text.Json;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentTypeCatalogEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task SeedDefaults_CreatesTheEightBuiltInTypes_AndIsIdempotent()
    {
        var repository = new InMemoryDocumentTypeCatalogRepository();
        var audit = new InMemoryAuditRepository();

        var first = await DocumentTypeCatalogEndpoints.SeedDefaults(repository, audit, ScopedContext(), CancellationToken.None);
        var (_, firstBody) = await ExecuteAsync<JsonElement>(first);

        var second = await DocumentTypeCatalogEndpoints.SeedDefaults(repository, audit, ScopedContext(), CancellationToken.None);
        var (_, secondBody) = await ExecuteAsync<JsonElement>(second);

        Assert.Equal(DefaultDocumentTypeCatalog.Defaults().Count, firstBody.GetProperty("seeded").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("seeded").GetInt32());
    }

    [Fact]
    public async Task SaveVersion_ForANewCustomKey_MarksItWithNoBuiltInType()
    {
        var repository = new InMemoryDocumentTypeCatalogRepository();
        var audit = new InMemoryAuditRepository();

        var result = await DocumentTypeCatalogEndpoints.SaveVersion(
            "SchoolAttendanceLetter",
            new SaveDocumentTypeDefinitionRequest("School attendance letter", "Confirms the student was enrolled.", true),
            repository,
            audit,
            ScopedContext(auditReason: "New type for school fees cover."),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<DocumentTypeDefinitionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(1, body!.Version);
        Assert.Null(body.BuiltInType);
        Assert.Single(await audit.ListAsync(Scope));
    }

    [Fact]
    public async Task SaveVersion_ForAKeyThatMatchesABuiltInEnumName_RecordsWhichOne()
    {
        var repository = new InMemoryDocumentTypeCatalogRepository();
        var audit = new InMemoryAuditRepository();

        var result = await DocumentTypeCatalogEndpoints.SaveVersion(
            "ClaimForm",
            new SaveDocumentTypeDefinitionRequest("Claim form", "The completed claim form for this loss.", true),
            repository,
            audit,
            ScopedContext(auditReason: "Reworded the description."),
            CancellationToken.None);
        var (_, body) = await ExecuteAsync<DocumentTypeDefinitionResponse>(result);

        Assert.Equal(nameof(DocumentType.ClaimForm), body!.BuiltInType);
    }

    [Fact]
    public async Task SaveVersion_IncrementsVersionAndKeepsHistory()
    {
        var repository = new InMemoryDocumentTypeCatalogRepository();
        var audit = new InMemoryAuditRepository();

        await DocumentTypeCatalogEndpoints.SaveVersion(
            "SchoolAttendanceLetter",
            new SaveDocumentTypeDefinitionRequest("School letter", "Original wording.", true),
            repository, audit, ScopedContext(auditReason: "Created."), CancellationToken.None);
        await DocumentTypeCatalogEndpoints.SaveVersion(
            "SchoolAttendanceLetter",
            new SaveDocumentTypeDefinitionRequest("School attendance letter", "Clarified wording.", true),
            repository, audit, ScopedContext(auditReason: "Clarified."), CancellationToken.None);

        var versions = await repository.ListVersionsAsync(Scope, "SchoolAttendanceLetter");
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task ListUnresolvedProductDocumentKeys_FlagsADeliberatelyMisspeltKey_AndIgnoresAKnownOne()
    {
        var catalog = new InMemoryDocumentTypeCatalogRepository();
        var products = new InMemoryProductRepository();
        var goodProduct = BaseProduct("Motor Comprehensive", [new ProductDocumentRequirement("ClaimForm", "Claim form", "Always needed", true, "intimation_and_reference", false)]);
        var badProduct = BaseProduct("Medical Cover", [new ProductDocumentRequirement("totally-unknown-key", "Something", "Reason", true, "intimation_and_reference", false)]);
        await products.SaveAsync(goodProduct);
        await products.SaveAsync(badProduct);

        var result = await DocumentTypeCatalogEndpoints.ListUnresolvedProductDocumentKeys(products, catalog, ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<UnresolvedProductDocumentKeysResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var flagged = Assert.Single(body!);
        Assert.Equal(badProduct.ProductId, flagged.ProductId);
        Assert.Contains("totally-unknown-key", flagged.UnresolvedDocumentKeys);
    }

    [Fact]
    public async Task ListUnresolvedProductDocumentKeys_ACatalogedCustomKey_IsNotFlagged()
    {
        var catalog = new InMemoryDocumentTypeCatalogRepository();
        await catalog.SaveAsync(new DocumentTypeDefinition
        {
            DefinitionId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Key = "SchoolAttendanceLetter",
            CustomerLabel = "School attendance letter",
            Description = "Confirms enrollment.",
            BuiltInType = null,
            Active = true,
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Added.",
        });
        var products = new InMemoryProductRepository();
        var product = BaseProduct("Education Cover", [new ProductDocumentRequirement("SchoolAttendanceLetter", "School letter", "Reason", true, "intimation_and_reference", false)]);
        await products.SaveAsync(product);

        var result = await DocumentTypeCatalogEndpoints.ListUnresolvedProductDocumentKeys(products, catalog, ScopedContext(), CancellationToken.None);
        var (_, body) = await ExecuteAsync<UnresolvedProductDocumentKeysResponse[]>(result);

        Assert.Empty(body!);
    }

    private static Product BaseProduct(string name, IReadOnlyList<ProductDocumentRequirement> documentMatrix) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = name,
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = documentMatrix,
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Test setup.",
        };

    private static DefaultHttpContext ScopedContext(string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-document-type-catalog" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-document-type-catalog";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }

        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private sealed class InMemoryDocumentTypeCatalogRepository : IDocumentTypeCatalogRepository
    {
        private readonly List<DocumentTypeDefinition> versions = [];

        public Task<DocumentTypeDefinition?> GetCurrentAsync(TenantScope scope, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Version)
                .FirstOrDefault());

        public Task<IReadOnlyCollection<DocumentTypeDefinition>> ListCurrentAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DocumentTypeDefinition>>(versions
                .Where(v => v.Scope.Matches(scope))
                .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(v => v.Version).First())
                .ToArray());

        public Task<IReadOnlyCollection<DocumentTypeDefinition>> ListVersionsAsync(TenantScope scope, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DocumentTypeDefinition>>(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))
                .ToArray());

        public Task SaveAsync(DocumentTypeDefinition version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProductRepository : IProductRepository
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
            Task.FromResult<IReadOnlyCollection<Product>>(versions
                .Where(v => v.Scope.Matches(scope))
                .GroupBy(v => v.ProductId)
                .Select(group => group.OrderByDescending(v => v.Version).First())
                .ToArray());

        public Task SaveAsync(Product version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
    }
}

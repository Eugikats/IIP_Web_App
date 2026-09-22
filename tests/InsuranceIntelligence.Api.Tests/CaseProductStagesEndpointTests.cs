using System.Text.Json;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CaseProductStagesEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task GetProductStages_ClaimWithNoPinnedProduct_ReturnsHonestlyNotConfigured()
    {
        var caseRepository = new InMemoryCaseRepository();
        var claim = Claim(productId: null, productVersion: null);
        await caseRepository.SaveAsync(claim);

        var result = await CaseEndpoints.GetProductStages(claim.CaseId, caseRepository, new InMemoryProductRepository(), ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<ClaimProcessResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.False(body!.Configured);
        Assert.Empty(body.Stages);
    }

    [Fact]
    public async Task GetProductStages_ClaimWithAPinnedProduct_ReturnsItsOrderedStages()
    {
        var caseRepository = new InMemoryCaseRepository();
        var products = new InMemoryProductRepository();
        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 2,
            Name = "Motor Comprehensive",
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages =
            [
                new ClaimStageDefinition("offer", "Offer", "Claims Officer", false, 2, 5),
                new ClaimStageDefinition("intimation", "Intimation", "Claims Officer", true, 1, null),
            ],
            DocumentMatrix = [],
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Test setup.",
        };
        await products.SaveAsync(product);
        var claim = Claim(product.ProductId, product.Version);
        await caseRepository.SaveAsync(claim);

        var result = await CaseEndpoints.GetProductStages(claim.CaseId, caseRepository, products, ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<ClaimProcessResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.True(body!.Configured);
        Assert.Equal("Motor Comprehensive", body.ProductName);
        Assert.Equal(2, body.Stages.Count);
        Assert.Equal("intimation", body.Stages.First().StageKey); // sorted by SortOrder, not insertion order
    }

    [Fact]
    public async Task GetProductStages_UnknownCase_ReturnsNotFound()
    {
        var result = await CaseEndpoints.GetProductStages(Guid.NewGuid(), new InMemoryCaseRepository(), new InMemoryProductRepository(), ScopedContext(), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    private static ClaimCase Claim(Guid? productId, int? productVersion) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ProductId = productId,
            ProductVersion = productVersion,
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision.",
            Status = CaseStatus.Assigned,
            CompletenessScore = 0m,
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-product-stages-tests",
        };

    private static DefaultHttpContext ScopedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-product-stages" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> versions = [];

        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions.Where(v => v.Scope.Matches(scope) && v.ProductId == productId).OrderByDescending(v => v.Version).FirstOrDefault());

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

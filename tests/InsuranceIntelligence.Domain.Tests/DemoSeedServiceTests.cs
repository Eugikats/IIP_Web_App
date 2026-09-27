using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Distribution;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Exercises the full seed run end-to-end against in-memory fakes. This exists because the
/// service was twice caught only by a live Azure SQL run (a string primary key that had to
/// be a real GUID under the hood; Math.Round called with a negative digit count, which the
/// decimal overload does not support), both would have failed fast here instead.
/// </summary>
public sealed class DemoSeedServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-test", "workspace-test");

    [Fact]
    public async Task SeedTestPolicyholder_IsRerunnable_AndUsesGuidIdsTheSqlRepositoriesRequire()
    {
        var caseRepository = new InMemoryCaseRepository();
        var customerRepository = new InMemoryCustomerRepository();
        var policyRepository = new InMemoryPolicyRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var service = new DemoSeedService(
            caseRepository,
            documentRepository,
            new InMemoryApprovalRepository(),
            new InMemoryAuditRepository(),
            customerRepository,
            policyRepository,
            new InMemoryCoverageRepository(),
            new InMemoryBranchRepository(),
            new InMemoryProducerRepository(),
            new InMemoryProductRepository());

        var first = await service.SeedTestPolicyholderAsync(Scope, "Test Person", "+256 700 123456", null, "system:test-seeder", "officer-1", CancellationToken.None);
        var second = await service.SeedTestPolicyholderAsync(Scope, "Test Person", "+256700123456", null, "system:test-seeder", "officer-1", CancellationToken.None);

        Assert.Equal(first.Policies.Select(p => p.PolicyNumber), second.Policies.Select(p => p.PolicyNumber));
        Assert.Equal(["Motor", "Medical"], first.Policies.Select(p => p.ProductLine));
        Assert.Equal(["Settled", "Closed"], first.PastClaims.Select(c => c.Status));

        var customers = await customerRepository.ListAsync(Scope, CancellationToken.None);
        var customer = Assert.Single(customers); // rerun updated, did not duplicate
        Assert.True(Guid.TryParse(customer.CustomerId, out _));
        Assert.True(PhoneNumbers.SameNumber("256700123456", customer.Phone));

        var policies = await policyRepository.ListByCustomerAsync(customer.CustomerId, Scope, CancellationToken.None);
        Assert.Equal(2, policies.Count);
        Assert.All(policies, policy => Assert.True(Guid.TryParse(policy.PolicyId, out _)));
        Assert.All(policies, policy => Assert.True(policy.EffectiveTo >= DateOnly.FromDateTime(DateTime.UtcNow)));

        var cases = await caseRepository.ListAsync(Scope, CancellationToken.None);
        Assert.Equal(2, cases.Count);
        Assert.All(cases, claimCase => Assert.False(ClaimIsOpen(claimCase.Status)));
        var documents = await documentRepository.ListForCaseAsync(first.PastClaims[0].CaseId, Scope, CancellationToken.None);
        Assert.Equal(documents.Count, documents.Select(d => d.DocumentId).Distinct().Count());
    }

    private static bool ClaimIsOpen(CaseStatus status) => status is not (CaseStatus.Settled or CaseStatus.Closed);

    [Fact]
    public async Task SeedAsync_ProducesFiftyCasesAcrossEveryProductLineAndStatus()
    {
        var caseRepository = new InMemoryCaseRepository();
        var customerRepository = new InMemoryCustomerRepository();
        var policyRepository = new InMemoryPolicyRepository();
        var coverageRepository = new InMemoryCoverageRepository();
        var branchRepository = new InMemoryBranchRepository();
        var producerRepository = new InMemoryProducerRepository();
        var productRepository = new InMemoryProductRepository();

        var service = new DemoSeedService(
            caseRepository,
            new InMemoryCaseDocumentRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryAuditRepository(),
            customerRepository,
            policyRepository,
            coverageRepository,
            branchRepository,
            producerRepository,
            productRepository);

        var result = await service.SeedAsync(Scope, "system:test-seeder");

        Assert.Equal(50, result.Cases);
        Assert.Equal(50, result.Customers);
        Assert.Equal(50, result.Policies);
        Assert.True(result.Documents > 0);
        Assert.True(result.Approvals > 0);
        Assert.True(result.AuditEvents >= result.Cases);
        Assert.Equal(3, result.HeroCaseIds.Count);
        // One published product per line (claim intake build prompt gap: seeded claims used to
        // have no pinned product at all, so every claim showed "no configured claim process").
        Assert.Equal(5, result.Products);

        var cases = await caseRepository.ListAsync(Scope);
        Assert.Equal(50, cases.Count);

        var productLines = cases.Select(c => c.ProductLine).Distinct().ToArray();
        Assert.Equal(
            new[] { "Motor", "Medical", "Life", "General", "Agriculture" }.OrderBy(x => x),
            productLines.OrderBy(x => x));

        // Every seeded claim is pinned to a real, published product with a real claim process and
        // document matrix, not left to fall back on the built-in claim-type rules invisibly.
        Assert.All(cases, claimCase => Assert.NotNull(claimCase.ProductId));
        Assert.All(cases, claimCase => Assert.NotNull(claimCase.ProductVersion));
        var products = await productRepository.ListCurrentForAllProductsAsync(Scope, default);
        Assert.Equal(5, products.Count);
        Assert.All(products, product =>
        {
            Assert.Equal(ProductStatus.Published, product.Status);
            Assert.NotEmpty(product.Stages);
            Assert.NotEmpty(product.DocumentMatrix);
            Assert.NotEqual(product.PublishSubmittedByUserId, product.PublishApprovedByUserId);
        });
        foreach (var claimCase in cases)
        {
            var pinnedProduct = await productRepository.GetVersionAsync(Scope, claimCase.ProductId!.Value, claimCase.ProductVersion!.Value, default);
            Assert.NotNull(pinnedProduct);
            Assert.Equal(claimCase.ProductLine, pinnedProduct!.ProductClass);
        }

        var statuses = cases.Select(c => c.Status).Distinct().ToArray();
        foreach (var status in Enum.GetValues<CaseStatus>())
        {
            Assert.Contains(status, statuses);
        }

        // The primary-key fields on these five entities are Guid.Parse()'d by the real EF
        // repositories despite being typed as string in the domain contract, every id must
        // therefore be a real GUID, not a human-readable code.
        foreach (var customer in await customerRepository.ListAsync(Scope, default))
        {
            Assert.True(Guid.TryParse(customer.CustomerId, out _), $"CustomerId '{customer.CustomerId}' is not a GUID.");
        }

        var policies = await policyRepository.ListAsync(Scope, default);
        foreach (var policy in policies)
        {
            Assert.True(Guid.TryParse(policy.PolicyId, out _), $"PolicyId '{policy.PolicyId}' is not a GUID.");
            Assert.NotNull(policy.AnnualPremium);
            Assert.True(policy.AnnualPremium > 0);
            Assert.Equal("UGX", policy.PremiumCurrency);

            var coveragesForPolicy = await coverageRepository.ListForPolicyAsync(policy.PolicyId, Scope, default);
            Assert.Single(coveragesForPolicy);
            Assert.True(Guid.TryParse(coveragesForPolicy.Single().CoverageId, out _));
        }

        foreach (var branch in await branchRepository.ListAsync(Scope, default))
        {
            Assert.True(Guid.TryParse(branch.BranchId, out _), $"BranchId '{branch.BranchId}' is not a GUID.");
        }

        foreach (var producer in await producerRepository.ListAsync(Scope, default))
        {
            Assert.True(Guid.TryParse(producer.ProducerId, out _), $"ProducerId '{producer.ProducerId}' is not a GUID.");
        }
    }

    [Fact]
    public async Task SeedAsync_IsDeterministicAcrossRuns()
    {
        var firstResult = await RunSeed();
        var secondResult = await RunSeed();

        Assert.Equal(firstResult.Cases, secondResult.Cases);
        Assert.Equal(firstResult.Documents, secondResult.Documents);
        Assert.Equal(firstResult.Approvals, secondResult.Approvals);

        static Task<DemoSeedResult> RunSeed()
        {
            var service = new DemoSeedService(
                new InMemoryCaseRepository(),
                new InMemoryCaseDocumentRepository(),
                new InMemoryApprovalRepository(),
                new InMemoryAuditRepository(),
                new InMemoryCustomerRepository(),
                new InMemoryPolicyRepository(),
                new InMemoryCoverageRepository(),
                new InMemoryBranchRepository(),
                new InMemoryProducerRepository(),
                new InMemoryProductRepository());

            return service.SeedAsync(Scope, "system:test-seeder");
        }
    }

    [Fact]
    public async Task SeedAsync_RunTwice_NeverDuplicatesTheProducts()
    {
        var productRepository = new InMemoryProductRepository();
        var service = new DemoSeedService(
            new InMemoryCaseRepository(),
            new InMemoryCaseDocumentRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryAuditRepository(),
            new InMemoryCustomerRepository(),
            new InMemoryPolicyRepository(),
            new InMemoryCoverageRepository(),
            new InMemoryBranchRepository(),
            new InMemoryProducerRepository(),
            productRepository);

        var first = await service.SeedAsync(Scope, "system:test-seeder");
        var second = await service.SeedAsync(Scope, "system:test-seeder");

        Assert.Equal(5, first.Products); // all five newly created on the first run
        Assert.Equal(0, second.Products); // reused, not duplicated, on the second run
        var products = await productRepository.ListCurrentForAllProductsAsync(Scope, default);
        Assert.Equal(5, products.Count);
    }
}

// Minimal in-memory fakes for the five string-keyed repositories DemoSeedService depends on.
// These mirror the real EF repositories' scoping semantics but skip the Guid.Parse() the real
// implementations perform on the id, the GUID-format assertions above are what actually
// guard against that mismatch reappearing.

file sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<string, Customer> customers = new();

    public Task<Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(customers.TryGetValue(customerId, out var c) ? c : null);

    public Task<IReadOnlyCollection<Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Customer>>(
            customers.Values.Where(c => c.TenantId == scope.TenantId && c.WorkspaceId == scope.WorkspaceId).ToArray());

    public Task UpsertAsync(Customer customer, CancellationToken cancellationToken)
    {
        customers[customer.CustomerId] = customer;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryPolicyRepository : IPolicyRepository
{
    private readonly Dictionary<string, Policy> policies = new();

    public Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(policies.TryGetValue(policyId, out var p) ? p : null);

    public Task<Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(policies.Values.FirstOrDefault(p =>
            p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId
            && string.Equals(p.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Policy>>(
            policies.Values.Where(p => p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId).ToArray());

    public Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Policy>>(policies.Values.Where(p => p.CustomerId == customerId).ToArray());

    public Task UpsertAsync(Policy policy, CancellationToken cancellationToken)
    {
        policies[policy.PolicyId] = policy;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryCoverageRepository : ICoverageRepository
{
    private readonly Dictionary<string, Coverage> coverages = new();

    public Task<Coverage?> GetAsync(string coverageId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(coverages.TryGetValue(coverageId, out var c) ? c : null);

    public Task<IReadOnlyCollection<Coverage>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Coverage>>(coverages.Values.Where(c => c.PolicyId == policyId).ToArray());

    public Task UpsertAsync(Coverage coverage, CancellationToken cancellationToken)
    {
        coverages[coverage.CoverageId] = coverage;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryBranchRepository : IBranchRepository
{
    private readonly Dictionary<string, Branch> branches = new();

    public Task<Branch?> GetAsync(string branchId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(branches.TryGetValue(branchId, out var b) ? b : null);

    public Task<IReadOnlyCollection<Branch>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Branch>>(
            branches.Values.Where(b => b.TenantId == scope.TenantId && b.WorkspaceId == scope.WorkspaceId).ToArray());

    public Task UpsertAsync(Branch branch, CancellationToken cancellationToken)
    {
        branches[branch.BranchId] = branch;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryProducerRepository : IProducerRepository
{
    private readonly Dictionary<string, Producer> producers = new();

    public Task<Producer?> GetAsync(string producerId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(producers.TryGetValue(producerId, out var p) ? p : null);

    public Task<IReadOnlyCollection<Producer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Producer>>(
            producers.Values.Where(p => p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId).ToArray());

    public Task<IReadOnlyCollection<Producer>> ListByBranchAsync(string branchId, TenantScope scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Producer>>(producers.Values.Where(p => p.BranchId == branchId).ToArray());

    public Task UpsertAsync(Producer producer, CancellationToken cancellationToken)
    {
        producers[producer.ProducerId] = producer;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryProductRepository : IProductRepository
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

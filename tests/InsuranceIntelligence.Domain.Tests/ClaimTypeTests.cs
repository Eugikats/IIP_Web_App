using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 3: claim types per product.</summary>
public sealed class ClaimTypeTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    private static readonly IReadOnlyList<ClaimTypeDefinition> MotorTypes =
    [
        new("own-damage", "Own damage", "Damage to your own vehicle in a collision.", true, 0, ["accident", "collision"]),
        new("theft", "Theft", "The vehicle or its parts were stolen.", true, 1, ["stolen", "stole", "break-in"]),
        new("windscreen", "Windscreen", "A cracked or broken windscreen or window.", true, 2, ["glass"]),
        new("retired-type", "Hail damage", "No longer offered.", false, 3),
    ];

    [Theory]
    [InlineData("Own damage", "own-damage")]
    [InlineData("own-damage", "own-damage")]
    [InlineData("OWN DAMAGE", "own-damage")]
    [InlineData("OwnDamage", "own-damage")]
    [InlineData("Accident", "own-damage")]
    [InlineData("  stolen ", "theft")]
    [InlineData("glass", "windscreen")]
    public void Find_MatchesKeyNameAndAlias_IgnoringCaseAndPunctuation(string text, string expectedKey) =>
        Assert.Equal(expectedKey, ClaimTypeMatching.Find(MotorTypes, text)?.Key);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("banana")]
    public void Find_ReturnsNull_ForNothingOrUnknownText(string? text) =>
        Assert.Null(ClaimTypeMatching.Find(MotorTypes, text));

    [Fact]
    public void Find_StillResolvesAnInactiveType_SoOldClaimsKeepTheirMeaning() =>
        Assert.Equal("retired-type", ClaimTypeMatching.Find(MotorTypes, "Hail damage")?.Key);

    [Theory]
    [InlineData("Someone broke into my car and stole the radio", "theft")]
    [InlineData("I hit a wall while reversing, there was a collision", "own-damage")]
    [InlineData("A stone cracked my windscreen on the highway", "windscreen")]
    public void Suggest_PicksTheBestMatchFromTheText(string text, string expectedKey) =>
        Assert.Equal(expectedKey, ClaimTypeMatching.Suggest(MotorTypes.Where(type => type.Active), text)?.Key);

    [Fact]
    public void Suggest_NeverPicksAnInactiveType() =>
        Assert.Null(ClaimTypeMatching.Suggest(MotorTypes.Where(type => type.Active), "hail damage on the roof"));

    [Fact]
    public void Suggest_ReturnsNull_WhenTwoTypesTie()
    {
        var types = new ClaimTypeDefinition[]
        {
            new("a", "Fire", "", true, 0, ["blaze"]),
            new("b", "Flood", "", true, 1, ["blaze"]),
        };

        Assert.Null(ClaimTypeMatching.Suggest(types, "a blaze"));
    }

    [Fact]
    public void ActiveClaimTypes_AreOrderedBySortOrder_AndExcludeInactive()
    {
        var product = Product(types: MotorTypes.Reverse().ToArray());

        Assert.Equal(new[] { "own-damage", "theft", "windscreen" }, product.ActiveClaimTypes.Select(type => type.Key));
    }

    [Fact]
    public async Task Intake_FromAForm_AcceptsAConfiguredClaimType_AndStoresItsKey()
    {
        var (service, _) = await ServiceWithProductAsync();

        var claim = await service.CreateCaseAsync(Command("Stolen", form: true));

        Assert.Equal("Theft", claim.ClaimType);
        Assert.Equal("theft", claim.ClaimTypeKey);
    }

    [Fact]
    public async Task Intake_FromAForm_RefusesATypeTheProductDoesNotOffer_NamingTheAllowedOnes()
    {
        var (service, _) = await ServiceWithProductAsync();

        var error = await Assert.ThrowsAsync<ClaimTypeNotConfiguredException>(() => service.CreateCaseAsync(Command("Banana", form: true)));

        Assert.Contains("Own damage", error.Message);
        Assert.Contains("Theft", error.Message);
        Assert.DoesNotContain("Hail damage", error.Message);
    }

    [Fact]
    public async Task Intake_FromAForm_RefusesAnInactiveType()
    {
        var (service, _) = await ServiceWithProductAsync();

        // Resolves (so history keeps its meaning) but a person cannot file a new claim under it.
        var claim = await Record.ExceptionAsync(() => service.CreateCaseAsync(Command("Hail damage", form: true)));

        Assert.IsType<ClaimTypeNotConfiguredException>(claim);
    }

    [Fact]
    public async Task Intake_FromAnAutomaticChannel_SuggestsAType_FromTheDescription()
    {
        var (service, _) = await ServiceWithProductAsync();

        var claim = await service.CreateCaseAsync(Command("Motor claim reported on WhatsApp", form: false, description: "My car was stolen last night"));

        Assert.Equal("theft", claim.ClaimTypeKey);
        Assert.Equal("Theft", claim.ClaimType);
    }

    [Fact]
    public async Task Intake_FromAnAutomaticChannel_KeepsTheTextWhenNothingMatches()
    {
        var (service, _) = await ServiceWithProductAsync();

        var claim = await service.CreateCaseAsync(Command("Email Intake", form: false, description: "Hello, please call me"));

        Assert.Equal("Email Intake", claim.ClaimType);
        Assert.Null(claim.ClaimTypeKey);
    }

    [Fact]
    public async Task Intake_WhenTheProductHasNoClaimTypes_BehavesExactlyAsBefore()
    {
        var (service, _) = await ServiceWithProductAsync(types: []);

        var claim = await service.CreateCaseAsync(Command("Whatever the customer typed", form: true));

        Assert.Equal("Whatever the customer typed", claim.ClaimType);
        Assert.Null(claim.ClaimTypeKey);
    }

    [Fact]
    public async Task Confirming_AnAutomaticClaim_ResolvesTheTypeAPersonTyped()
    {
        var (service, _) = await ServiceWithProductAsync();
        var created = await service.CreateCaseAsync(Command("Email Intake", form: false, description: "Hello") with { RequiresConfirmation = true });

        var confirmed = await service.ConfirmCaseAsync(created.CaseId, Scope, new ConfirmCaseCorrections(ClaimType: "Windscreen"));

        Assert.Equal("windscreen", confirmed.After!.ClaimTypeKey);
        Assert.Equal("Windscreen", confirmed.After.ClaimType);
    }

    private static Product Product(IReadOnlyList<ClaimTypeDefinition>? types = null) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 4,
            Name = "Motor Cover",
            ProductClass = "Motor",
            IraApprovalReference = "REF-1",
            IraApprovalEffectiveDate = new DateOnly(2026, 1, 1),
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = [],
            ClaimTypes = types ?? MotorTypes,
            UpdatedByUserId = "admin",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "test",
        };

    private static async Task<(CaseIntakeService Service, Product Product)> ServiceWithProductAsync(IReadOnlyList<ClaimTypeDefinition>? types = null)
    {
        var products = new Products();
        var product = Product(types);
        await products.SaveAsync(product);
        return (new CaseIntakeService(new Cases(), new Audits(), products), product);
    }

    private static CreateCaseCommand Command(string claimType, bool form, string description = "Something happened to the vehicle.") =>
        new(Scope, "Amina Okello", "amina@example.test", "+256700000000", "POL-1", "Motor", claimType, "WebPortal",
            new DateOnly(2026, 5, 24), description, "user-1", "corr-1", RequireConfiguredClaimType: form);

    private sealed class Cases : ICaseRepository
    {
        private readonly Dictionary<Guid, ClaimCase> items = [];
        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) { items[claimCase.CaseId] = claimCase; return Task.CompletedTask; }
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult(items.GetValueOrDefault(caseId));
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ClaimCase>>(items.Values.Where(item => item.Scope.Matches(scope)).ToArray());
    }

    private sealed class Audits : IAuditRepository
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
    }

    private sealed class Products : IProductRepository
    {
        private readonly List<Product> versions = [];
        public Task SaveAsync(Product version, CancellationToken cancellationToken = default) { versions.Add(version); return Task.CompletedTask; }
        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions.Where(item => item.ProductId == productId && item.Scope.Matches(scope)).OrderByDescending(item => item.Version).FirstOrDefault());
        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions.FirstOrDefault(item => item.ProductId == productId && item.Version == version && item.Scope.Matches(scope)));
        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions.Where(item => item.ProductId == productId && item.Scope.Matches(scope)).ToArray());
        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions.Where(item => item.Scope.Matches(scope)).GroupBy(item => item.ProductId).Select(group => group.OrderByDescending(item => item.Version).First()).ToArray());
    }
}

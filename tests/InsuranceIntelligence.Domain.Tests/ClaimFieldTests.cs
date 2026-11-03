using System.Reflection;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 5: claim fields per claim type.</summary>
public sealed class ClaimFieldTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    private static ClaimFieldDefinition Def(string key, ClaimFieldType type, bool required = false, bool customer = true, string[]? options = null, params string[] claimTypes) =>
        new(key, key, type, required, options, null, 0, customer, claimTypes);

    private static ClaimFieldValidator.Result Check(ClaimFieldDefinition definition, string value, bool customer = false) =>
        ClaimFieldValidator.Validate([definition], new Dictionary<string, string> { [definition.Key] = value }, requireRequired: false, customerAudience: customer);

    [Theory]
    [InlineData(ClaimFieldType.Number, "1,250.5", "1250.5")]
    [InlineData(ClaimFieldType.Currency, "1,500,000", "1500000")]
    [InlineData(ClaimFieldType.Date, "2026-10-01", "2026-10-01")]
    [InlineData(ClaimFieldType.Date, "1 October 2026", "2026-10-01")]
    [InlineData(ClaimFieldType.Phone, "+256 700 000 123", "+256700000123")]
    [InlineData(ClaimFieldType.YesNo, "YES", "Yes")]
    [InlineData(ClaimFieldType.YesNo, "false", "No")]
    [InlineData(ClaimFieldType.Text, "  UAX 123A  ", "UAX 123A")]
    public void Valid_values_are_accepted_and_normalised(ClaimFieldType type, string input, string expected)
    {
        var result = Check(Def("f", type), input);

        Assert.True(result.IsValid, string.Join(";", result.Errors.Values));
        Assert.Equal(expected, result.Values["f"]);
    }

    [Theory]
    [InlineData(ClaimFieldType.Number, "abc")]
    [InlineData(ClaimFieldType.Currency, "-5")]
    [InlineData(ClaimFieldType.Date, "not a date")]
    [InlineData(ClaimFieldType.Phone, "123")]
    [InlineData(ClaimFieldType.YesNo, "maybe")]
    public void Invalid_values_are_refused_with_a_reason_naming_the_field(ClaimFieldType type, string input)
    {
        var result = Check(Def("amount", type), input);

        Assert.False(result.IsValid);
        Assert.Contains("amount", result.Errors["amount"]);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void Select_accepts_an_option_in_any_case_and_refuses_others()
    {
        var definition = Def("channel", ClaimFieldType.Select, options: ["Phone", "WhatsApp"]);

        Assert.Equal("WhatsApp", Check(definition, "whatsapp").Values["channel"]);
        Assert.Contains("Phone, WhatsApp", Check(definition, "Pigeon").Errors["channel"]);
    }

    [Fact]
    public void A_blank_answer_is_simply_not_an_answer()
    {
        var result = Check(Def("f", ClaimFieldType.Number), "   ");

        Assert.True(result.IsValid);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void Required_fields_are_enforced_only_when_asked()
    {
        var definitions = new[] { Def("a", ClaimFieldType.Text, required: true), Def("b", ClaimFieldType.Text) };

        var partial = ClaimFieldValidator.Validate(definitions, new Dictionary<string, string>(), requireRequired: false, customerAudience: false);
        var filing = ClaimFieldValidator.Validate(definitions, new Dictionary<string, string>(), requireRequired: true, customerAudience: false);

        Assert.True(partial.IsValid);
        Assert.Equal(new[] { "a" }, filing.Errors.Keys);
    }

    [Fact]
    public void A_customer_cannot_answer_a_staff_only_field_or_an_unknown_one()
    {
        var definitions = new[] { Def("public", ClaimFieldType.Text), Def("internal", ClaimFieldType.Text, customer: false) };

        var result = ClaimFieldValidator.Validate(
            definitions,
            new Dictionary<string, string> { ["public"] = "ok", ["internal"] = "sneaky", ["made-up"] = "x" },
            requireRequired: false,
            customerAudience: true);

        Assert.Equal("ok", result.Values["public"]);
        Assert.False(result.Values.ContainsKey("internal"));
        Assert.Contains("internal", result.Errors.Keys);
        Assert.Contains("made-up", result.Errors.Keys);
    }

    [Fact]
    public void Staff_may_answer_staff_only_fields()
    {
        var result = ClaimFieldValidator.Validate(
            [Def("internal", ClaimFieldType.Text, customer: false)],
            new Dictionary<string, string> { ["internal"] = "note" },
            requireRequired: false,
            customerAudience: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Staff_only_required_fields_are_not_demanded_of_a_customer()
    {
        var result = ClaimFieldValidator.Validate(
            [Def("internal", ClaimFieldType.Text, required: true, customer: false)],
            new Dictionary<string, string>(),
            requireRequired: true,
            customerAudience: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Built_in_definitions_mirror_every_original_fnol_property()
    {
        foreach (var (type, builtIn) in new[] { (typeof(MotorFnolDetails), BuiltInClaimFields.Motor), (typeof(MedicalFnolDetails), BuiltInClaimFields.Medical) })
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
                .Where(name => name is not ("uploadedDocuments" or "equalityContract"))
                .ToArray();

            Assert.All(properties, name => Assert.Contains(builtIn, field => field.Key == name));
        }
    }

    [Fact]
    public void Values_read_through_the_original_records_and_the_new_values_win()
    {
        var claim = Claim() with
        {
            MotorFnolDetails = new MotorFnolDetails("UAX 123A", null, "Jinja Road", null, null, null, "No injuries reported", null, null, null),
            FieldValues = new Dictionary<string, string> { ["accidentLocation"] = "Northern Bypass", ["extra"] = "yes" },
        };

        var values = ClaimFieldValues.For(claim);

        Assert.Equal("UAX 123A", values["vehicleRegistration"]);
        Assert.Equal("Northern Bypass", values["accidentLocation"]);
        Assert.Equal("yes", values["extra"]);
        Assert.Equal("No injuries reported", values["injuryIndicator"]);
    }

    [Fact]
    public void Field_values_can_fill_the_motor_record_so_injury_detection_still_works()
    {
        var motor = ClaimFieldValues.ToMotorFnol(new Dictionary<string, string> { ["injuryIndicator"] = "Injury reported", ["vehicleRegistration"] = "UBB 1" }, null);

        Assert.Equal("Injury reported", motor!.InjuryIndicator);
        Assert.Equal("UBB 1", motor.VehicleRegistration);
        Assert.Null(ClaimFieldValues.ToMotorFnol(new Dictionary<string, string> { ["myOwnField"] = "x" }, null));
    }

    [Fact]
    public void Resolver_uses_the_products_fields_for_the_claim_type_else_the_built_in_ones()
    {
        var product = Product(
            Def("everyone", ClaimFieldType.Text),
            Def("theftOnly", ClaimFieldType.Text, claimTypes: "theft"));

        Assert.Equal(new[] { "everyone", "theftOnly" }, ClaimFieldResolver.ForProduct(product, "Motor", "theft").Select(field => field.Key));
        Assert.Equal(new[] { "everyone" }, ClaimFieldResolver.ForProduct(product, "Motor", "own-damage").Select(field => field.Key));
        Assert.Equal(BuiltInClaimFields.Motor, ClaimFieldResolver.ForProduct(null, "Motor", null));
        Assert.Equal(BuiltInClaimFields.Medical, ClaimFieldResolver.ForProduct(Product(), "Medical", null));
        Assert.Empty(ClaimFieldResolver.ForProduct(null, "Agriculture", null));
    }

    [Fact]
    public async Task Intake_refuses_a_filing_that_misses_a_required_field_and_names_it()
    {
        var service = await ServiceAsync(Def("policeNumber", ClaimFieldType.Text, required: true));

        var error = await Assert.ThrowsAsync<ClaimFieldsInvalidException>(() => service.CreateCaseAsync(Command()));

        Assert.Contains("policeNumber", error.Errors.Keys);
    }

    [Fact]
    public async Task Intake_stores_validated_values_and_feeds_the_motor_record()
    {
        var service = await ServiceAsync(Def("policeNumber", ClaimFieldType.Text, required: true), Def("injuryIndicator", ClaimFieldType.Select, options: ["Unknown", "Injury reported"]));

        var claim = await service.CreateCaseAsync(Command(new Dictionary<string, string> { ["policeNumber"] = "POL/1", ["injuryIndicator"] = "injury reported" }));

        Assert.Equal("POL/1", claim.FieldValues!["policeNumber"]);
        Assert.Equal("Injury reported", claim.FieldValues["injuryIndicator"]);
        Assert.Equal("Injury reported", claim.MotorFnolDetails!.InjuryIndicator);
    }

    [Fact]
    public async Task Intake_counts_the_original_fnol_details_as_answers_to_required_fields()
    {
        var service = await ServiceAsync(Def("vehicleRegistration", ClaimFieldType.Text, required: true));

        var claim = await service.CreateCaseAsync(Command() with
        {
            MotorFnolDetails = new MotorFnolDetails("UAX 1", null, null, null, null, null, null, null, null, null),
        });

        Assert.NotNull(claim);
    }

    [Fact]
    public async Task Intake_from_a_customer_refuses_a_staff_only_answer()
    {
        var service = await ServiceAsync(Def("internalNote", ClaimFieldType.Text, customer: false));

        var error = await Assert.ThrowsAsync<ClaimFieldsInvalidException>(() =>
            service.CreateCaseAsync(Command(new Dictionary<string, string> { ["internalNote"] = "hi" }) with { FieldsFromCustomer = true }));

        Assert.Contains("internalNote", error.Errors.Keys);
    }

    [Fact]
    public async Task Intake_on_a_product_with_no_fields_asks_nothing_and_changes_nothing()
    {
        var service = await ServiceAsync();

        var claim = await service.CreateCaseAsync(Command());

        Assert.Null(claim.FieldValues);
    }

    private static Product Product(params ClaimFieldDefinition[] fields) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = "Motor Cover",
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = [],
            ClaimFields = fields,
            UpdatedByUserId = "u",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "test",
        };

    private static async Task<CaseIntakeService> ServiceAsync(params ClaimFieldDefinition[] fields)
    {
        var products = new Products();
        await products.SaveAsync(Product(fields));
        return new CaseIntakeService(new Cases(), new Audits(), products);
    }

    private static CreateCaseCommand Command(Dictionary<string, string>? values = null) =>
        new(Scope, "Amina Okello", "a@example.test", "+256700000000", "POL-1", "Motor", "Accident", "WebPortal",
            new DateOnly(2026, 5, 24), "Collision.", "user-1", "corr-1", RequireConfiguredClaimType: true, FieldValues: values);

    private static ClaimCase Claim() =>
        new()
        {
            CaseId = Guid.NewGuid(), Scope = Scope, CustomerName = "A", PolicyNumber = "P", ProductLine = "Motor", ClaimType = "Accident",
            Channel = "Phone", IncidentDate = new DateOnly(2026, 1, 1), IncidentDescription = "d", Status = CaseStatus.New, CompletenessScore = 0,
            CreatedByUserId = "u", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "c",
        };

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
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
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

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

    [Fact]
    public async Task BuildChecklistAsync_ADeliberatelyMisspeltMatrixKey_IsRequiredAsACustomDocumentInsteadOfDropped()
    {
        // Claim intake build prompt outcome A: a key that does not match one of the eight
        // DocumentType names used to vanish from the checklist with no error. It must now surface
        // as a named custom requirement instead of disappearing.
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("SchoolAttendanceLetter", "School attendance letter", "Confirms the student was enrolled", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);

        var checklist = await provider.BuildChecklistAsync(MotorClaim(product.ProductId, product.Version), []);

        Assert.Contains(checklist.CustomRequired, entry => entry.Key == "SchoolAttendanceLetter");
        Assert.Contains(checklist.MissingRequiredDocuments, missing => missing.Key == "SchoolAttendanceLetter" && missing.Label == "School attendance letter");
        Assert.Equal(0m, checklist.CompletenessScore.Percentage);
    }

    [Fact]
    public async Task BuildChecklistAsync_ACustomDocumentKey_IsSatisfiedWhenTheMatchingDocumentIsReceived()
    {
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("SchoolAttendanceLetter", "School attendance letter", "Confirms the student was enrolled", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);
        var claimCase = MotorClaim(product.ProductId, product.Version);
        var received = ReceivedDocument(claimCase.CaseId, documentTypeKey: "SchoolAttendanceLetter");

        var checklist = await provider.BuildChecklistAsync(claimCase, [received]);

        Assert.DoesNotContain(checklist.MissingRequiredDocuments, missing => missing.Key == "SchoolAttendanceLetter");
        Assert.Contains("SchoolAttendanceLetter", checklist.CustomReceived);
        Assert.Equal(100m, checklist.CompletenessScore.Percentage);
    }

    [Fact]
    public async Task BuildChecklistAsync_UploadedSelectedBuiltInTypeCountsAsReceivedWithoutClaimingItWasClassified()
    {
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("ClaimForm", "Claim form", "Required to register the claim", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);
        var claimCase = MotorClaim(product.ProductId, product.Version);
        var uploaded = ReceivedDocument(claimCase.CaseId, documentTypeKey: "ClaimForm") with
        {
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            DocumentType = DocumentType.Unknown,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "NotConfigured", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "NotConfigured",
                DateTimeOffset.UtcNow,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ProcessedAt = null,
        };

        var checklist = await provider.BuildChecklistAsync(claimCase, [uploaded]);

        Assert.Contains(DocumentType.ClaimForm, checklist.ReceivedDocuments);
        Assert.Empty(checklist.MissingRequiredDocuments);
        Assert.Equal(100m, checklist.CompletenessScore.Percentage);
        Assert.Equal(DocumentType.Unknown, uploaded.DocumentType);
        Assert.Equal("NotConfigured", uploaded.Classification.ProviderName);
        Assert.Equal("NotConfigured", uploaded.Extraction.ProviderName);
    }

    [Fact]
    public async Task BuildChecklistAsync_UploadedWithoutSelectedTypeDoesNotSatisfyBuiltInRequirement()
    {
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("ClaimForm", "Claim form", "Required to register the claim", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);
        var claimCase = MotorClaim(product.ProductId, product.Version);
        var uploaded = ReceivedDocument(claimCase.CaseId) with
        {
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            DocumentType = DocumentType.Unknown,
        };

        var checklist = await provider.BuildChecklistAsync(claimCase, [uploaded]);

        Assert.Empty(checklist.ReceivedDocuments);
        Assert.Contains(checklist.MissingRequiredDocuments, missing => missing.Key == "ClaimForm");
    }

    [Fact]
    public async Task BuildChecklistAsync_AnOlderDocumentWithNoStoredKey_StillSatisfiesItsBuiltInEnumRequirement()
    {
        // Pre-existing documents were persisted before DocumentTypeKey existed, so it deserializes
        // as null. Matching must still work off the DocumentType enum alone.
        var repository = new FakeProductRepository();
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("MedicalReport", "Medical report", "Required for injury assessment", IsRequired: true, "intimation_and_reference", false),
        ]);
        await repository.SaveAsync(product);
        var provider = new StaticDocumentChecklistProvider(new CompletenessScoringService(), repository);
        var claimCase = MotorClaim(product.ProductId, product.Version);
        var received = ReceivedDocument(claimCase.CaseId, documentType: DocumentType.MedicalReport, documentTypeKey: null);

        var checklist = await provider.BuildChecklistAsync(claimCase, [received]);

        Assert.Contains(DocumentType.MedicalReport, checklist.ReceivedDocuments);
        Assert.Empty(checklist.MissingRequiredDocuments);
        Assert.Equal(100m, checklist.CompletenessScore.Percentage);
    }

    private static CaseDocument ReceivedDocument(Guid caseId, DocumentType documentType = DocumentType.Unknown, string? documentTypeKey = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = caseId,
            Scope = Scope,
            OriginalFileName = "document.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 128,
            ObjectKey = $"{Scope.TenantId}/{Scope.WorkspaceId}/case/document.pdf",
            DocumentType = documentType,
            DocumentTypeKey = documentTypeKey,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(documentType, 0.9m, "Mock", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "Mock",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = "user-claims-1",
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = "corr-checklist-product-tests",
        };
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

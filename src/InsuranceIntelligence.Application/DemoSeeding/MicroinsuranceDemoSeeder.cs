using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.DemoSeeding;

/// <summary>Who the demo phone's policyholder is. The name comes from configuration, never from
/// source code, the same rule as the rest of the demo seeding.</summary>
public sealed record DemoPolicyholderSettings(string FullName);

/// <summary>
/// The Kato field test's microinsurance catalogue: three products a regulator would recognise
/// (hospital cash and funeral, boda rider personal accident with daily income, market stall stock),
/// each labelled as microinsurance with every sum insured within the UGX 10 million ceiling of the
/// IRA's Interim Micro Insurance Guidelines, 2016, a document matrix matching the evidence insurers
/// actually ask for, and a policy wording plus a key information summary in the knowledge base.
///
/// Also replaces the made-up approval references the earlier demo seed stamped on its products
/// ("IRA/DEMO/MOTOR/2026" and similar) with <see cref="DemoApprovalMarker"/>: nothing a participant
/// or an IRA judge sees may look like a real IRA approval number.
///
/// Idempotent throughout: products are found by class, documents by name, and the demo
/// policyholder's records by identifiers derived from the phone number. Demo profile only.
/// </summary>
public sealed class MicroinsuranceDemoSeeder(
    IProductRepository productRepository,
    IKnowledgeDocumentRepository knowledgeRepository,
    IKnowledgeIndexProvider knowledgeIndex,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository,
    ICaseRepository caseRepository,
    IAuditRepository auditRepository,
    DemoPolicyholderSettings policyholderSettings) : IDemoPolicyholderLoader
{
    public const string DemoApprovalMarker = "DEMO PRODUCT, NOT IRA-APPROVED";
    private const string SeedActor = "system:demo-seed";
    private const string SeedReviewer = "system:demo-seed-reviewer";
    private const string LegacyReferencePrefix = "IRA/DEMO/";

    public sealed record CatalogueResult(int ProductsCreated, int ProductsRelabelled, int DocumentsIndexed, int DocumentsNotIndexed);

    public async Task<CatalogueResult> SeedCatalogueAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var relabelled = await RelabelLegacyReferencesAsync(scope, now, cancellationToken);
        var created = 0;
        foreach (var product in CatalogueProducts)
        {
            if (await EnsureProductAsync(scope, product, now, cancellationToken))
            {
                created++;
            }
        }

        var indexed = 0;
        var notIndexed = 0;
        foreach (var document in CatalogueDocuments)
        {
            try
            {
                if (await EnsureKnowledgeDocumentAsync(scope, document, now, cancellationToken))
                {
                    indexed++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Search not configured or unreachable: the document is not saved as indexed, so
                // Kato keeps saying it cannot answer rather than pretending the wording is there.
                notIndexed++;
            }
        }

        return new CatalogueResult(created, relabelled, indexed, notIndexed);
    }

    /// <summary>"#demo" on the demo phone: the demo policyholder with Hospital Cash and Boda Rider
    /// Accident cover, and one Hospital Cash claim waiting for documents, so "where is my claim?"
    /// has something real to show. Running it again restores the same records.</summary>
    public async Task<string> LoadAsync(TenantScope scope, string senderWaId, string correlationId, CancellationToken cancellationToken)
    {
        await SeedCatalogueAsync(scope, cancellationToken);
        var result = await SeedDemoPolicyholderAsync(scope, senderWaId, cancellationToken);
        return $"Demo policyholder loaded for this phone: {result.FullName}, with Hospital Cash and Boda Rider Accident cover, " +
               $"and claim {ClaimReference.For(result.ClaimId)} waiting for documents. Send any message to start. Send #reset to clear it again.";
    }

    public sealed record DemoPolicyholderResult(string FullName, IReadOnlyList<string> PolicyNumbers, Guid ClaimId);

    public async Task<DemoPolicyholderResult> SeedDemoPolicyholderAsync(TenantScope scope, string phone, CancellationToken cancellationToken)
    {
        var digits = PhoneNumbers.Digits(phone);
        var key = digits.Length > 9 ? digits[^9..] : digits;
        var idKey = $"{scope.TenantId}:{scope.WorkspaceId}:{key}"; // ids are per workspace, never shared between organisations
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var fullName = string.IsNullOrWhiteSpace(policyholderSettings.FullName) ? "Demo Policyholder" : policyholderSettings.FullName.Trim();

        // Reuse whoever is already on file for this phone (the contact Kato saved on first contact),
        // renamed: a second record under a different name would leave the number unidentifiable.
        var onFile = (await customerRepository.ListAsync(scope, cancellationToken))
            .Where(c => PhoneNumbers.SameNumber(c.Phone, digits))
            .ToArray();
        var customerId = onFile.Length == 1 ? onFile[0].CustomerId : DeterministicGuid($"micro-demo-{idKey}").ToString();
        foreach (var other in onFile.Where(c => c.CustomerId != customerId))
        {
            await customerRepository.UpsertAsync(other with { FullName = "Erased customer", Email = null, Phone = null }, cancellationToken);
        }

        var customer = new Customer(customerId, scope.TenantId, scope.WorkspaceId, fullName, null, $"+{digits}");
        await customerRepository.UpsertAsync(customer, cancellationToken);

        var effectiveFrom = today.AddDays(-120);
        var sequence = key[^5..];
        var hospital = new Policy(
            DeterministicGuid($"micro-demo-{idKey}-hospital").ToString(), scope.TenantId, scope.WorkspaceId,
            $"NSA/HC/{effectiveFrom.Year}/{sequence}", MicroinsuranceCatalog.HospitalCashLine, "Hospital Cash, UGX 1,000 a month",
            customer.CustomerId, null, effectiveFrom, effectiveFrom.AddYears(1), 12_000m, "UGX", 1_000_000m);
        var boda = new Policy(
            DeterministicGuid($"micro-demo-{idKey}-boda").ToString(), scope.TenantId, scope.WorkspaceId,
            $"NSA/BA/{effectiveFrom.Year}/{sequence}", MicroinsuranceCatalog.BodaAccidentLine, "Boda Rider Accident, UGX 6,000 a month",
            customer.CustomerId, null, effectiveFrom, effectiveFrom.AddYears(1), 72_000m, "UGX", 2_500_000m);
        await policyRepository.UpsertAsync(hospital, cancellationToken);
        await policyRepository.UpsertAsync(boda, cancellationToken);

        // Older than the 72-hour duplicate window, so a new accident report is never mistaken for it.
        var claimId = DeterministicGuid($"micro-demo-{idKey}-hospital-claim");
        var createdAt = now.AddDays(-5);
        var product = (await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
            .FirstOrDefault(p => string.Equals(p.ProductClass, MicroinsuranceCatalog.HospitalCashLine, StringComparison.OrdinalIgnoreCase)
                && p.Status == ProductStatus.Published);
        var existing = await caseRepository.GetAsync(claimId, cancellationToken);
        await caseRepository.SaveAsync(new ClaimCase
        {
            CaseId = claimId,
            Scope = scope,
            CustomerName = customer.FullName,
            CustomerEmail = null,
            CustomerPhone = customer.Phone,
            PolicyNumber = hospital.PolicyNumber,
            ProductLine = hospital.ProductLine,
            ProductId = product?.ProductId,
            ProductVersion = product?.Version,
            ClaimType = "Hospital Cash claim reported on WhatsApp",
            Channel = "WhatsApp",
            IncidentDate = DateOnly.FromDateTime(createdAt.AddDays(-4).UtcDateTime),
            IncidentDescription = "Admitted to Mulago hospital for three nights with malaria. Claiming hospital cash for the three nights.",
            ClaimedAmount = 120_000m,
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 0m,
            RiskLevel = "Low",
            Priority = CasePriority.Normal,
            SlaDueAt = now.AddDays(5),
            AssignedUserId = null,
            AssignedTeam = "Microinsurance Claims",
            CreatedByUserId = SeedActor,
            CreatedAt = createdAt,
            UpdatedAt = now,
            CorrelationId = $"micro-demo-{key}-hospital-claim",
        }, cancellationToken);

        if (existing is null)
        {
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, claimId, null, SeedActor, ActorType.Integration, "case.created", createdAt,
                    null, CaseStatus.MissingDocuments.ToString(), [], "DemoSeed", null, "accepted", null,
                    "Demo claim seeded for the Kato field test phone.", $"micro-demo-{key}"),
                cancellationToken);
        }

        return new DemoPolicyholderResult(customer.FullName, [hospital.PolicyNumber, boda.PolicyNumber], claimId);
    }

    private async Task<int> RelabelLegacyReferencesAsync(TenantScope scope, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var relabelled = 0;
        foreach (var product in await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
        {
            if (product.IraApprovalReference?.StartsWith(LegacyReferencePrefix, StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            // A new version, never an edit in place: claims pinned to the old version keep it.
            await productRepository.SaveAsync(product with
            {
                VersionId = Guid.NewGuid(),
                Version = product.Version + 1,
                IraApprovalReference = DemoApprovalMarker,
                UpdatedByUserId = SeedActor,
                UpdatedAt = now,
                ChangeReason = "Replaced a made-up approval reference with a plain demo marker. No IRA approval exists for this demo product.",
            }, cancellationToken);
            relabelled++;
        }

        return relabelled;
    }

    private sealed record CatalogueProduct(string ProductClass, string Name, IReadOnlyList<ProductDocumentRequirement> Documents);

    private static readonly string DocumentStage = DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest;

    private static ProductDocumentRequirement Requirement(string key, string label, string reason, bool required) =>
        new(key, label, reason, required, DocumentStage, IsRegulatoryOrWordingMandated: false);

    private static readonly CatalogueProduct[] CatalogueProducts =
    [
        new(MicroinsuranceCatalog.HospitalCashLine, "Hospital Cash and Funeral (microinsurance, demo)",
        [
            Requirement("MedicalReport", "the hospital discharge note or admission letter", "Shows the nights spent in hospital.", true),
            Requirement("BurialPermit", "the burial permit or an LC1 letter, for a funeral claim", "Confirms the death for the funeral benefit.", false),
            Requirement("CustomerId", "your national ID", "Confirms the claim is yours.", false),
        ]),
        new(MicroinsuranceCatalog.BodaAccidentLine, "Boda Rider Accident with daily income (microinsurance, demo)",
        [
            Requirement("PoliceAbstract", "the police report of the accident", "Confirms the accident was reported to the police.", true),
            Requirement("MedicalReport", "the medical form from the clinic or hospital", "Shows the injury and how long it keeps you from work.", true),
            Requirement("PhotoEvidence", "photos of the motorcycle or the injury", "Shows what happened.", false),
        ]),
        new(MicroinsuranceCatalog.StallStockLine, "Market Stall Stock (microinsurance, demo)",
        [
            Requirement("PoliceAbstract", "the police report, or the fire brigade report for a fire", "Confirms the theft or the fire.", true),
            Requirement("StockList", "a list of the stock you lost and what it cost", "Shows the value of the goods lost.", true),
            Requirement("PhotoEvidence", "photos of the stall after the loss", "Shows the damage.", false),
        ]),
        new(MicroinsuranceCatalog.ShopLine, "Small Business Shop Cover (microinsurance, demo)",
        [
            Requirement("PoliceAbstract", "the police report, or the fire brigade report for a fire", "Confirms the break-in or the fire.", true),
            Requirement("StockList", "a list of what was lost or damaged and what it was worth", "Shows the value of the loss.", true),
            Requirement("PhotoEvidence", "photos of the shop after the loss", "Shows the damage.", false),
        ]),
        new(MicroinsuranceCatalog.TransitLine, "Goods in Transit (microinsurance, demo)",
        [
            Requirement("PoliceAbstract", "the police report of the theft or the accident", "Confirms what happened on the road.", true),
            Requirement("SupplierReceipt", "the supplier's receipt or delivery note for the goods", "Shows what was being carried and its value.", true),
            Requirement("PhotoEvidence", "photos of the damaged goods or vehicle, if you have them", "Shows the damage.", false),
        ]),
        new(MicroinsuranceCatalog.MaternityLine, "Mama Care Maternity (microinsurance, demo)",
        [
            Requirement("MedicalReport", "the hospital discharge summary or birth notification", "Shows the delivery and how the baby was born.", true),
            Requirement("Invoice", "the hospital bill or receipts", "Shows what the delivery cost.", true),
        ]),
    ];

    private async Task<bool> EnsureProductAsync(TenantScope scope, CatalogueProduct entry, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = (await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
            .Any(p => string.Equals(p.ProductClass, entry.ProductClass, StringComparison.OrdinalIgnoreCase) && p.Status == ProductStatus.Published);
        if (existing)
        {
            return false;
        }

        await productRepository.SaveAsync(new Product
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = 1,
            Name = entry.Name,
            ProductClass = entry.ProductClass,
            IraApprovalReference = DemoApprovalMarker,
            IraApprovalEffectiveDate = DateOnly.FromDateTime(now.UtcDateTime),
            ApprovedWordingKnowledgeDocumentId = null,
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = entry.Documents,
            UpdatedByUserId = SeedActor,
            UpdatedAt = now,
            ChangeReason = "Microinsurance demo product for the Kato field test. Not an IRA-approved product.",
            PublishSubmittedByUserId = SeedActor,
            PublishSubmittedAt = now,
            PublishApprovedByUserId = SeedReviewer,
            PublishApprovedAt = now,
        }, cancellationToken);
        return true;
    }

    private sealed record CatalogueDocument(string Name, string ProductLine, KnowledgeDocumentType Type, string Text);

    private async Task<bool> EnsureKnowledgeDocumentAsync(TenantScope scope, CatalogueDocument entry, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var documentId = DeterministicGuid($"{scope.TenantId}:{scope.WorkspaceId}:micro-kb:{entry.Name}");
        var existing = await knowledgeRepository.GetAsync(documentId, scope, cancellationToken);
        if (existing is { ApprovalStatus: KnowledgeDocumentStatus.Approved, IndexedStatus: KnowledgeIndexedStatus.Indexed })
        {
            return false;
        }

        const string version = "v1.0";
        var objectKey = $"demo-seed/knowledge/{documentId:N}.txt";
        var chunks = Chunk(entry.Text)
            .Select((content, index) => new KnowledgeChunk
            {
                ChunkId = $"{documentId:N}-{index}",
                KnowledgeDocumentId = documentId,
                Scope = scope,
                DocumentName = entry.Name,
                ProductLine = entry.ProductLine,
                Version = version,
                ChunkIndex = index,
                Content = content,
                SourceReference = new KnowledgeSourceReference(documentId, $"{documentId:N}-{index}", entry.Name, entry.ProductLine, version, objectKey),
            })
            .ToArray();

        var document = new KnowledgeDocument
        {
            KnowledgeDocumentId = documentId,
            Scope = scope,
            DocumentName = entry.Name,
            ProductLine = entry.ProductLine,
            DocumentType = entry.Type,
            Version = version,
            EffectiveDate = DateOnly.FromDateTime(now.UtcDateTime),
            OriginalFileName = $"{entry.Name}.txt",
            ContentType = "text/plain",
            FileSizeBytes = entry.Text.Length,
            ObjectKey = objectKey,
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            Sensitivity = KnowledgeSensitivity.Approved,
            GovernanceReason = "Microinsurance demo content for the Kato field test, written from the IRA Interim Micro Insurance Guidelines 2016 and published market products.",
            Chunks = chunks,
            UploadedByUserId = SeedActor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = $"micro-kb-{documentId:N}",
        };

        await knowledgeIndex.IndexAsync(document, cancellationToken);
        await knowledgeRepository.SaveAsync(document, cancellationToken);
        return true;
    }

    // Paragraph-aligned chunks of up to about 700 characters, so a benefit and its conditions stay together.
    private static IEnumerable<string> Chunk(string text)
    {
        var current = new List<string>();
        var length = 0;
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (length > 0 && length + paragraph.Length > 700)
            {
                yield return string.Join(" ", current);
                current.Clear();
                length = 0;
            }

            current.Add(paragraph.Replace('\n', ' '));
            length += paragraph.Length;
        }

        if (current.Count > 0)
        {
            yield return string.Join(" ", current);
        }
    }

    private static Guid DeterministicGuid(string value) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static readonly CatalogueDocument[] CatalogueDocuments =
    [
        new("Nile Shield Hospital Cash and Funeral, Key Information", MicroinsuranceCatalog.HospitalCashLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Hospital Cash and Funeral. This is a microinsurance product. It is a sample product being tested with customers.

            What it pays. Nile Shield Hospital Cash pays you UGX 40,000 for each night you spend in hospital, up to 10 nights a year. If you die, it pays your family UGX 1,000,000 for the funeral. The most it can pay in one year is UGX 1,400,000.

            What it costs. UGX 1,000 a month, paid by mobile money.

            When cover starts (waiting period). Accidents are covered from the day you join. Illness is covered 30 days after you join. Pregnancy and childbirth are covered 9 months after you join.

            Grace period. If you miss a payment, you have 30 days to pay before your cover stops. A claim in those 30 days is still paid, minus the missed payment.

            What it does not pay for. A stay of less than one night. Treatment you get without being admitted. Injuries you cause to yourself on purpose. Illness you already had in the 30 days before you joined. Stays for cosmetic treatment.

            How to claim. Tell Kato on WhatsApp or call us within 30 days. For a hospital stay we need the hospital discharge note or admission letter. For a funeral we need the burial permit or a letter from the LC1. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 10 working days of getting the papers.
            """),
        new("Nile Shield Hospital Cash and Funeral, Policy Wording", MicroinsuranceCatalog.HospitalCashLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Hospital Cash and Funeral, microinsurance policy wording, demo version 1.0.

            Section 1, the cover. When the insured person is admitted to a registered hospital or health centre in Uganda and stays at least one night, we pay the daily hospital benefit of UGX 40,000 for each night, up to 10 nights in a policy year. When the insured person dies during the period of cover, we pay the funeral benefit of UGX 1,000,000 to the named beneficiary. The sum insured under this policy is UGX 1,400,000 a year, within the microinsurance limit of UGX 10,000,000.

            Section 2, premium and grace period. The premium is UGX 1,000 a month. There is a grace period of 30 days for a missed premium. Cover continues during the grace period. If the premium is still unpaid after 30 days, the cover lapses.

            Section 3, waiting periods. There is no waiting period for accidents. There is a waiting period of 30 days for illness, and 9 months for pregnancy and childbirth.

            Section 4, exclusions. We do not pay for: a stay of less than one night; outpatient treatment; self-inflicted injury; an illness that began in the 30 days before cover started; cosmetic treatment; a death in the first 30 days of cover that is not caused by an accident.

            Section 5, claims. A claim must be reported within 30 days. Hospital claims need the discharge note or admission letter. Funeral claims need the burial permit or an LC1 letter. We settle within 10 working days of receiving complete documents and pay by mobile money.

            Section 6, complaints. If you are not happy with a decision, tell us and a person will review it within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Boda Rider Accident, Key Information", MicroinsuranceCatalog.BodaAccidentLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Boda Rider Accident. This is a microinsurance product for boda boda riders. It is a sample product being tested with customers.

            What it pays. If an accident stops you from working, it pays you UGX 50,000 for each day you cannot work, up to 15 days for one accident. If an accident leaves you disabled for life, or you die from an accident, it pays UGX 2,500,000. The most it pays for one accident is UGX 3,250,000.

            What it costs. UGX 6,000 a month, paid by mobile money.

            When cover starts (waiting period). You are covered from the day you join. There is no waiting period.

            Grace period. If you miss a payment, you have 15 days to pay before your cover stops.

            What it does not pay for. Riding without a valid riding permit. Riding under the influence of alcohol or drugs. Racing. Injuries from a fight you started. Illness that is not caused by an accident. Damage to the motorcycle itself, which is covered by motor insurance, not by this product.

            How to claim. Tell Kato on WhatsApp or call us within 30 days of the accident. We need the police report of the accident and the medical form from the clinic or hospital. Photos help but are not required. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 10 working days of getting the papers.
            """),
        new("Nile Shield Boda Rider Accident, Policy Wording", MicroinsuranceCatalog.BodaAccidentLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Boda Rider Accident, microinsurance personal accident policy wording, demo version 1.0.

            Section 1, the cover. When the insured rider suffers bodily injury in an accident during the period of cover, we pay: a daily income benefit of UGX 50,000 for each day a doctor certifies the rider cannot work, from the first day, up to 15 days for each accident; and a lump sum of UGX 2,500,000 for permanent total disablement or accidental death. The most payable for one accident is UGX 3,250,000, within the microinsurance limit of UGX 10,000,000.

            Section 2, premium and grace period. The premium is UGX 6,000 a month. There is a grace period of 15 days.

            Section 3, waiting period. None. Cover starts on the day the first premium is paid.

            Section 4, exclusions. We do not pay for injury: while riding without a valid riding permit; while under the influence of alcohol or drugs; while racing or performing stunts; from a fight the rider started; that is self-inflicted. We do not pay for illness, or for damage to the motorcycle or to other people's property.

            Section 5, claims. Report the accident within 30 days. We need the police report and a medical form completed by the treating clinic or hospital. We settle within 10 working days of complete documents and pay by mobile money.

            Section 6, complaints. A person reviews any complaint within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Market Stall Stock, Key Information", MicroinsuranceCatalog.StallStockLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Market Stall Stock. This is a microinsurance product for market traders and small shops. It is a sample product being tested with customers.

            What it pays. It pays for the goods in your stall or shop if they are burnt in a fire or stolen in a break-in, up to UGX 3,000,000 a year. For each claim you pay the first UGX 50,000 yourself.

            What it costs. UGX 5,000 a month, paid by mobile money. This is a sample price for testing.

            When cover starts (waiting period). Cover starts 14 days after you join.

            Grace period. If you miss a payment, you have 15 days to pay before your cover stops.

            What it does not pay for. Goods stolen without a break-in, for example while the stall is open. Cash. Goods that rot or go bad. Damage from floods. Losses you cannot show with a list of the goods and what they cost.

            How to claim. Report a theft or fire to the police (or the fire brigade) and tell Kato on WhatsApp within 14 days. We need the police or fire report and a list of the goods you lost and what they cost. Photos of the stall help. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 15 working days of getting the papers.
            """),
        new("Nile Shield Market Stall Stock, Policy Wording", MicroinsuranceCatalog.StallStockLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Market Stall Stock, microinsurance property policy wording, demo version 1.0.

            Section 1, the cover. We cover stock in trade kept at the insured stall or shop against loss or damage by fire, lightning or explosion, and against theft involving forcible and violent entry. The sum insured is chosen by the trader between UGX 1,000,000 and UGX 3,000,000, within the microinsurance limit of UGX 10,000,000. An excess of UGX 50,000 applies to each claim.

            Section 2, premium and grace period. The premium is UGX 5,000 a month for UGX 3,000,000 of cover. There is a grace period of 15 days.

            Section 3, waiting period. Cover starts 14 days after the first premium is paid.

            Section 4, exclusions. We do not pay for: theft without forcible and violent entry; cash or money; perishable goods that spoil; flood; wear and tear; losses that cannot be supported by a stock list or receipts.

            Section 5, claims. Report the loss to the police or fire brigade and to us within 14 days. We need the police or fire brigade report and a list of the stock lost with its cost. We settle within 15 working days of complete documents and pay by mobile money.

            Section 6, complaints. A person reviews any complaint within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Hospital Cash, How to Join, Pay and Claim", MicroinsuranceCatalog.HospitalCashLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Hospital Cash. This is a microinsurance product being tested with customers. Anyone aged 18 to 65 can join, whatever their work. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area, and choose the cover. You do not need to visit an office and no papers are needed to join.

            How to pay. The cover costs UGX 1,000 a month, paid by mobile money on MTN or Airtel. The first payment starts the cover. Accidents are covered from the day you join, illness 30 days after you join. If you miss a payment you have a grace period before the cover stops.

            How to make a claim. Tell Kato on WhatsApp what happened, when and where. Kato reads it back to you, and when you confirm, the claim goes straight to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share your papers: the hospital discharge note or admission letter, or for a funeral the burial permit or an LC1 letter. Never send papers or ID in the WhatsApp chat itself.

            How claims are paid. The claims team reviews the claim and the papers. When a claim is approved, the money is paid to you by mobile money on the phone number on your policy, usually within 10 working days of receiving all the papers. You can ask Kato where your claim is at any time.
            """),
        new("Nile Shield Boda Rider Accident, How to Join, Pay and Claim", MicroinsuranceCatalog.BodaAccidentLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Boda Rider Accident. This is a microinsurance product being tested with customers. Boda boda riders aged 18 to 60 with a riding permit can join. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area, and choose the cover. You do not need to visit an office and no papers are needed to join.

            How to pay. The cover costs UGX 6,000 a month, paid by mobile money on MTN or Airtel. The first payment starts the cover. You are covered from the day you join. If you miss a payment you have a grace period before the cover stops.

            How to make a claim. Tell Kato on WhatsApp what happened, when and where. Kato reads it back to you, and when you confirm, the claim goes straight to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share your papers: the police report of the accident and the medical form from the clinic or hospital. Never send papers or ID in the WhatsApp chat itself.

            How claims are paid. The claims team reviews the claim and the papers. When a claim is approved, the money is paid to you by mobile money on the phone number on your policy, usually within 10 working days of receiving all the papers. You can ask Kato where your claim is at any time.
            """),
        new("Nile Shield Market Stall Stock, How to Join, Pay and Claim", MicroinsuranceCatalog.StallStockLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Market Stall Stock. This is a microinsurance product being tested with customers. Market vendors and stall owners can join. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area, and choose the cover. You do not need to visit an office and no papers are needed to join.

            How to pay. The cover costs UGX 5,000 a month, paid by mobile money on MTN or Airtel. The first payment starts the cover. Cover starts 14 days after you join. If you miss a payment you have a grace period before the cover stops.

            How to make a claim. Tell Kato on WhatsApp what happened, when and where. Kato reads it back to you, and when you confirm, the claim goes straight to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share your papers: the police or fire brigade report and a list of the goods you lost with what they cost. Never send papers or ID in the WhatsApp chat itself.

            How claims are paid. The claims team reviews the claim and the papers. When a claim is approved, the money is paid to you by mobile money on the phone number on your policy, usually within 10 working days of receiving all the papers. You can ask Kato where your claim is at any time.
            """),
        new("Nile Shield Small Business Shop Cover, How to Join, Pay and Claim", MicroinsuranceCatalog.ShopLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Small Business Shop Cover. This is a microinsurance product being tested with customers. Owners of a shop, boutique, salon, pharmacy, hardware or similar small business with a lockable premises can join. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area, and choose the cover. You do not need to visit an office and no papers are needed to join.

            How to pay. The cover costs UGX 15,000 a month, paid by mobile money on MTN or Airtel. The first payment starts the cover. Cover starts 7 days after you join. If you miss a payment you have a grace period before the cover stops.

            How to make a claim. Tell Kato on WhatsApp what happened, when and where. Kato reads it back to you, and when you confirm, the claim goes straight to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share your papers: the police or fire brigade report, a list of what was lost or damaged with its value, and photos of the premises. Never send papers or ID in the WhatsApp chat itself.

            How claims are paid. The claims team reviews the claim and the papers. When a claim is approved, the money is paid to you by mobile money on the phone number on your policy, usually within 10 working days of receiving all the papers. You can ask Kato where your claim is at any time.
            """),
        new("Nile Shield Small Business Shop Cover, Key Information", MicroinsuranceCatalog.ShopLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Small Business Shop Cover. This is a microinsurance product for owners of shops, boutiques, salons, pharmacies, hardware shops and similar small businesses. It is a sample product being tested with customers.

            What it pays. It pays for your stock, shelves, fittings and equipment kept in your shop if they are burnt in a fire, damaged by lightning or an explosion, or stolen in a break-in, up to UGX 8,000,000 a year. For each claim you pay the first UGX 100,000 yourself.

            What it costs. UGX 15,000 a month, paid by mobile money.

            When cover starts (waiting period). Cover starts 7 days after you join.

            Grace period. If you miss a payment, you have 15 days to pay before your cover stops.

            What it does not pay for. Goods stolen without a break-in, for example while the shop is open. Cash. Goods that rot or expire. Damage from floods. Wear and tear. Losses you cannot show with a list of what was lost and what it was worth.

            How to claim. Report a break-in or fire to the police (or the fire brigade) and tell Kato on WhatsApp within 14 days. We need the police or fire brigade report, a list of what was lost or damaged with its value, and photos of the shop. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 15 working days of getting the papers.
            """),
        new("Nile Shield Small Business Shop Cover, Policy Wording", MicroinsuranceCatalog.ShopLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Small Business Shop Cover, microinsurance property policy wording, demo version 1.0.

            Section 1, the cover. We cover stock in trade, shelves, fittings and business equipment kept at the insured premises against loss or damage by fire, lightning or explosion, and against theft involving forcible and violent entry into or exit from the premises. The sum insured is up to UGX 8,000,000 a year, within the microinsurance limit of UGX 10,000,000. An excess of UGX 100,000 applies to each claim.

            Section 2, premium and grace period. The premium is UGX 15,000 a month. There is a grace period of 15 days.

            Section 3, waiting period. Cover starts 7 days after the first premium is paid.

            Section 4, exclusions. We do not pay for: theft without forcible and violent entry; cash, cheques or mobile money float; perishable or expired goods; flood; wear and tear or gradual deterioration; loss discovered only at stock-taking; losses that cannot be supported by a stock list, receipts or records.

            Section 5, claims. Report the loss to the police or fire brigade and to us within 14 days. We need the police or fire brigade report, a list of what was lost or damaged with its value, and photos of the premises. We settle within 15 working days of complete documents and pay by mobile money.

            Section 6, complaints. A person reviews any complaint within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Small Business Shop Cover, Cover Levels", MicroinsuranceCatalog.ShopLine, KnowledgeDocumentType.ProductOverview, """
            Choosing your level of Small Business Shop Cover. Choose the level that matches what your stock, shelves and equipment are worth on a normal day, so a loss is paid in full.

            Level 1: pays up to UGX 2,000,000 a year, for UGX 5,000 a month. Suits a kiosk or a small shop.

            Level 2: pays up to UGX 4,000,000 a year, for UGX 9,000 a month. Suits a shop or boutique with a moderate amount of stock.

            Level 3: pays up to UGX 8,000,000 a year, for UGX 15,000 a month. Suits a well-stocked boutique, hardware shop, pharmacy or salon with equipment.

            If your stock is worth more than UGX 8,000,000, Level 3 pays up to UGX 8,000,000 and a person from Nile Shield can talk to you about a larger business policy. For every level you pay the first UGX 100,000 of each claim yourself, and cover starts 7 days after you join.
            """),
        new("Nile Shield Goods in Transit, Key Information", MicroinsuranceCatalog.TransitLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Goods in Transit. This is a microinsurance product for traders who carry stock from their suppliers, for example from Kikuubo or Owino, to their shop or stall by boda, taxi or pick-up, and for anyone who delivers goods to customers. It is a sample product being tested with customers.

            What it pays. It pays for the goods you are carrying if they are stolen on the way, or destroyed or damaged in a road accident, from the moment they leave the supplier until they reach your shop, stall or customer. Level 1 pays up to UGX 1,000,000 per trip for UGX 3,000 a month. Level 2 pays up to UGX 2,000,000 per trip for UGX 5,000 a month.

            When cover starts (waiting period). Cover starts 7 days after you join.

            Grace period. If you miss a payment, you have 15 days to pay before your cover stops.

            What it does not pay for. Goods left unattended on a vehicle. Cash or mobile money float. Goods that spoil because of delay or heat. Goods carried on a vehicle with an unlicensed driver or rider. Losses you cannot show with a receipt or delivery note.

            How to claim. Report the theft or accident to the police and tell Kato on WhatsApp within 7 days. We need the police report and the supplier's receipt or delivery note. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 10 working days of getting the papers.
            """),
        new("Nile Shield Goods in Transit, Policy Wording", MicroinsuranceCatalog.TransitLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Goods in Transit, microinsurance policy wording, demo version 1.0.

            Section 1, the cover. We cover goods belonging to the insured, or held by the insured for sale, while in transit by road within Uganda between the supplier's premises, the insured's premises and the insured's customers, against theft and against loss or damage caused by a road accident, fire or overturning of the carrying vehicle. The limit per trip is UGX 1,000,000 at Level 1 or UGX 2,000,000 at Level 2, within the microinsurance limit of UGX 10,000,000.

            Section 2, premium and grace period. The premium is UGX 3,000 a month at Level 1 and UGX 5,000 a month at Level 2. There is a grace period of 15 days.

            Section 3, waiting period. Cover starts 7 days after the first premium is paid.

            Section 4, exclusions. We do not pay for: goods left unattended; cash, cheques or mobile money float; spoilage, delay or temperature damage; goods carried by an unlicensed driver or rider; losses not supported by a receipt or delivery note.

            Section 5, claims. Report to the police and to us within 7 days. We need the police report and the supplier's receipt or delivery note. We settle within 10 working days of complete documents and pay by mobile money.

            Section 6, complaints. A person reviews any complaint within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Goods in Transit, How to Join, Pay and Claim", MicroinsuranceCatalog.TransitLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Goods in Transit. This is a microinsurance product being tested with customers. Traders who carry stock from suppliers, and anyone who delivers goods, can join. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area, and choose the level.

            How to pay. Level 1 costs UGX 3,000 a month and Level 2 costs UGX 5,000 a month, paid by mobile money on MTN or Airtel. The first payment starts the cover, which begins 7 days after you join.

            How to make a claim. Tell Kato on WhatsApp what happened, when and where. Kato reads it back to you, and when you confirm, the claim goes straight to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share the police report and the supplier's receipt or delivery note. Never send papers or ID in the WhatsApp chat itself.

            How claims are paid. When a claim is approved, the money is paid to you by mobile money on the phone number on your policy, usually within 10 working days of receiving all the papers.
            """),
        new("Nile Shield Mama Care Maternity, Key Information", MicroinsuranceCatalog.MaternityLine, KnowledgeDocumentType.ProductOverview, """
            Key information: Nile Shield Mama Care Maternity. This is a microinsurance product for mothers and families. It is a sample product being tested with customers.

            What it pays. It pays towards the cost of giving birth at a registered hospital or health centre in Uganda: up to UGX 600,000 for a normal delivery, or up to UGX 2,000,000 for a caesarean section. It can be paid to you or straight to the hospital. If the bill is higher, you pay the rest.

            What it costs. UGX 12,000 a month, paid by mobile money.

            When cover starts (waiting period). Childbirth is covered once you have had the cover for 10 months, so it is best taken before or at the very start of a pregnancy. Treatment after an accident during pregnancy is covered from the day you join.

            Grace period. If you miss a payment, you have 30 days to pay before your cover stops.

            What it does not pay for. A pregnancy that had already started before you joined, for childbirth itself. Delivery at home or at an unregistered clinic. Fertility treatment. Cosmetic procedures.

            How to claim. Tell Kato on WhatsApp after the birth, within 30 days. We need the hospital discharge summary or birth notification and the hospital bill or receipts. Send papers only through the secure link we give you, not in the chat. We pay by mobile money within 10 working days of getting the papers.
            """),
        new("Nile Shield Mama Care Maternity, Policy Wording", MicroinsuranceCatalog.MaternityLine, KnowledgeDocumentType.PolicyWording, """
            Nile Shield Mama Care Maternity, microinsurance policy wording, demo version 1.0.

            Section 1, the cover. When the insured mother gives birth at a registered hospital or health centre in Uganda after the waiting period, we pay the reasonable cost of the delivery up to UGX 600,000 for a normal delivery or UGX 2,000,000 for a caesarean section, within the microinsurance limit of UGX 10,000,000.

            Section 2, premium and grace period. The premium is UGX 12,000 a month. There is a grace period of 30 days.

            Section 3, waiting period. Childbirth is covered after 10 months of continuous cover. Accidental injury during pregnancy is covered from the first day.

            Section 4, exclusions. We do not pay for: childbirth from a pregnancy that began before cover started; home delivery or delivery at an unregistered facility; fertility treatment; cosmetic procedures.

            Section 5, claims. Report within 30 days of the birth. We need the discharge summary or birth notification and the hospital bill or receipts. We settle within 10 working days of complete documents and pay by mobile money, to the insured or to the hospital.

            Section 6, complaints. A person reviews any complaint within 14 days. You may also complain to the Insurance Regulatory Authority of Uganda.
            """),
        new("Nile Shield Mama Care Maternity, How to Join, Pay and Claim", MicroinsuranceCatalog.MaternityLine, KnowledgeDocumentType.ProductOverview, """
            How to join Nile Shield Mama Care Maternity. This is a microinsurance product being tested with customers. Women aged 18 to 45 can join, or a husband or partner can take it for the mother of their child. Joining takes a few minutes on WhatsApp: tell Kato your full name, your work and your area.

            How to pay. It costs UGX 12,000 a month, paid by mobile money on MTN or Airtel. Childbirth is covered once you have had the cover for 10 months.

            How to make a claim. Tell Kato on WhatsApp after the birth. Kato reads it back to you, and when you confirm, the claim goes to the Nile Shield claims team with a claim reference. Kato then sends you a secure link to share the discharge summary or birth notification and the hospital bill. Never send papers in the WhatsApp chat itself.

            How claims are paid. When a claim is approved, the money is paid by mobile money to the phone number on the policy, or straight to the hospital, usually within 10 working days of receiving all the papers.
            """),
    ];
}

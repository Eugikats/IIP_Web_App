using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Distribution;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.DemoSeeding;

/// <summary>
/// Builds a realistic, deeply varied demo dataset for a Ugandan composite insurer
/// ("Nile Shield Assurance") spanning Motor, Medical, Life, General, and Agriculture,
/// so a reviewer can see claims in every workflow state, with real Uganda-grounded
/// premium bands, real per-line document checklists, and enough narrative detail for
/// AI triage/summarisation to make credible, checkable inferences.
///
/// Every record is written directly through the same repositories the real API uses
/// (no shortcuts, no separate seed-only storage), the only difference from normal API
/// traffic is that CreatedAt/UpdatedAt/SlaDueAt are set explicitly here so the dataset
/// can show genuine age and SLA variety, which CaseIntakeService's live "now" semantics
/// cannot produce. This service must only ever run behind the Demo/local-profile gate
/// enforced by the calling endpoint (see DemoSeedEndpoints).
/// </summary>
public sealed class DemoSeedService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IApprovalRepository approvalRepository,
    IAuditRepository auditRepository,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository,
    ICoverageRepository coverageRepository,
    IBranchRepository branchRepository,
    IProducerRepository producerRepository,
    IProductRepository productRepository)
{
    private const int Seed = 42;

    /// <param name="assignedToUserId">
    /// Who the seeded work is currently assigned to. Defaults to <paramref name="seededByUserId"/>
    /// for callers that don't care, but offline/demo runs pass the identity the operator actually
    /// signs in as, otherwise every seeded claim is owned by a synthetic id nobody signs in as, and
    /// "My Work" (which filters on assignment) and the notification feed (which is per-recipient)
    /// both render empty on a freshly seeded workspace. Assignment is current state, so pointing it
    /// at the demo user is truthful; provenance fields (created-by, audit actor, who approved what)
    /// deliberately stay on <paramref name="seededByUserId"/>, the system did seed them, and a demo
    /// user must never appear in the audit trail as having taken an action they did not take.
    /// </param>
    public async Task<DemoSeedResult> SeedAsync(
        TenantScope scope,
        string seededByUserId,
        string? assignedToUserId = null,
        CancellationToken cancellationToken = default)
    {
        var rng = new Random(Seed);
        var now = DateTimeOffset.UtcNow;
        var assignee = string.IsNullOrWhiteSpace(assignedToUserId) ? seededByUserId : assignedToUserId.Trim();

        var branches = BuildBranches(scope);
        foreach (var branch in branches)
        {
            await branchRepository.UpsertAsync(branch, cancellationToken);
        }

        var producers = BuildProducers(scope, branches, rng);
        foreach (var producer in producers)
        {
            await producerRepository.UpsertAsync(producer, cancellationToken);
        }

        // A seeded claim needs a real, published Product to pin against, otherwise every claim
        // shows "no configured claim process" no matter how thorough the rest of the dataset is.
        // Idempotent: a product already published for a line is reused, never duplicated.
        var products = await EnsureDemoProductsAsync(scope, seededByUserId, now, cancellationToken);
        var newProductCount = products.Values.Count(p => p.IsNew);

        var plan = BuildSeedPlan(rng);
        var customerCount = 0;
        var policyCount = 0;
        var caseCount = 0;
        var documentCount = 0;
        var approvalCount = 0;
        var auditCount = 0;
        var heroCaseIds = new List<Guid>();

        for (var i = 0; i < plan.Count; i++)
        {
            var slot = plan[i];
            var line = ProductLines[slot.LineIndex];

            var customer = BuildCustomer(scope, i, line, rng);
            await customerRepository.UpsertAsync(customer, cancellationToken);
            customerCount++;

            var branch = branches[rng.Next(branches.Count)];
            var (policy, coverage) = BuildPolicy(scope, i, line, branch, customer, rng, now);
            await policyRepository.UpsertAsync(policy, cancellationToken);
            await coverageRepository.UpsertAsync(coverage, cancellationToken);
            policyCount++;

            var ageDays = AgeDaysForStatus(slot.Status, rng);
            var createdAt = now.AddDays(-ageDays);
            var incidentDate = DateOnly.FromDateTime(createdAt.AddDays(-rng.Next(0, 4)).UtcDateTime);
            var slaDueAt = ComputeSlaDueAt(createdAt, slot.SlaState, now, rng);
            var completeness = CompletenessForStatus(slot.Status, rng);
            var risk = RiskForLine(line, rng);
            var priority = PriorityForRisk(risk, slot.SlaState);
            var correlationId = $"demo-seed-{i:D3}-{Guid.NewGuid():N}";
            var caseId = Guid.NewGuid();

            var product = products[line.Name];
            var claimCase = new ClaimCase
            {
                CaseId = caseId,
                Scope = scope,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                PolicyNumber = policy.PolicyNumber,
                ProductLine = line.Name,
                ProductId = product.ProductId,
                ProductVersion = product.Version,
                ClaimType = line.ClaimTypes[rng.Next(line.ClaimTypes.Length)],
                Channel = Channels[rng.Next(Channels.Length)],
                IncidentDate = incidentDate,
                IncidentDescription = line.Narratives[rng.Next(line.Narratives.Length)],
                MotorFnolDetails = line.Name == "Motor" ? BuildMotorFnol(rng) : null,
                Status = slot.Status,
                CompletenessScore = completeness,
                RiskLevel = risk,
                Priority = priority,
                SlaDueAt = slaDueAt,
                AssignedUserId = slot.Status is CaseStatus.New ? null : assignee,
                AssignedTeam = line.ReviewTeam,
                CreatedByUserId = seededByUserId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddHours(rng.Next(1, Math.Max(2, ageDays * 24))),
                CorrelationId = correlationId
            };

            await caseRepository.SaveAsync(claimCase, cancellationToken);
            caseCount++;

            await auditRepository.WriteAsync(
                AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), seededByUserId, correlationId) with
                {
                    Timestamp = createdAt
                },
                cancellationToken);
            auditCount++;

            if (slot.Status is not CaseStatus.New)
            {
                await auditRepository.WriteAsync(
                    new AuditEvent(
                        Guid.NewGuid(), scope, caseId, null, seededByUserId, ActorType.User,
                        "case.assigned", createdAt.AddHours(2), "New", "Assigned",
                        Array.Empty<string>(), "Mock", null, "accepted", null, null, correlationId),
                    cancellationToken);
                auditCount++;
            }

            var documents = BuildDocuments(scope, caseId, line, slot.Status, seededByUserId, createdAt, correlationId, rng);
            foreach (var document in documents)
            {
                await documentRepository.SaveAsync(document, cancellationToken);
                documentCount++;
            }

            if (documents.Count > 0)
            {
                await auditRepository.WriteAsync(
                    new AuditEvent(
                        Guid.NewGuid(), scope, caseId, documents[0].DocumentId.ToString(), seededByUserId,
                        ActorType.Agent, "document.processed", createdAt.AddHours(3), "Uploaded", "Processed",
                        Array.Empty<string>(), "AzureDocumentIntelligence", 0.9m, "accepted", null, null, correlationId),
                    cancellationToken);
                auditCount++;
            }

            if (slot.Status is CaseStatus.PendingApproval or CaseStatus.Escalated or CaseStatus.ReadyForExport)
            {
                var approval = BuildApproval(
                    scope, caseId, line, slot.Status, seededByUserId, assignee,
                    assignToReviewer: approvalCount % 2 == 0, createdAt, correlationId, rng);
                await approvalRepository.SaveAsync(approval, cancellationToken);
                approvalCount++;

                await auditRepository.WriteAsync(
                    new AuditEvent(
                        Guid.NewGuid(), scope, caseId, null, seededByUserId, ActorType.Agent,
                        "approval.created", createdAt.AddHours(4), "InReview", slot.Status.ToString(),
                        Array.Empty<string>(), "AzureOpenAI", 0.82m, "accepted", approval.ApprovalId.ToString(), null, correlationId),
                    cancellationToken);
                auditCount++;
            }

            if (slot.IsHero)
            {
                heroCaseIds.Add(caseId);
            }
        }

        return new DemoSeedResult(
            InsurerName: "Nile Shield Assurance (Uganda) Ltd",
            Products: newProductCount,
            Customers: customerCount,
            Policies: policyCount,
            Cases: caseCount,
            Documents: documentCount,
            Approvals: approvalCount,
            AuditEvents: auditCount,
            HeroCaseIds: heroCaseIds);
    }

    /// <summary>Ensures a Published product, with a real claim process and document matrix, exists
    /// for every seeded product line. A product already published for that line (ProductClass) is
    /// reused rather than duplicated, so reseeding an already-configured tenant is a no-op here.
    /// PublishSubmittedByUserId/PublishApprovedByUserId are two distinct synthetic system actors
    /// rather than the operator who triggered seeding, the same provenance rule as the rest of this
    /// file: a demo user must never appear to have taken an action they did not take, and a real
    /// product publish always needs a second person distinct from whoever submitted it.</summary>
    private async Task<IReadOnlyDictionary<string, (Guid ProductId, int Version, bool IsNew)>> EnsureDemoProductsAsync(
        TenantScope scope, string seededByUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken);
        var publishedByLine = existing
            .Where(p => p.Status == ProductStatus.Published)
            .GroupBy(p => p.ProductClass, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First(), StringComparer.OrdinalIgnoreCase);

        var catalogDefaults = DefaultDocumentTypeCatalog.Defaults()
            .ToDictionary(entry => entry.BuiltInType, entry => entry);

        var result = new Dictionary<string, (Guid ProductId, int Version, bool IsNew)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ProductLines)
        {
            if (publishedByLine.TryGetValue(line.Name, out var current))
            {
                result[line.Name] = (current.ProductId, current.Version, false);
                continue;
            }

            var matrix = line.RequiredDocs
                .Select(type => ToDocumentRequirement(type, isRequired: true, catalogDefaults))
                .Concat(line.OptionalDocs.Select(type => ToDocumentRequirement(type, isRequired: false, catalogDefaults)))
                .ToArray();

            var product = new Product
            {
                ProductId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Scope = scope,
                Version = 1,
                Name = $"{line.Name} Cover",
                ProductClass = line.Name,
                IraApprovalReference = MicroinsuranceDemoSeeder.DemoApprovalMarker,
                IraApprovalEffectiveDate = DateOnly.FromDateTime(now.AddYears(-1).UtcDateTime),
                ApprovedWordingKnowledgeDocumentId = null,
                Status = ProductStatus.Published,
                Stages = DefaultClaimStageLibrary.Default(),
                DocumentMatrix = matrix,
                UpdatedByUserId = seededByUserId,
                UpdatedAt = now,
                ChangeReason = "Seeded demo product, so a seeded claim has a real configured claim process and document matrix to pin against.",
                PublishSubmittedByUserId = "system:demo-seed",
                PublishSubmittedAt = now.AddDays(-30),
                PublishApprovedByUserId = "system:demo-seed-reviewer",
                PublishApprovedAt = now.AddDays(-29),
            };

            await productRepository.SaveAsync(product, cancellationToken);
            result[line.Name] = (product.ProductId, product.Version, true);
        }

        return result;
    }

    private static ProductDocumentRequirement ToDocumentRequirement(
        DocumentType documentType,
        bool isRequired,
        IReadOnlyDictionary<DocumentType, (string Key, string CustomerLabel, string Description, DocumentType BuiltInType)> catalogDefaults)
    {
        var entry = catalogDefaults[documentType];
        return new ProductDocumentRequirement(
            entry.Key,
            entry.CustomerLabel,
            entry.Description,
            isRequired,
            DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest,
            IsRegulatoryOrWordingMandated: false);
    }

    // ---------- Reference data ----------

    private static readonly string[] Channels = ["WebPortal", "WhatsApp", "Email", "Branch", "CallCentre"];

    private sealed record ProductLineTemplate(
        string Name,
        string[] ClaimTypes,
        string[] Narratives,
        string ReviewTeam,
        string[] CoverTypes,
        Func<Random, decimal> PremiumFactory,
        Func<Random, decimal> SumInsuredFactory,
        DocumentType[] RequiredDocs,
        DocumentType[] OptionalDocs);

    // Declared ahead of ProductLines (below) purely so nullable-flow analysis can see
    // these are always assigned before the lambdas that close over them are invoked.
    private static readonly decimal[] MedicalPremiumTiers = [600_000m, 1_000_000m, 2_000_000m, 2_500_000m, 4_500_000m, 7_000_000m];
    private static readonly decimal[] MedicalCoverTiers = [20_000_000m, 70_000_000m, 120_000_000m, 40_000_000m];
    private static readonly decimal[] LifePremiumBands = [300_000m, 600_000m, 1_200_000m, 2_000_000m, 3_000_000m];
    private static readonly decimal[] LifeSumAssuredBands = [10_000_000m, 25_000_000m, 50_000_000m, 100_000_000m, 200_000_000m];
    private static readonly decimal[] AgricultureSumInsuredBands = [3_000_000m, 8_000_000m, 15_000_000m, 30_000_000m];

    private static readonly ProductLineTemplate[] ProductLines =
    [
        new(
            "Motor",
            ["Accident", "Theft", "Fire", "WindscreenDamage", "ThirdPartyLiability"],
            [
                "Collision at Jinja Road roundabout during heavy rain; front bumper and headlight damaged.",
                "Vehicle broken into overnight at Entebbe Road parking; car radio and side mirror stolen.",
                "Rear-ended at Clock Tower junction in traffic; boot and rear bumper crushed.",
                "Vehicle hit a pothole on Masaka Road, blowing out two tyres and damaging the suspension.",
                "Third-party pedestrian injured after driver swerved to avoid a boda-boda on Kampala Road.",
                "Windscreen cracked by a stone thrown up from a passing truck on the Northern Bypass."
            ],
            "Motor Claims Review",
            ["Comprehensive", "Third Party Only", "Third Party Fire & Theft"],
            rng => RoundToNearest(RandomVehicleValue(rng) * (0.04m + (decimal)rng.NextDouble() * 0.06m), 1000m),
            RandomVehicleValue,
            [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence],
            [DocumentType.Invoice, DocumentType.GarageEstimate, DocumentType.CustomerId]),
        new(
            "Medical",
            ["Inpatient", "Outpatient", "Maternity", "Dental", "Optical"],
            [
                "Member admitted at Nakasero Hospital for three nights for acute malaria treatment.",
                "Outpatient consultation and lab tests at International Hospital Kampala for persistent fever.",
                "Maternity delivery cover claimed at Case Clinic; mother and newborn both covered under the family plan.",
                "Root canal and crown procedure claimed under the dental rider at a Kampala dental clinic.",
                "Emergency appendectomy performed at Mulago Private Wing; five-day admission.",
                "Routine eye examination and prescription glasses claimed under the optical benefit."
            ],
            "Medical Claims Review",
            ["Silver", "Gold", "Platinum", "Family Plan"],
            rng => MedicalPremiumTiers[rng.Next(MedicalPremiumTiers.Length)],
            rng => MedicalCoverTiers[rng.Next(MedicalCoverTiers.Length)],
            [DocumentType.ClaimForm, DocumentType.MedicalReport, DocumentType.Invoice],
            [DocumentType.CustomerId]),
        new(
            "Life",
            ["DeathClaim", "EndowmentMaturity", "GroupLifeClaim", "LastExpenseClaim"],
            [
                "Policyholder passed away following a short illness; beneficiary claim lodged by next of kin.",
                "Endowment policy reached its 15-year maturity date; policyholder claiming the maturity value.",
                "Group life claim lodged by an employer following the death of an insured staff member.",
                "Last-expense cover claimed to support funeral costs following the death of the policyholder's spouse."
            ],
            "Life Claims Review",
            ["Term Life", "Endowment", "Group Life", "Last Expense"],
            rng => LifePremiumBands[rng.Next(LifePremiumBands.Length)],
            rng => LifeSumAssuredBands[rng.Next(LifeSumAssuredBands.Length)],
            [DocumentType.ClaimForm, DocumentType.CustomerId],
            [DocumentType.Invoice]),
        new(
            "General",
            ["FireDamage", "Burglary", "MarineCargoLoss", "PublicLiability", "BusinessInterruption"],
            [
                "Warehouse fire in Nakawa Industrial Area damaged stored inventory and racking.",
                "Break-in at a Kampala retail branch overnight; till contents and stock stolen.",
                "Cargo container damaged in transit from Mombasa port; goods claimed under marine cover.",
                "Customer slipped on a wet floor at an insured supermarket; public liability claim lodged.",
                "Business interruption claim following a fire that closed a manufacturing line for six weeks."
            ],
            "General Claims Review",
            ["Fire & Perils", "Burglary", "Marine Cargo", "Public Liability", "Business Interruption"],
            rng => RoundToNearest(RandomSumInsuredGeneral(rng) * (0.001m + (decimal)rng.NextDouble() * 0.004m), 10000m),
            RandomSumInsuredGeneral,
            [DocumentType.ClaimForm, DocumentType.PhotoEvidence],
            [DocumentType.Invoice, DocumentType.PoliceAbstract]),
        new(
            "Agriculture",
            ["CropLoss", "LivestockMortality", "DroughtIndexTrigger", "FloodDamage", "DiseaseOutbreak"],
            [
                "Maize crop in Masaka District damaged by prolonged drought during the flowering stage.",
                "Dairy cow died of suspected East Coast Fever; livestock cover claimed under UAIS.",
                "Weather index station recorded rainfall below the drought trigger threshold for the insured season.",
                "Flash flooding in the Mbale area submerged a smallholder's bean crop shortly before harvest.",
                "Disease outbreak in a poultry flock in Wakiso District triggered a livestock mortality claim."
            ],
            "Agriculture Claims Review",
            ["Multi-Peril Crop", "Livestock", "Weather Index", "Aquaculture"],
            rng => AgriculturePremiumAfterSubsidy(rng),
            rng => AgricultureSumInsuredBands[rng.Next(AgricultureSumInsuredBands.Length)],
            [DocumentType.ClaimForm, DocumentType.PhotoEvidence],
            [DocumentType.GarageEstimate])
    ];

    /// <summary>
    /// Math.Round(decimal, int) only accepts 0-28 (decimal places after the point), it has
    /// no "round to the nearest power of 10" mode like some other rounding APIs. This is
    /// that mode, used throughout for tidy UGX figures (nearest 100/1,000/10,000).
    /// </summary>
    private static decimal RoundToNearest(decimal value, decimal nearest) =>
        Math.Round(value / nearest, 0, MidpointRounding.AwayFromZero) * nearest;

    private static decimal RandomVehicleValue(Random rng) => 15_000_000m + (decimal)rng.Next(0, 75) * 1_000_000m;

    private static decimal RandomSumInsuredGeneral(Random rng) => 50_000_000m + (decimal)rng.Next(0, 4950) * 1_000_000m;

    /// <summary>
    /// UAIS premium after the government subsidy: 30% for large-scale farmers,
    /// 50% for smallholders, 80% in high-risk/disaster-prone areas.
    /// </summary>
    private static decimal AgriculturePremiumAfterSubsidy(Random rng)
    {
        var grossRate = 0.03m + (decimal)rng.NextDouble() * 0.05m; // 3-8% of sum insured, typical multi-peril crop rating
        var sumInsured = AgricultureSumInsuredBands[rng.Next(AgricultureSumInsuredBands.Length)];
        var gross = sumInsured * grossRate;
        var subsidy = rng.Next(3) switch { 0 => 0.30m, 1 => 0.50m, _ => 0.80m };
        return RoundToNearest(gross * (1 - subsidy), 100m);
    }

    private static readonly (string Region, string Name)[] BranchDefinitions =
    [
        ("Central", "Kampala Head Office"),
        ("Central", "Entebbe Branch"),
        ("Eastern", "Jinja Branch"),
        ("Western", "Mbarara Branch"),
        ("Northern", "Gulu Branch")
    ];

    // Note: IBranchRepository/IProducerRepository/IPolicyRepository/ICoverageRepository/
    // ICustomerRepository all Guid.Parse() their string Id fields internally (the EF
    // implementations key the underlying table on a Guid column), even though the domain
    // contracts type them as plain string. Human-readable identifiers therefore cannot be
    // used as the *Id*, Guid.NewGuid().ToString() is required there; readable codes still
    // appear elsewhere (PolicyNumber, CustomerName) for anything actually shown in the UI.
    private static IReadOnlyList<Branch> BuildBranches(TenantScope scope) =>
        BranchDefinitions
            .Select((b, i) => new Branch(Guid.NewGuid().ToString(), scope.TenantId, scope.WorkspaceId, b.Name, b.Region))
            .ToArray();

    private static readonly string[] ProducerTypes = ["Direct", "Broker", "BancassuranceAgent", "Corporate"];

    private static IReadOnlyList<Producer> BuildProducers(TenantScope scope, IReadOnlyList<Branch> branches, Random rng) =>
        Enumerable.Range(1, 8)
            .Select(i => new Producer(
                Guid.NewGuid().ToString(),
                scope.TenantId,
                scope.WorkspaceId,
                branches[rng.Next(branches.Count)].BranchId,
                $"{ProducerNamePrefixes[rng.Next(ProducerNamePrefixes.Length)]} {i}",
                ProducerTypes[rng.Next(ProducerTypes.Length)]))
            .ToArray();

    private static readonly string[] ProducerNamePrefixes = ["Nile Shield Direct", "Pearl Brokers", "Kampala Bancassurance Desk", "Equator Risk Partners"];

    private static readonly string[] FirstNames =
    [
        "Aisha", "Brian", "Christine", "David", "Esther", "Fred", "Grace", "Herbert",
        "Immaculate", "Joseph", "Kevin", "Lillian", "Moses", "Nakato", "Opio", "Patricia",
        "Ronald", "Sarah", "Timothy", "Victoria", "Wasswa", "Yusuf", "Zainab", "Allan"
    ];

    private static readonly string[] LastNames =
    [
        "Achieng", "Byaruhanga", "Kirabo", "Mugisha", "Nabirye", "Okello", "Ssemwogerere",
        "Tumusiime", "Wanyama", "Kato", "Namuli", "Bwambale", "Kyomuhendo", "Atim", "Rukundo"
    ];

    /// <summary>
    /// Adds policyholders who hold an active policy and have never filed a claim, so the claimant
    /// portal's first-claim journey can be demonstrated. The main seed gives every customer a claim,
    /// which leaves nobody who could file a first one: the portal only offers "start a new claim" to
    /// a verified policyholder with no claim on that policy.
    ///
    /// Safe to run more than once. Names and emails are derived from a fixed seed per slot, and a
    /// policyholder whose email already exists is returned as it is rather than created again.
    /// Must only run behind the same Demo gate as <see cref="SeedAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<DemoPolicyholder>> SeedPolicyholdersWithoutClaimsAsync(
        TenantScope scope,
        int count,
        CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 5);
        var existingCustomers = await customerRepository.ListAsync(scope, cancellationToken);
        var existingPolicies = await policyRepository.ListAsync(scope, cancellationToken);

        var branchId = existingPolicies
            .Select(policy => policy.BranchId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        if (branchId is null)
        {
            var branches = BuildBranches(scope);
            foreach (var branch in branches)
            {
                await branchRepository.UpsertAsync(branch, cancellationToken);
            }

            branchId = branches[0].BranchId;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<DemoPolicyholder>();

        for (var slot = 0; slot < count; slot++)
        {
            var index = 900 + slot;
            var rng = new Random(Seed + index);
            var line = ProductLines[slot % ProductLines.Length];
            var customer = BuildCustomer(scope, index, line, rng);

            var existing = existingCustomers.FirstOrDefault(item =>
                string.Equals(item.Email, customer.Email, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var existingPolicy = existingPolicies.FirstOrDefault(policy => policy.CustomerId == existing.CustomerId);
                if (existingPolicy is not null)
                {
                    results.Add(new DemoPolicyholder(existing.FullName, existing.Email ?? string.Empty, existing.Phone ?? string.Empty,
                        existingPolicy.PolicyNumber, existingPolicy.ProductLine, Created: false));
                    continue;
                }

                customer = existing;
            }
            else
            {
                await customerRepository.UpsertAsync(customer, cancellationToken);
            }

            // In force today, so the demonstration is of an ordinary claim, not an expired-cover edge case.
            var effectiveFrom = today.AddDays(-45);
            var policyId = Guid.NewGuid().ToString();
            var policyNumber = $"NSA/{line.Name.ToUpperInvariant()[..2]}/{effectiveFrom.Year}/{index + 1:D5}";
            var premium = line.PremiumFactory(rng);
            var sumInsured = line.SumInsuredFactory(rng);
            var coverType = line.CoverTypes[rng.Next(line.CoverTypes.Length)];

            await policyRepository.UpsertAsync(
                new Policy(
                    policyId, scope.TenantId, scope.WorkspaceId, policyNumber, line.Name, coverType,
                    customer.CustomerId, branchId, effectiveFrom, effectiveFrom.AddYears(1),
                    premium, "UGX", sumInsured),
                cancellationToken);

            await coverageRepository.UpsertAsync(
                new Coverage(
                    Guid.NewGuid().ToString(), scope.TenantId, scope.WorkspaceId, policyId,
                    $"{coverType}, {line.Name}",
                    $"UGX {sumInsured:N0}",
                    $"UGX {RoundToNearest(sumInsured * 0.02m, 1000m):N0}",
                    line.Name == "Motor" ? "Insured vehicle" : line.Name == "Agriculture" ? "Insured crop/livestock" : "Insured risk",
                    $"{coverType} cover under the {line.Name} product line, sum insured UGX {sumInsured:N0}, annual premium UGX {premium:N0}."),
                cancellationToken);

            results.Add(new DemoPolicyholder(customer.FullName, customer.Email ?? string.Empty, customer.Phone ?? string.Empty,
                policyNumber, line.Name, Created: true));
        }

        return results;
    }

    /// <summary>
    /// A named policyholder for someone testing the WhatsApp assistant from their own phone: a
    /// Motor and a Medical policy in force today, and two past claims (a settled windscreen claim,
    /// a closed outpatient claim) so "what's the status of my claim?" and the claims officer's
    /// customer view both have history to show. No open claim, so the tester starts one fresh.
    ///
    /// The name and number come from the request, never from source code, and every identifier is
    /// a GUID derived from the phone number (the SQL-backed repositories store customer, policy and
    /// coverage ids as real GUIDs), so running it again updates the same records instead of adding
    /// duplicates. Must only run behind the same Demo gate as <see cref="SeedAsync"/>.
    /// </summary>
    public async Task<TestPolicyholderResult> SeedTestPolicyholderAsync(
        TenantScope scope,
        string fullName,
        string phone,
        string? email,
        string seededByUserId,
        string? assignedToUserId,
        CancellationToken cancellationToken)
    {
        var digits = PhoneNumbers.Digits(phone);
        var key = digits.Length > 9 ? digits[^9..] : digits;
        // Ids are derived from the phone, so they must also be derived from the workspace: two
        // organisations seeding the same number must never address the same rows.
        var idKey = $"{scope.TenantId}:{scope.WorkspaceId}:{key}";
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var rng = new Random(Seed + key.GetHashCode(StringComparison.Ordinal));
        var assignee = string.IsNullOrWhiteSpace(assignedToUserId) ? seededByUserId : assignedToUserId.Trim();

        var customer = new Customer(DeterministicGuid($"test-policyholder-{idKey}").ToString(), scope.TenantId, scope.WorkspaceId, fullName.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(), phone.Trim());
        await customerRepository.UpsertAsync(customer, cancellationToken);

        var existingPolicies = await policyRepository.ListAsync(scope, cancellationToken);
        var branchId = existingPolicies.Select(policy => policy.BranchId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        if (branchId is null)
        {
            var branches = BuildBranches(scope);
            foreach (var branch in branches)
            {
                await branchRepository.UpsertAsync(branch, cancellationToken);
            }

            branchId = branches[0].BranchId;
        }

        var effectiveFrom = today.AddDays(-200);
        var sequence = key[^5..];
        var motor = new Policy(
            DeterministicGuid($"test-policyholder-{idKey}-motor").ToString(), scope.TenantId, scope.WorkspaceId, $"NSA/MO/{effectiveFrom.Year}/{sequence}", "Motor",
            "Comprehensive", customer.CustomerId, branchId, effectiveFrom, effectiveFrom.AddYears(1), 2_450_000m, "UGX", 48_000_000m);
        var medical = new Policy(
            DeterministicGuid($"test-policyholder-{idKey}-medical").ToString(), scope.TenantId, scope.WorkspaceId, $"NSA/ME/{effectiveFrom.Year}/{sequence}", "Medical",
            "Gold", customer.CustomerId, branchId, effectiveFrom, effectiveFrom.AddYears(1), 2_000_000m, "UGX", 70_000_000m);

        foreach (var policy in new[] { motor, medical })
        {
            await policyRepository.UpsertAsync(policy, cancellationToken);
            await coverageRepository.UpsertAsync(
                new Coverage(
                    DeterministicGuid($"{policy.PolicyId}-coverage").ToString(), scope.TenantId, scope.WorkspaceId, policy.PolicyId,
                    $"{policy.CoverType}, {policy.ProductLine}",
                    $"UGX {policy.SumInsured:N0}",
                    $"UGX {RoundToNearest(policy.SumInsured!.Value * 0.02m, 1000m):N0}",
                    policy.ProductLine == "Motor" ? "Insured vehicle" : "Insured member and family",
                    $"{policy.CoverType} cover under the {policy.ProductLine} product line, sum insured UGX {policy.SumInsured:N0}, annual premium UGX {policy.AnnualPremium:N0}."),
                cancellationToken);
        }

        var pastClaims = new[]
        {
            (Slot: "claim-1", Policy: motor, Line: ProductLines[0], ClaimType: "WindscreenDamage", Status: CaseStatus.Settled, AgeDays: 95, ClosedAfterDays: 21,
                Description: "Windscreen cracked by a stone thrown up from a passing truck on the Northern Bypass on the way to work.", ClaimedAmount: 1_850_000m),
            (Slot: "claim-2", Policy: medical, Line: ProductLines[1], ClaimType: "Outpatient", Status: CaseStatus.Closed, AgeDays: 40, ClosedAfterDays: 12,
                Description: "Outpatient consultation and lab tests at International Hospital Kampala for persistent fever, treated for malaria.", ClaimedAmount: 420_000m),
        };

        var results = new List<TestPolicyholderClaim>();
        foreach (var claim in pastClaims)
        {
            var caseId = DeterministicGuid($"test-policyholder-{idKey}-{claim.Slot}");
            var createdAt = now.AddDays(-claim.AgeDays);
            var correlationId = $"test-policyholder-{idKey}-{claim.Slot}";
            var alreadySeeded = await caseRepository.GetAsync(caseId, cancellationToken) is not null;

            var claimCase = new ClaimCase
            {
                CaseId = caseId,
                Scope = scope,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                PolicyNumber = claim.Policy.PolicyNumber,
                ProductLine = claim.Policy.ProductLine,
                ClaimType = claim.ClaimType,
                Channel = "WhatsApp",
                IncidentDate = DateOnly.FromDateTime(createdAt.AddDays(-1).UtcDateTime),
                IncidentDescription = claim.Description,
                MotorFnolDetails = claim.Policy.ProductLine == "Motor"
                    ? BuildMotorFnol(rng) with { DriverName = customer.FullName, AccidentLocation = "Northern Bypass", ThirdPartyInvolved = "No", InjuryIndicator = "No" }
                    : null,
                ClaimedAmount = claim.ClaimedAmount,
                Status = claim.Status,
                CompletenessScore = 100m,
                RiskLevel = "Low",
                Priority = CasePriority.Normal,
                SlaDueAt = null,
                AssignedUserId = assignee,
                AssignedTeam = claim.Line.ReviewTeam,
                CreatedByUserId = seededByUserId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddDays(claim.ClosedAfterDays),
                CorrelationId = correlationId
            };
            await caseRepository.SaveAsync(claimCase, cancellationToken);

            var documents = BuildDocuments(scope, caseId, claim.Line, claim.Status, seededByUserId, createdAt, correlationId, rng)
                .Select((document, index) => document with { DocumentId = DeterministicGuid($"{correlationId}-document-{index}") })
                .ToArray();
            foreach (var document in documents)
            {
                await documentRepository.SaveAsync(document, cancellationToken);
            }

            if (!alreadySeeded)
            {
                await auditRepository.WriteAsync(
                    AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), seededByUserId, correlationId) with { Timestamp = createdAt },
                    cancellationToken);
                await auditRepository.WriteAsync(
                    new AuditEvent(
                        Guid.NewGuid(), scope, caseId, null, seededByUserId, ActorType.User,
                        claim.Status == CaseStatus.Settled ? "case.settled" : "case.closed",
                        createdAt.AddDays(claim.ClosedAfterDays), "PendingApproval", claim.Status.ToString(),
                        Array.Empty<string>(), "Mock", null, "accepted", null, null, correlationId),
                    cancellationToken);
            }

            results.Add(new TestPolicyholderClaim(caseId, claim.Policy.PolicyNumber, claim.Policy.ProductLine, claim.Status.ToString()));
        }

        return new TestPolicyholderResult(
            customer.FullName,
            customer.Phone ?? phone,
            [new TestPolicyholderPolicy(motor.PolicyNumber, motor.ProductLine), new TestPolicyholderPolicy(medical.PolicyNumber, medical.ProductLine)],
            results);
    }

    private static Guid DeterministicGuid(string value) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static Customer BuildCustomer(TenantScope scope, int index, ProductLineTemplate line, Random rng)
    {
        var first = FirstNames[rng.Next(FirstNames.Length)];
        var last = LastNames[rng.Next(LastNames.Length)];
        var fullName = $"{first} {last}";
        var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{index}@example.ug";
        var phone = $"+2567{rng.Next(0, 10)}{rng.Next(1000000, 9999999)}";
        // The same demonstration person always gets the same record, so loading the demo again updates it
        // instead of adding another copy of them.
        return new Customer(DeterministicGuid($"{scope.TenantId}:{scope.WorkspaceId}:demo-customer:{email}").ToString(), scope.TenantId, scope.WorkspaceId, fullName, email, phone);
    }

    private static (Policy Policy, Coverage Coverage) BuildPolicy(
        TenantScope scope, int index, ProductLineTemplate line, Branch branch, Customer customer, Random rng, DateTimeOffset now)
    {
        var effectiveFrom = DateOnly.FromDateTime(now.AddDays(-rng.Next(30, 700)).UtcDateTime);
        var policyId = Guid.NewGuid().ToString();
        var policyNumber = $"NSA/{line.Name.ToUpperInvariant()[..2]}/{effectiveFrom.Year}/{index + 1:D5}";
        var premium = line.PremiumFactory(rng);
        var sumInsured = line.SumInsuredFactory(rng);
        var coverType = line.CoverTypes[rng.Next(line.CoverTypes.Length)];

        var policy = new Policy(
            policyId, scope.TenantId, scope.WorkspaceId, policyNumber, line.Name, coverType,
            customer.CustomerId, branch.BranchId, effectiveFrom, effectiveFrom.AddYears(1),
            premium, "UGX", sumInsured);

        var coverage = new Coverage(
            Guid.NewGuid().ToString(), scope.TenantId, scope.WorkspaceId, policyId,
            $"{coverType}, {line.Name}",
            $"UGX {sumInsured:N0}",
            $"UGX {RoundToNearest(sumInsured * 0.02m, 1000m):N0}",
            line.Name == "Motor" ? "Insured vehicle" : line.Name == "Agriculture" ? "Insured crop/livestock" : "Insured risk",
            $"{coverType} cover under the {line.Name} product line, sum insured UGX {sumInsured:N0}, annual premium UGX {premium:N0}.");

        return (policy, coverage);
    }

    private static MotorFnolDetails BuildMotorFnol(Random rng) =>
        new(
            VehicleRegistration: $"U{"ABCDEFGH"[rng.Next(8)]}{rng.Next(100, 999)}{"ABC"[rng.Next(3)]}",
            AccidentTime: $"{rng.Next(0, 24):D2}:{rng.Next(0, 60):D2}",
            AccidentLocation: MotorLocations[rng.Next(MotorLocations.Length)],
            DriverName: $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
            DrivingPermitNumber: $"DP{rng.Next(100000, 999999)}",
            ThirdPartyInvolved: rng.Next(2) == 0 ? "Yes" : "No",
            InjuryIndicator: rng.Next(5) == 0 ? "Yes" : "No",
            PoliceReportStatus: rng.Next(3) == 0 ? "Pending" : "Filed",
            PreferredCommunicationChannel: Channels[rng.Next(Channels.Length)],
            UploadedDocuments: null);

    private static readonly string[] MotorLocations = ["Jinja Road", "Entebbe Road", "Clock Tower Junction", "Masaka Road", "Northern Bypass", "Kampala Road"];

    // ---------- Distribution / status planning ----------

    private sealed record SeedSlot(int LineIndex, CaseStatus Status, SlaState SlaState, bool IsHero);

    private enum SlaState { Healthy, AtRisk, Breached }

    private static IReadOnlyList<SeedSlot> BuildSeedPlan(Random rng)
    {
        // Product-line weighting reflects Uganda's real claim mix: motor, medical and
        // general/fire are the three largest claim-generating lines industry-wide.
        int[] lineCounts = [18, 13, 6, 8, 5]; // Motor, Medical, Life, General, Agriculture
        var lineIndices = new List<int>();
        for (var i = 0; i < lineCounts.Length; i++)
        {
            lineIndices.AddRange(Enumerable.Repeat(i, lineCounts[i]));
        }

        // Every CaseStatus appears at least a few times so no workflow screen is empty.
        CaseStatus[] statusCycle =
        [
            CaseStatus.New, CaseStatus.New, CaseStatus.New, CaseStatus.New, CaseStatus.New,
            CaseStatus.Assigned, CaseStatus.Assigned, CaseStatus.Assigned, CaseStatus.Assigned, CaseStatus.Assigned,
            CaseStatus.InReview, CaseStatus.InReview, CaseStatus.InReview, CaseStatus.InReview, CaseStatus.InReview, CaseStatus.InReview,
            CaseStatus.MissingDocuments, CaseStatus.MissingDocuments, CaseStatus.MissingDocuments, CaseStatus.MissingDocuments, CaseStatus.MissingDocuments, CaseStatus.MissingDocuments,
            CaseStatus.ReadyForReview, CaseStatus.ReadyForReview, CaseStatus.ReadyForReview, CaseStatus.ReadyForReview, CaseStatus.ReadyForReview,
            CaseStatus.PendingApproval, CaseStatus.PendingApproval, CaseStatus.PendingApproval, CaseStatus.PendingApproval, CaseStatus.PendingApproval, CaseStatus.PendingApproval,
            CaseStatus.ReadyForExport, CaseStatus.ReadyForExport, CaseStatus.ReadyForExport,
            CaseStatus.Escalated, CaseStatus.Escalated, CaseStatus.Escalated, CaseStatus.Escalated,
            CaseStatus.Settled, CaseStatus.Settled, CaseStatus.Settled, CaseStatus.Settled, CaseStatus.Settled,
            CaseStatus.Closed, CaseStatus.Closed, CaseStatus.Closed, CaseStatus.Closed, CaseStatus.Closed
        ];

        // ~75% healthy, ~15% at-risk, ~10% breached across the open-ish population.
        var slaCycle = Enumerable.Range(0, 50)
            .Select(i => (i % 10) switch { < 7 => SlaState.Healthy, < 9 => SlaState.AtRisk, _ => SlaState.Breached })
            .OrderBy(_ => rng.Next())
            .ToArray();

        Shuffle(lineIndices, rng);
        var statuses = statusCycle.ToList();
        Shuffle(statuses, rng);

        var plan = new List<SeedSlot>(50);
        for (var i = 0; i < 50; i++)
        {
            var isHero = i is 0 or 1 or 2; // first three: reserved for the live agent-workflow demo
            var status = isHero ? CaseStatus.New : statuses[i];
            plan.Add(new SeedSlot(lineIndices[i], status, slaCycle[i], isHero));
        }

        return plan;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static int AgeDaysForStatus(CaseStatus status, Random rng) => status switch
    {
        CaseStatus.New => rng.Next(0, 2),
        CaseStatus.Assigned => rng.Next(1, 4),
        CaseStatus.InReview => rng.Next(2, 6),
        CaseStatus.MissingDocuments => rng.Next(3, 10),
        CaseStatus.ReadyForReview => rng.Next(2, 5),
        CaseStatus.PendingApproval => rng.Next(3, 8),
        CaseStatus.ReadyForExport => rng.Next(4, 9),
        CaseStatus.Escalated => rng.Next(5, 14),
        CaseStatus.Settled => rng.Next(10, 45),
        CaseStatus.Closed => rng.Next(20, 90),
        _ => rng.Next(1, 10)
    };

    private static DateTimeOffset ComputeSlaDueAt(DateTimeOffset createdAt, SlaState state, DateTimeOffset now, Random rng) => state switch
    {
        SlaState.Healthy => now.AddDays(rng.Next(2, 6)),
        SlaState.AtRisk => now.AddHours(rng.Next(4, 20)),
        SlaState.Breached => now.AddDays(-rng.Next(1, 10)),
        _ => createdAt.AddDays(3)
    };

    private static decimal CompletenessForStatus(CaseStatus status, Random rng) => status switch
    {
        CaseStatus.New => 0m,
        CaseStatus.Assigned => rng.Next(0, 30),
        CaseStatus.MissingDocuments => rng.Next(20, 70),
        CaseStatus.InReview => rng.Next(60, 100),
        CaseStatus.ReadyForReview or CaseStatus.PendingApproval or CaseStatus.ReadyForExport
            or CaseStatus.Escalated or CaseStatus.Settled or CaseStatus.Closed => 100m,
        _ => rng.Next(0, 100)
    };

    private static string RiskForLine(ProductLineTemplate line, Random rng) =>
        rng.Next(10) switch { < 6 => "Low", < 9 => "Medium", _ => "High" };

    private static CasePriority PriorityForRisk(string risk, SlaState sla) =>
        (risk, sla) switch
        {
            (_, SlaState.Breached) => CasePriority.Urgent,
            ("High", _) => CasePriority.High,
            (_, SlaState.AtRisk) => CasePriority.High,
            _ => CasePriority.Normal
        };

    // ---------- Documents ----------

    private static IReadOnlyList<CaseDocument> BuildDocuments(
        TenantScope scope, Guid caseId, ProductLineTemplate line, CaseStatus status,
        string userId, DateTimeOffset createdAt, string correlationId, Random rng)
    {
        if (status == CaseStatus.New)
        {
            return [];
        }

        // MissingDocuments cases deliberately omit one required document so the gap is real.
        var toUpload = status == CaseStatus.MissingDocuments
            ? line.RequiredDocs.Take(Math.Max(1, line.RequiredDocs.Length - 1))
            : line.RequiredDocs.Concat(line.OptionalDocs.Take(1));

        var docs = new List<CaseDocument>();
        var docIndex = 0;
        foreach (var docType in toUpload)
        {
            docIndex++;
            var uploadedAt = createdAt.AddHours(docIndex);
            var isFullyProcessed = status is not (CaseStatus.New or CaseStatus.Assigned);
            var confidence = 0.72m + (decimal)rng.NextDouble() * 0.26m;

            docs.Add(new CaseDocument
            {
                DocumentId = Guid.NewGuid(),
                CaseId = caseId,
                Scope = scope,
                OriginalFileName = $"{docType}-{docIndex}.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = rng.Next(80_000, 2_500_000),
                ObjectKey = $"demo-seed/{caseId}/{docType}-{docIndex}.pdf",
                DocumentType = docType,
                ProcessingStatus = isFullyProcessed ? DocumentProcessingStatus.ExtractionApproved : DocumentProcessingStatus.Processed,
                Classification = new DocumentClassificationResult(docType, confidence, "AzureDocumentIntelligence", uploadedAt.AddMinutes(5)),
                Extraction = new DocumentExtractionResult(
                    ExtractionFieldsFor(line, docType, rng),
                    ExtractionConfidencesFor(line, docType, rng),
                    "AzureDocumentIntelligence",
                    uploadedAt.AddMinutes(6),
                    Approved: isFullyProcessed,
                    ApprovedByUserId: isFullyProcessed ? userId : null,
                    ApprovedAt: isFullyProcessed ? uploadedAt.AddHours(1) : null),
                ValidationWarnings = confidence < 0.8m
                    ? [new DocumentValidationWarning("LOW_CONFIDENCE", "One or more extracted fields have low confidence and should be reviewed.", "Warning")]
                    : [],
                UploadedByUserId = userId,
                UploadedAt = uploadedAt,
                ProcessedAt = uploadedAt.AddMinutes(6),
                CorrelationId = correlationId
            });
        }

        return docs;
    }

    private static IReadOnlyDictionary<string, string> ExtractionFieldsFor(ProductLineTemplate line, DocumentType type, Random rng) =>
        type switch
        {
            DocumentType.ClaimForm => new Dictionary<string, string>
            {
                ["claimantName"] = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
                ["productLine"] = line.Name,
                ["dateOfLoss"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-rng.Next(1, 30))).ToString("yyyy-MM-dd")
            },
            DocumentType.MedicalReport => new Dictionary<string, string> { ["diagnosis"] = "Under physician review", ["facility"] = "Kampala facility" },
            DocumentType.PoliceAbstract => new Dictionary<string, string> { ["stationName"] = "Central Police Station", ["referenceNumber"] = $"CPS/{rng.Next(1000, 9999)}" },
            DocumentType.GarageEstimate => new Dictionary<string, string> { ["estimatedCostUgx"] = $"{rng.Next(500_000, 8_000_000):N0}" },
            _ => new Dictionary<string, string>()
        };

    private static IReadOnlyDictionary<string, decimal> ExtractionConfidencesFor(ProductLineTemplate line, DocumentType type, Random rng) =>
        ExtractionFieldsFor(line, type, rng).ToDictionary(kv => kv.Key, _ => 0.75m + (decimal)rng.NextDouble() * 0.24m);

    // ---------- Approvals ----------

    private static ApprovalRequest BuildApproval(
        TenantScope scope, Guid caseId, ProductLineTemplate line, CaseStatus status,
        string userId, string assignee, bool assignToReviewer, DateTimeOffset createdAt,
        string correlationId, Random rng)
    {
        var approvalCreatedAt = createdAt.AddHours(4);
        var isDecided = status is CaseStatus.ReadyForExport;

        return new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ApprovalType = status == CaseStatus.Escalated ? "EscalationReview" : "SupervisorApproval",
            Status = isDecided ? ApprovalStatus.Approved : status == CaseStatus.Escalated ? ApprovalStatus.Escalated : ApprovalStatus.Pending,
            RiskLevel = RiskForLine(line, rng),
            EvidenceSummary = $"AI-prepared {line.Name} claim summary with evidence references pending human sign-off. Not a final decision.",
            SourceReferences = [$"case:{caseId}"],
            RequestedByUserId = "system:agent-workflow-worker",
            // Decided approvals keep the seeder as the decider (never attribute a decision to a demo
            // operator who did not make it). Undecided ones are current state, not history: some are
            // picked up by the assignee, the rest stay in the unassigned team queue, so both "Mine"
            // and "Unassigned" in My Work have something real to show on a freshly seeded workspace.
            ReviewerUserId = isDecided ? userId : assignToReviewer ? assignee : null,
            Decision = isDecided ? ApprovalDecision.Approved : null,
            Comments = isDecided ? "Reviewed and approved for export." : null,
            ExecutedByUserId = null,
            ExecutionResult = null,
            CreatedAt = approvalCreatedAt,
            UpdatedAt = isDecided ? approvalCreatedAt.AddHours(6) : approvalCreatedAt,
            DecidedAt = isDecided ? approvalCreatedAt.AddHours(6) : null,
            ExecutedAt = null,
            CorrelationId = correlationId
        };
    }
}

/// <summary>A tester's own policyholder profile: their policies and the past claims seeded on them.</summary>
public sealed record TestPolicyholderResult(
    string FullName,
    string Phone,
    IReadOnlyList<TestPolicyholderPolicy> Policies,
    IReadOnlyList<TestPolicyholderClaim> PastClaims);

public sealed record TestPolicyholderPolicy(string PolicyNumber, string ProductLine);

public sealed record TestPolicyholderClaim(Guid CaseId, string PolicyNumber, string ProductLine, string Status);

/// <summary>A demonstration policyholder with an active policy and no claim, and whether this run created it.</summary>
public sealed record DemoPolicyholder(string FullName, string Email, string Phone, string PolicyNumber, string ProductLine, bool Created);

public sealed record DemoSeedResult(
    string InsurerName,
    int Products,
    int Customers,
    int Policies,
    int Cases,
    int Documents,
    int Approvals,
    int AuditEvents,
    IReadOnlyList<Guid> HeroCaseIds);

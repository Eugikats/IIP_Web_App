using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Products;

/// <summary>
/// A product's configuration: name, class, IRA approval, its claim process stages and its document
/// matrix, as one versioned snapshot (blueprint outcome A/C, IIP_NEXT_BUILD_PROMPT.md section 5).
/// Every save is a new, append-only version, never overwritten in place (ProductId stable across
/// versions, VersionId and Version change), the same pattern as AgentBehaviourProfile, so a claim
/// can record the exact version in force when it was filed and keep it even after the product is
/// edited or republished. A version cannot move to Published without an IRA approval reference and
/// effective date (see CanPublish), and publishing itself needs a second person, distinct from
/// whoever submitted it, enforced in ProductCommandService.ApprovePublishAsync.
/// </summary>
public sealed record Product
{
    public required Guid ProductId { get; init; }
    public required Guid VersionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required int Version { get; init; }

    public required string Name { get; init; }
    public required string ProductClass { get; init; }

    public string? IraApprovalReference { get; init; }
    public DateOnly? IraApprovalEffectiveDate { get; init; }

    /// <summary>Knowledge document id of the approved wording attached to this product. Null means
    /// no wording has been attached yet, which also blocks publish (see CanPublish).</summary>
    public string? ApprovedWordingKnowledgeDocumentId { get; init; }

    public required ProductStatus Status { get; init; }

    public required IReadOnlyList<ClaimStageDefinition> Stages { get; init; }
    public required IReadOnlyList<ProductDocumentRequirement> DocumentMatrix { get; init; }

    /// <summary>Human-confirmed facts prepared from policy wording. Every fact keeps its source evidence and extraction estimate.</summary>
    public IReadOnlyList<ProductWordingFact> WordingFacts { get; init; } = [];

    /// <summary>The kinds of claim this product handles (build-loop item 3). Part of the version
    /// snapshot like the stages and document matrix, so a claim keeps the types in force when it
    /// was filed. Empty for a product that has not configured any, in which case claims keep the
    /// free-text claim type and the built-in Motor/Medical rules, exactly as before.</summary>
    public IReadOnlyList<ClaimTypeDefinition> ClaimTypes { get; init; } = [];

    /// <summary>The questions this product asks about a claim (build-loop item 5), part of the
    /// version snapshot like the claim types and document matrix. Empty means the built-in
    /// definitions for the product line apply (see <see cref="ClaimFieldResolver"/>).</summary>
    public IReadOnlyList<ClaimFieldDefinition> ClaimFields { get; init; } = [];

    /// <summary>What the policy wording says is covered and what is excluded (build-loop item 7),
    /// each with the clause it came from. Part of the version snapshot. Informational for staff and
    /// for the assistant's grounding; it does not decide a claim.</summary>
    public IReadOnlyList<CoverageItem> CoverageItems { get; init; } = [];

    public IReadOnlyList<ClaimTypeDefinition> ActiveClaimTypes =>
        ClaimTypes.Where(type => type.Active).OrderBy(type => type.SortOrder).ToArray();

    /// <summary>Resolves what a person or a form wrote (a key, a name or a known alias, in any
    /// case and punctuation) to one of this product's claim types. Inactive types still resolve,
    /// so a claim filed before a type was retired keeps its meaning.</summary>
    public ClaimTypeDefinition? FindClaimType(string? text) => ClaimTypeMatching.Find(ClaimTypes, text);

    /// <summary>A best guess from free text such as an incident description, never a decision:
    /// the person filing still chooses. Null when nothing matches or two types tie.</summary>
    public ClaimTypeDefinition? SuggestClaimType(string? text) => ClaimTypeMatching.Suggest(ActiveClaimTypes, text);

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string ChangeReason { get; init; }

    public string? PublishSubmittedByUserId { get; init; }
    public DateTimeOffset? PublishSubmittedAt { get; init; }
    public string? PublishApprovedByUserId { get; init; }
    public DateTimeOffset? PublishApprovedAt { get; init; }
    public string? PublishRejectedByUserId { get; init; }
    public DateTimeOffset? PublishRejectedAt { get; init; }
    public string? PublishRejectionReason { get; init; }

    /// <summary>A product cannot be activated without an approval reference and its effective
    /// date. The screen must say so in plain words rather than failing silently (outcome A).</summary>
    public bool CanPublish => CanPublishWith(requireIraApproval: true);

    /// <summary>Whether the product can be submitted for publishing. The IRA reference and date are
    /// required unless the workspace has switched that rule off (TenantSettings); a claim process
    /// with at least one stage is always required.</summary>
    public bool CanPublishWith(bool requireIraApproval) =>
        Stages.Count > 0
        && (!requireIraApproval
            || (!string.IsNullOrWhiteSpace(IraApprovalReference) && IraApprovalEffectiveDate is not null));
}

public enum CoverageKind
{
    Covered,
    Excluded,
}

/// <summary>One thing the wording covers or excludes, with where it says so.</summary>
public sealed record CoverageItem(
    string Key,
    CoverageKind Kind,
    string Title,
    string Description,
    string? Clause,
    int? Page,
    string? Quote);

/// <summary>One kind of claim a product handles, for example "Own damage" or "Theft". The key is
/// stable and is what a claim stores; the name is what people read; aliases are other words people
/// use for it ("stolen" for theft) and feed matching and suggestions.</summary>
public sealed record ClaimTypeDefinition(
    string Key,
    string Name,
    string Description,
    bool Active,
    int SortOrder,
    IReadOnlyList<string>? Aliases = null);

public static class ClaimTypeMatching
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "was", "were", "with", "that", "this", "from", "have", "has", "had", "are", "not",
        "claim", "claims", "cover", "insurance", "policy", "loss", "damage", "damaged", "my", "our", "their", "his",
        "her", "its", "into", "onto", "over", "under", "out", "off", "but", "all", "any", "can", "did", "does",
    };

    /// <summary>Lower-case letters and digits only, so "Own-damage", "own damage" and "OwnDamage"
    /// are the same thing.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer, 0, length);
    }

    public static ClaimTypeDefinition? Find(IEnumerable<ClaimTypeDefinition> types, string? text)
    {
        var wanted = Normalize(text);
        if (wanted.Length == 0)
        {
            return null;
        }

        var list = types as IReadOnlyList<ClaimTypeDefinition> ?? types.ToArray();
        return list.FirstOrDefault(type => Normalize(type.Key) == wanted)
            ?? list.FirstOrDefault(type => Normalize(type.Name) == wanted)
            ?? list.FirstOrDefault(type => type.Aliases?.Any(alias => Normalize(alias) == wanted) == true);
    }

    public static ClaimTypeDefinition? Suggest(IEnumerable<ClaimTypeDefinition> activeTypes, string? text)
    {
        var exact = Find(activeTypes, text);
        if (exact is not null)
        {
            return exact;
        }

        var words = Tokens(text);
        if (words.Count == 0)
        {
            return null;
        }

        var scored = activeTypes
            .Select(type => (Type: type, Score: Score(type, words)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ToArray();
        if (scored.Length == 0 || (scored.Length > 1 && scored[0].Score == scored[1].Score))
        {
            return null;
        }

        return scored[0].Type;
    }

    private static int Score(ClaimTypeDefinition type, HashSet<string> words)
    {
        var score = 0;
        foreach (var phrase in new[] { type.Name }.Concat(type.Aliases ?? []))
        {
            var phraseTokens = Tokens(phrase);
            var hits = phraseTokens.Count(words.Contains);
            if (hits > 0)
            {
                score += hits * 2 + (hits == phraseTokens.Count ? 2 : 0);
            }
        }

        score += Tokens(type.Description).Count(words.Contains);
        return score;
    }

    private static readonly char[] Separators = " ,.;:/-_()!?".Concat(new[] { (char)39, (char)34, (char)10, (char)13, (char)9 }).ToArray();

    private static HashSet<string> Tokens(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        foreach (var raw in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.ToLowerInvariant();
            if (token.Length >= 3 && !StopWords.Contains(token))
            {
                // A crude stem (first five letters) so "stole" and "stolen" meet.
                tokens.Add(token.Length > 5 ? token[..5] : token);
            }
        }

        return tokens;
    }
}

public enum ProductStatus
{
    Draft,
    PendingPublishApproval,
    Published,
    Retired
}

/// <summary>
/// One ordered stage of a product's claim process. The IRA settlement clock depends on the claim's
/// value tier rather than a flat day count, so a stage carries either a flat
/// ServiceStandardWorkingDays or a set of ValueTierStandards, never both meaningfully at once; see
/// ResolveServiceStandardWorkingDays.
/// </summary>
public sealed record ClaimStageDefinition(
    string StageKey,
    string Name,
    string OwnerRole,
    bool IsAutomatic,
    int SortOrder,
    int? ServiceStandardWorkingDays,
    IReadOnlyList<ValueTierServiceStandard>? ValueTierStandards = null,
    // What the customer's timeline shows for this stage (build-loop item 19). All optional: the default
    // stages carry their own customer wording and milestone (see ClaimantTimeline), so a product saved
    // before these existed behaves exactly as the defaults say.
    string? CustomerLabel = null,
    bool? CustomerVisible = null,
    InsuranceIntelligence.Domain.Cases.CaseStatus? MilestoneStatus = null)
{
    public int? ResolveServiceStandardWorkingDays(decimal? claimValueUgx)
    {
        if (ValueTierStandards is { Count: > 0 })
        {
            var value = claimValueUgx ?? 0m;
            foreach (var tier in ValueTierStandards.OrderBy(t => t.UpToValueUgx ?? decimal.MaxValue))
            {
                if (tier.UpToValueUgx is null || value <= tier.UpToValueUgx)
                {
                    return tier.WorkingDays;
                }
            }
        }

        return ServiceStandardWorkingDays;
    }
}

/// <summary>One value band of the settlement clock, e.g. "up to UGX 10,000,000 within 10 working
/// days". UpToValueUgx null means the open-ended top tier ("and above").</summary>
public sealed record ValueTierServiceStandard(decimal? UpToValueUgx, int WorkingDays);

/// <summary>
/// One document or evidence slot a product asks for (build-loop item 4). <c>ClaimTypeKeys</c>
/// limits it to certain claim types; empty means it applies to every claim type of the product.
/// <c>Kind</c> says whether it is paperwork (can be scanned and turned into a PDF) or evidence
/// (kept exactly as taken). The remaining fields are the slot's limits. Every added field has a
/// default so a requirement stored before they existed reads exactly as it always did.
/// </summary>
public sealed record ProductDocumentRequirement(
    string DocumentKey,
    string CustomerLabel,
    string Reason,
    bool IsRequired,
    string RequiredAtStageKey,
    bool IsRegulatoryOrWordingMandated,
    IReadOnlyList<string>? ClaimTypeKeys = null,
    DocumentKind? Kind = null,
    bool AllowsMultiplePages = false,
    int MaxFiles = 0,
    IReadOnlyList<string>? AcceptedFormats = null)
{
    public bool AppliesTo(string? claimTypeKey) =>
        ClaimTypeKeys is not { Count: > 0 }
        || (!string.IsNullOrWhiteSpace(claimTypeKey)
            && ClaimTypeKeys.Any(key => string.Equals(key, claimTypeKey, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The slot a customer sees: configured limits where given, the kind's defaults
    /// otherwise (MaxFiles 0 means "use the default for this kind").</summary>
    public ResolvedDocumentSlot ToSlot()
    {
        // No stored kind means "the default for this document": photos of the damage are evidence,
        // everything else is paperwork. That keeps every requirement saved before kinds existed
        // behaving sensibly without a migration.
        var kind = Kind ?? (Enum.TryParse<DocumentType>(DocumentKey, ignoreCase: true, out var builtIn)
            ? DocumentSlotDefaults.KindFor(builtIn)
            : DocumentKind.Document);
        var multiple = kind == DocumentKind.Evidence || AllowsMultiplePages;
        var maxFiles = MaxFiles > 0 ? MaxFiles : DocumentSlotDefaults.MaxFilesFor(kind, multiple);
        if (!multiple)
        {
            maxFiles = 1;
        }

        var formats = AcceptedFormats is { Count: > 0 } ? AcceptedFormats : DocumentSlotDefaults.FormatsFor(kind);
        return new ResolvedDocumentSlot(
            DocumentKey.Trim(), CustomerLabel, Reason, IsRequired, kind, multiple, maxFiles, formats, IsRegulatoryOrWordingMandated);
    }
}

public sealed record ProductWordingFact(
    string Category,
    string Label,
    string Value,
    Guid SourceKnowledgeDocumentId,
    string SourceChunkId,
    string SourceQuote,
    decimal Confidence);

public interface IProductRepository
{
    /// <summary>The highest-Version row for this product, or null if it does not exist.</summary>
    Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>A specific historical version, used to pin a claim to the version in force when it
    /// was filed even after the product moves on to later versions.</summary>
    Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default);

    /// <summary>Every saved version for this product, newest first.</summary>
    Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>The current version for every product this tenant has ever created.</summary>
    Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Always inserts a new version row, never overwrites a previous one.</summary>
    Task SaveAsync(Product version, CancellationToken cancellationToken = default);
}

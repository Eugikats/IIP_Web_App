using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

/// <summary>
/// Outcome N: "upload an export of policies... map columns on screen, see a validation report,
/// load read only." The recognized target fields a column can be mapped to; PolicyNumber and
/// ProductLine are the only two a row cannot be imported without.
/// </summary>
public static class PolicyImportFields
{
    public const string PolicyNumber = "policyNumber";
    public const string ProductLine = "productLine";
    public const string CoverType = "coverType";
    public const string CustomerId = "customerId";
    public const string BranchId = "branchId";
    public const string EffectiveFrom = "effectiveFrom";
    public const string EffectiveTo = "effectiveTo";
    public const string AnnualPremium = "annualPremium";
    public const string PremiumCurrency = "premiumCurrency";
    public const string SumInsured = "sumInsured";

    public static readonly IReadOnlyList<string> All =
    [
        PolicyNumber, ProductLine, CoverType, CustomerId, BranchId,
        EffectiveFrom, EffectiveTo, AnnualPremium, PremiumCurrency, SumInsured,
    ];

    public static readonly IReadOnlyList<string> Required = [PolicyNumber, ProductLine];

    /// <summary>Best-effort column-name -> field guess, "Policy Number"/"policy_no"/"POLICYNO" all
    /// normalize to the same comparison key as PolicyNumber. A convenience for the "map columns on
    /// screen" step to arrive pre-filled, never authoritative: the confirmed mapping the caller
    /// sends to Validate is what actually decides.</summary>
    public static string? SuggestFieldFor(string columnHeader)
    {
        var normalized = Normalize(columnHeader);
        return All.FirstOrDefault(field => Normalize(field) == normalized || Normalize(HumanLabel(field)) == normalized)
            ?? (Synonyms.TryGetValue(normalized, out var field) ? field : null);
    }

    /// <summary>The names insurers' core systems and spreadsheets actually give these columns. An
    /// export from any of them arrives with its columns already matched, so loading policies is a
    /// check and a click rather than a mapping exercise. Compared after dropping case, spaces and
    /// punctuation, so "POLICY_NO", "Policy No." and "policy no" are one entry.</summary>
    private static readonly Dictionary<string, string> Synonyms = BuildSynonyms();

    private static Dictionary<string, string> BuildSynonyms()
    {
        var table = new Dictionary<string, string[]>
        {
            [PolicyNumber] = ["policyno", "polno", "policyref", "policyreference", "policynum", "policyid", "certificateno", "certno", "contractno", "policyidentifier"],
            [ProductLine] = ["classofbusiness", "class", "lineofbusiness", "lob", "product", "productname", "productcode", "producttype", "businessclass", "insuranceclass", "subclass"],
            [CoverType] = ["cover", "covertype", "coverage", "coveragetype", "coverdescription", "plan", "plantype", "policytype", "risktype"],
            [CustomerId] = ["customer", "customercode", "customerno", "clientid", "clientcode", "clientno", "insuredid", "insuredcode", "insuredno", "assuredid", "policyholderid", "memberno", "membernumber", "accountno"],
            [BranchId] = ["branch", "branchcode", "branchname", "office", "agency", "agencycode"],
            [EffectiveFrom] = ["inceptiondate", "inception", "startdate", "start", "commencementdate", "commencement", "effectivedate", "effective", "periodfrom", "coverfrom", "validfrom", "from", "datefrom", "riskstartdate"],
            [EffectiveTo] = ["expirydate", "expiry", "expirationdate", "expiration", "enddate", "end", "renewaldate", "periodto", "coverto", "validto", "validuntil", "to", "dateto", "riskenddate", "maturitydate"],
            [AnnualPremium] = ["premium", "grosspremium", "totalpremium", "annualpremium", "yearlypremium", "premiumamount", "premiumdue", "gwp", "netpremium"],
            [PremiumCurrency] = ["currency", "ccy", "curr", "currencycode", "premiumcurrency"],
            [SumInsured] = ["suminsured", "sumassured", "si", "limit", "limitofliability", "insuredvalue", "coveramount", "totalsuminsured", "tsi", "sumcovered"],
        };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, names) in table)
        {
            foreach (var name in names)
            {
                result.TryAdd(name, field);
            }
        }

        return result;
    }

    public static string HumanLabel(string field) => field switch
    {
        PolicyNumber => "Policy Number",
        ProductLine => "Product Line",
        CoverType => "Cover Type",
        CustomerId => "Customer ID",
        BranchId => "Branch ID",
        EffectiveFrom => "Effective From",
        EffectiveTo => "Effective To",
        AnnualPremium => "Annual Premium",
        PremiumCurrency => "Premium Currency",
        SumInsured => "Sum Insured",
        _ => field,
    };

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

/// <summary>One row's outcome: either a ready-to-save Policy, or the reasons it could not be
/// imported. Deterministic (AGENTS.md non-negotiable 2): every rule here is a parse or a presence
/// check over the row's own values, never a model call.</summary>
public sealed record PolicyImportRowOutcome(
    int RowNumber,
    bool IsValid,
    Policy? Policy,
    IReadOnlyCollection<string> Errors)
{
    public static PolicyImportRowOutcome Success(int rowNumber, Policy policy) => new(rowNumber, true, policy, []);
    public static PolicyImportRowOutcome Failure(int rowNumber, IReadOnlyCollection<string> errors) => new(rowNumber, false, null, errors);
}

public static class PolicyImportRowValidator
{
    /// <param name="existingPolicyId">The PolicyId of an existing record with this same policy
    /// number, if one already exists, so a re-import refreshes it in place instead of creating a
    /// duplicate; a freshly generated id otherwise.</param>
    public static PolicyImportRowOutcome Validate(
        TenantScope scope,
        int rowNumber,
        IReadOnlyDictionary<string, string> mappedRow,
        string existingPolicyId,
        DateTimeOffset importedAt)
    {
        var errors = new List<string>();

        var policyNumber = Trimmed(mappedRow, PolicyImportFields.PolicyNumber);
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            errors.Add("Policy number is required.");
        }

        var productLine = Trimmed(mappedRow, PolicyImportFields.ProductLine);
        if (string.IsNullOrWhiteSpace(productLine))
        {
            errors.Add("Product line is required.");
        }

        var effectiveFrom = ParseDate(mappedRow, PolicyImportFields.EffectiveFrom, "cover start date", errors);
        var effectiveTo = ParseDate(mappedRow, PolicyImportFields.EffectiveTo, "cover end date", errors);
        if (effectiveFrom is not null && effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            errors.Add("Cover end date is before cover start date.");
        }

        var annualPremium = ParseDecimal(mappedRow, PolicyImportFields.AnnualPremium, "annual premium", errors);
        var sumInsured = ParseDecimal(mappedRow, PolicyImportFields.SumInsured, "sum insured", errors);

        if (errors.Count > 0)
        {
            return PolicyImportRowOutcome.Failure(rowNumber, errors);
        }

        var policy = new Policy(
            existingPolicyId,
            scope.TenantId,
            scope.WorkspaceId,
            policyNumber!,
            productLine!,
            Trimmed(mappedRow, PolicyImportFields.CoverType),
            Trimmed(mappedRow, PolicyImportFields.CustomerId),
            Trimmed(mappedRow, PolicyImportFields.BranchId),
            effectiveFrom,
            effectiveTo,
            annualPremium,
            Trimmed(mappedRow, PolicyImportFields.PremiumCurrency),
            sumInsured,
            PolicyRecordSource.FileImport,
            importedAt);

        return PolicyImportRowOutcome.Success(rowNumber, policy);
    }

    private static string? Trimmed(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static DateOnly? ParseDate(IReadOnlyDictionary<string, string> row, string field, string label, List<string> errors)
    {
        var raw = Trimmed(row, field);
        if (raw is null)
        {
            return null;
        }

        if (DateOnly.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        errors.Add($"'{raw}' is not a valid {label}.");
        return null;
    }

    private static decimal? ParseDecimal(IReadOnlyDictionary<string, string> row, string field, string label, List<string> errors)
    {
        var raw = Trimmed(row, field);
        if (raw is null)
        {
            return null;
        }

        var cleaned = raw.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (decimal.TryParse(cleaned, out var parsed))
        {
            return parsed;
        }

        errors.Add($"'{raw}' is not a valid {label}.");
        return null;
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Application.Workspace;

/// <summary>One real step the analysis took, with what it found. Steps are produced from the counts
/// of the run, never from a timer, so what the screen shows is what happened.</summary>
public sealed record AnalysisStep(string Label, string Detail, string Status);

public sealed record ColumnMappingResult(string Header, string Field, string FieldLabel, string Confidence);

public sealed record AnalysedProspect(
    int RowNumber,
    string CustomerRef,
    string Name,
    string ContactMasked,
    string? Phone,
    string Consent,
    bool Contactable,
    string SuggestedProduct,
    string ProductLine,
    string Insurer,
    int Score,
    string Band,
    string Confidence,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Missing,
    string NextAction,
    string ReviewStatus,
    string TemplateName,
    string MessagePreview);

public sealed record ExcludedRow(int RowNumber, string CustomerRef, string Reason, string Remediation);

public sealed record CountItem(string Label, int Count);

public sealed record AnalysisSummary(
    int TotalRecords,
    int ValidContacts,
    int Eligible,
    int Excluded,
    int NeedsReview,
    IReadOnlyList<CountItem> ProductRecommendations,
    IReadOnlyList<CountItem> ScoreDistribution,
    IReadOnlyList<CountItem> ExclusionReasons,
    string CampaignReadiness);

public sealed record WorkbookAnalysis(
    string AnalysisId,
    string FileName,
    DateTimeOffset AnalysedAt,
    string BankName,
    IReadOnlyList<ColumnMappingResult> Mappings,
    IReadOnlyList<string> IgnoredColumns,
    IReadOnlyList<string> SensitiveColumnsIgnored,
    IReadOnlyList<AnalysisStep> Steps,
    IReadOnlyList<AnalysedProspect> Prospects,
    IReadOnlyList<ExcludedRow> Exclusions,
    AnalysisSummary Summary,
    IReadOnlyList<string> Notices,
    bool Truncated);

/// <summary>
/// Turns a customer workbook (headers plus rows of text) into explainable insurance recommendations.
/// The scoring is <see cref="ProspectScoring"/>, the same fixed rules the rest of the platform uses, so a
/// score can be explained from the file and is not a model's guess. Nothing here contacts anyone: it
/// separates commercial opportunity (a score and a suggested cover) from permission to message
/// (a valid number plus stated consent), and it never decides eligibility or price.
/// </summary>
public static class WorkbookProspectAnalyzer
{
    public const string TemplateName = "iip_bancassurance_product_intro";
    public const int MaxRows = 5000;

    private sealed record FieldSpec(string Key, string Label, string[] Aliases, string[] ExactOnly, bool Multi = false);

    private static readonly FieldSpec[] Fields =
    [
        new("customer_ref", "Customer ID", ["customer id", "client id", "customer number", "customer no", "cif", "customer reference", "customer ref"], ["ref", "reference"]),
        new("first_name", "First name", ["first name", "firstname", "given name"], []),
        new("last_name", "Last name", ["last name", "lastname", "surname", "family name"], []),
        new("name", "Full name", ["full name", "customer name", "client name"], ["name", "customer", "client"]),
        new("phone", "WhatsApp number", ["phone number", "mobile number", "whatsapp number", "telephone", "whatsapp", "msisdn", "phone", "mobile"], ["tel", "contact"]),
        new("email", "Email", ["email", "e mail"], []),
        new("occupation", "Work", ["occupation", "profession", "employment category", "employment", "job"], ["work"]),
        new("business", "Business or sector", ["business sector", "business activity", "business", "sector"], []),
        new("area", "Area", ["district", "area", "town", "location"], []),
        new("branch", "Bank branch", ["branch"], []),
        new("bank_products", "Bank products held", ["asset financing", "loan type", "account type", "bank products", "products held", "product held"], ["products", "loan"], Multi: true),
        new("income_band", "Income band", ["income band", "income", "turnover"], []),
        new("dependants", "Dependants", ["dependants", "dependents"], []),
        new("existing_cover", "Existing cover", ["existing insurance", "existing cover", "insurance products", "current insurance"], []),
        new("interest", "Stated insurance interest", ["insurance interest", "product interest", "interested in", "interest"], []),
        new("consent", "Messaging consent", ["whatsapp opt in", "marketing consent", "messaging consent", "opt in status", "opted in", "opt in", "optin", "consent"], []),
        new("opt_out", "Opt-out", ["opt out status", "opt out", "optout", "unsubscribed"], []),
        new("language", "Preferred language", ["preferred language", "language"], []),
    ];

    // Headers that name data this tool does not need. They are listed to the user and never read.
    private static readonly string[] SensitiveHeaderWords =
    [
        "national id", "nin", "id number", "passport", "account number", "balance", "salary", "card number",
        "pin", "password", "date of birth", "dob", "tin", "religion", "ethnic", "health", "disability",
    ];

    private static readonly Dictionary<string, string[]> InterestWords = new(StringComparer.OrdinalIgnoreCase)
    {
        [MicroinsuranceCatalog.FuneralLine] = ["life", "funeral", "death", "burial", "family"],
        [MicroinsuranceCatalog.HospitalCashLine] = ["health", "medical", "hospital", "sick"],
        [MicroinsuranceCatalog.BodaAccidentLine] = ["accident", "boda", "motorcycle", "rider"],
        [MicroinsuranceCatalog.StallStockLine] = ["stock", "stall", "market"],
        [MicroinsuranceCatalog.ShopLine] = ["shop", "business", "retail"],
        [MicroinsuranceCatalog.TransitLine] = ["transit", "goods", "cargo", "transport"],
        [MicroinsuranceCatalog.MaternityLine] = ["maternity", "pregnan", "baby"],
        [MicroinsuranceCatalog.LoanProtectorLine] = ["loan"],
        [MicroinsuranceCatalog.SchoolFeesLine] = ["school", "education", "fees"],
        [MicroinsuranceCatalog.HomeContentsLine] = ["home", "house", "property", "contents"],
    };

    private static readonly HashSet<string> AgreedWords = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "y", "true", "1", "agreed", "opted in", "opt in", "opt-in", "consented", "granted", "accepted", "subscribed" };

    private static readonly HashSet<string> DeclinedWords = new(StringComparer.OrdinalIgnoreCase)
        { "no", "n", "false", "0", "declined", "opted out", "opt out", "opt-out", "stop", "unsubscribed", "withdrawn", "refused" };

    private static readonly HashSet<string> OptedOutWords = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "y", "true", "1", "opted out", "opt out", "opt-out", "stop", "unsubscribed", "withdrawn", "refused" };

    private static readonly HashSet<string> NoCoverWords = new(StringComparer.OrdinalIgnoreCase)
        { "none", "no", "nil", "n/a", "na", "-", "none yet", "not insured", "no cover" };

    public static WorkbookAnalysis Analyse(
        string fileName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        bool truncated,
        DateTimeOffset now,
        KatoBrand brand)
    {
        var mapping = MapColumns(headers);
        var products = brand.Products;
        var profiles = ProductTargeting.ProfilesFor(products);
        var bankName = brand.Organisation;

        string Cell(IReadOnlyList<string> row, string field)
        {
            if (!mapping.Columns.TryGetValue(field, out var indexes)) return string.Empty;
            var parts = indexes.Select(index => index < row.Count ? row[index].Trim() : string.Empty).Where(value => value.Length > 0);
            return string.Join(", ", parts);
        }

        var working = rows
            .Select((row, index) => (Row: row, Number: index + 2))
            .Where(item => item.Row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToArray();

        var prospects = new List<AnalysedProspect>();
        var exclusions = new List<ExcludedRow>();
        var seenPhones = new Dictionary<string, int>(StringComparer.Ordinal);
        var invalidPhones = 0;
        var duplicates = 0;
        var agreed = 0;
        var notRecorded = 0;
        var optedOut = 0;
        var matchedToProduct = 0;

        foreach (var (row, number) in working)
        {
            var customerRef = Cell(row, "customer_ref");
            var label = string.IsNullOrWhiteSpace(customerRef) ? $"Row {number}" : customerRef;
            var name = Cell(row, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"{Cell(row, "first_name")} {Cell(row, "last_name")}".Trim();
            }

            var (phone, phoneProblem) = NormalisePhone(Cell(row, "phone"));
            if (phone is not null)
            {
                if (seenPhones.TryGetValue(phone, out var firstRow))
                {
                    duplicates++;
                    exclusions.Add(new ExcludedRow(number, label, $"Duplicate of row {firstRow}", "Remove the repeated row. Each person is analysed once."));
                    continue;
                }

                seenPhones[phone] = number;
            }
            else
            {
                invalidPhones++;
            }

            var consent = ParseConsent(Cell(row, "consent"), Cell(row, "opt_out"));
            switch (consent)
            {
                case ContactConsentStatus.Agreed: agreed++; break;
                case ContactConsentStatus.NotRecorded: notRecorded++; break;
                default: optedOut++; break;
            }

            var facts = BuildFacts(row, Cell, now);
            var interests = BuildInterests(Cell(row, "interest"), products, now, out var unmatchedInterest);
            var existing = Cell(row, "existing_cover");
            var holdsCover = !string.IsNullOrWhiteSpace(existing) && !NoCoverWords.Contains(existing.Trim());
            var bankProducts = Cell(row, "bank_products");

            var inputs = new ProspectInputs(
                facts,
                interests,
                string.IsNullOrWhiteSpace(bankProducts) ? null : true,
                holdsCover,
                Registered: false,
                consent,
                HasPhone: phone is not null,
                LastInteractionAt: null,
                CustomerMessages: 0,
                Applied: false);
            var triage = ProspectScoring.Score(inputs, profiles, now);

            // A cover that scores only on its built-in baseline has no evidence from this person's row
            // behind it, so it is not presented as a match. The generic message is the safe fallback.
            var top = triage.Fits.Count > 0 ? triage.Fits[0] : null;
            if (top is not null && IsBaselineOnly(top, profiles)) top = null;
            if (top is not null) matchedToProduct++;

            var contactable = phone is not null && consent == ContactConsentStatus.Agreed;
            var missing = triage.Missing.ToList();
            missing.AddRange(unmatchedInterest.Select(word => $"Interest in '{word}' has no matching configured product"));

            var firstName = FirstName(Cell(row, "first_name"), name);
            var preview = BuildPreview(firstName, bankName, top?.Product);

            string reviewStatus;
            string nextAction;
            if (!contactable)
            {
                reviewStatus = "Excluded from campaign";
                var (reason, fix) = ExclusionFor(phone, phoneProblem, consent);
                exclusions.Add(new ExcludedRow(number, label, reason, fix));
                nextAction = fix;
            }
            else if (unmatchedInterest.Count > 0)
            {
                reviewStatus = "Needs human review";
                nextAction = top is null
                    ? "Review before sending: the stated interest has no configured product. The generic discovery message would be used."
                    : $"Review before sending: part of the stated interest has no configured product. Invite to explore {top.Product}.";
            }
            else if (top is null)
            {
                reviewStatus = "Ready for campaign review";
                nextAction = "Send the approved generic discovery message. Too little is recorded to suggest a specific cover.";
            }
            else
            {
                reviewStatus = "Ready for campaign review";
                nextAction = $"Invite to explore {top.Product}.";
            }

            prospects.Add(new AnalysedProspect(
                number,
                label,
                string.IsNullOrWhiteSpace(name) ? "Name not provided" : name,
                phone is null ? "No valid number" : MaskPhone(phone),
                phone,
                ConsentLabel(consent),
                contactable,
                top?.Product ?? "No clear match",
                top?.ProductLine ?? string.Empty,
                top?.Insurer ?? string.Empty,
                triage.Score,
                ProspectScoring.Label(triage.Band),
                triage.Confidence,
                top is null ? triage.Signals : top.Reasons.Concat(triage.Signals).ToArray(),
                missing,
                nextAction,
                reviewStatus,
                TemplateName,
                preview));
        }

        var eligible = prospects.Count(p => p.Contactable);
        var needsReview = prospects.Count(p => p.ReviewStatus == "Needs human review");
        var validContacts = prospects.Count(p => p.Phone is not null);

        var summary = new AnalysisSummary(
            working.Length,
            validContacts,
            eligible,
            exclusions.Count,
            needsReview,
            prospects.Where(p => p.ProductLine.Length > 0)
                .GroupBy(p => p.SuggestedProduct).Select(g => new CountItem(g.Key, g.Count()))
                .OrderByDescending(item => item.Count).ToArray(),
            new[] { "High priority", "Qualified", "Warm", "Early interest", "Cold" }
                .Select(band => new CountItem(band, prospects.Count(p => p.Band == band))).ToArray(),
            exclusions.GroupBy(e => GroupReason(e.Reason)).Select(g => new CountItem(g.Key, g.Count()))
                .OrderByDescending(item => item.Count).ToArray(),
            eligible == 0
                ? "No recipients are ready. Fix the exclusions or record consent first."
                : $"{eligible} recipient{(eligible == 1 ? string.Empty : "s")} ready for named human review. Nothing has been sent.");

        var ignored = headers.Where((header, index) =>
                !string.IsNullOrWhiteSpace(header)
                && !mapping.Mapped.Contains(index)
                && !mapping.Sensitive.Contains(index))
            .ToArray();
        var sensitive = mapping.Sensitive.Select(index => headers[index]).ToArray();

        var mappingRows = mapping.Report
            .Select(item => new ColumnMappingResult(item.Header, item.Field, Fields.First(f => f.Key == item.Field).Label, item.Confidence))
            .ToArray();

        var notices = new List<string>
        {
            "Consent shown here is what the file states. A workbook flag is a claim, not evidence. The send step must recheck consent evidence, opt-outs and template approval.",
            "Scores order follow-up from the fixed rules shown with each result. They are not probabilities and not underwriting or pricing decisions.",
            "The message preview uses the QR-registration introduction template, whose wording says the person asked about insurance. Use it only for people who registered and agreed to hear from you.",
        };
        if (!mapping.Columns.ContainsKey("phone")) notices.Add("No phone column was recognised, so nobody can be messaged. Rename the column to 'Phone' or 'WhatsApp number'.");
        if (!mapping.Columns.ContainsKey("consent")) notices.Add("No consent column was recognised, so every person is treated as having no recorded consent.");
        if (truncated) notices.Add($"The file has more than {MaxRows} rows. Only the first {MaxRows} were analysed.");
        if (sensitive.Length > 0) notices.Add($"{sensitive.Length} column{(sensitive.Length == 1 ? " was" : "s were")} left unread because it holds sensitive data this analysis does not need: {string.Join(", ", sensitive)}.");

        var steps = new List<AnalysisStep>
        {
            new("Reading spreadsheet", $"{working.Length} records and {headers.Count} columns in {fileName}", "Done"),
            new("Validating column structure",
                $"{mappingRows.Length} columns recognised, {ignored.Length} ignored, {sensitive.Length} sensitive left unread",
                mapping.Columns.ContainsKey("phone") ? "Done" : "Warning"),
            new("Cleaning customer records",
                $"{validContacts} valid numbers in international format, {invalidPhones} invalid or missing, {duplicates} duplicates removed",
                invalidPhones > 0 || duplicates > 0 ? "Warning" : "Done"),
            new("Checking consent and messaging eligibility",
                $"{agreed} state consent, {notRecorded} have none recorded, {optedOut} opted out",
                mapping.Columns.ContainsKey("consent") ? "Done" : "Warning"),
            new("Matching customers to products", $"{matchedToProduct} matched to a configured product, {prospects.Count - matchedToProduct} with no clear match", "Done"),
            new("Generating scored recommendations", $"{prospects.Count} scored with reasons from the fixed rules", "Done"),
            new("Preparing personalised messages", $"{prospects.Count} previews built from the approved template wording", "Done"),
            new("Validating templates", $"'{TemplateName}' is a draft here. Meta approval has not been verified in this step", "Warning"),
            new("Preparing campaign approval", summary.CampaignReadiness, eligible > 0 ? "Done" : "Warning"),
        };

        return new WorkbookAnalysis(
            Guid.NewGuid().ToString("N")[..12],
            fileName,
            now,
            bankName,
            mappingRows,
            ignored,
            sensitive,
            steps,
            prospects,
            exclusions,
            summary,
            notices,
            truncated);
    }

    private sealed record Mapping(
        Dictionary<string, int[]> Columns,
        HashSet<int> Mapped,
        HashSet<int> Sensitive,
        List<(string Header, string Field, string Confidence)> Report);

    private static Mapping MapColumns(IReadOnlyList<string> headers)
    {
        var columns = new Dictionary<string, List<int>>();
        var mapped = new HashSet<int>();
        var sensitive = new HashSet<int>();
        var report = new List<(string, string, string)>();
        var normalised = headers.Select(Normalise).ToArray();

        for (var i = 0; i < headers.Count; i++)
        {
            if (SensitiveHeaderWords.Any(word => Padded(normalised[i]).Contains($" {word} ", StringComparison.Ordinal)))
            {
                sensitive.Add(i);
            }
        }

        foreach (var field in Fields)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (mapped.Contains(i) || sensitive.Contains(i) || normalised[i].Length == 0) continue;
                if (columns.ContainsKey(field.Key) && !field.Multi) break;

                string? confidence = null;
                if (field.ExactOnly.Contains(normalised[i]) || field.Aliases.Contains(normalised[i]))
                {
                    confidence = "Exact";
                }
                else if (field.Aliases.Any(alias => Padded(normalised[i]).Contains($" {alias} ", StringComparison.Ordinal)))
                {
                    confidence = "Likely";
                }

                if (confidence is null) continue;
                if (!columns.TryGetValue(field.Key, out var list)) columns[field.Key] = list = [];
                list.Add(i);
                mapped.Add(i);
                report.Add((headers[i], field.Key, confidence));
            }
        }

        return new Mapping(columns.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), mapped, sensitive, report);
    }

    private static bool IsBaselineOnly(ProductFit fit, IReadOnlyList<ProductTargetProfile> profiles)
    {
        var profile = profiles.FirstOrDefault(p => p.Product == fit.Product);
        return profile?.WhoItIsFor is { } who && fit.Reasons.Count == 1 && fit.Reasons[0] == who;
    }

    private static string Padded(string value) => $" {value} ";

    private static string Normalise(string? header) =>
        Regex.Replace((header ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();

    private static IReadOnlyList<ProspectFact> BuildFacts(IReadOnlyList<string> row, Func<IReadOnlyList<string>, string, string> cell, DateTimeOffset now)
    {
        var facts = new List<ProspectFact>();
        foreach (var key in new[] { "occupation", "business", "area", "income_band", "dependants", "existing_cover", "bank_products", "branch" })
        {
            var value = cell(row, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                facts.Add(new ProspectFact(key, value, ProspectFactSource.ImportedList, now));
            }
        }

        return facts;
    }

    private static IReadOnlyList<ProspectInterest> BuildInterests(
        string text,
        IReadOnlyList<MicroinsuranceCatalog.MicroProduct> products,
        DateTimeOffset now,
        out List<string> unmatched)
    {
        unmatched = [];
        var interests = new List<ProspectInterest>();
        if (string.IsNullOrWhiteSpace(text)) return interests;

        foreach (var token in Regex.Split(text, @"[,;/&]|\band\b", RegexOptions.IgnoreCase)
                     .Select(part => part.Trim().ToLowerInvariant())
                     .Where(part => part.Length > 0))
        {
            var line = InterestWords.FirstOrDefault(pair => pair.Value.Any(word => token.Contains(word, StringComparison.Ordinal))).Key;
            var product = line is null ? null : products.FirstOrDefault(p => string.Equals(p.ProductLine, line, StringComparison.OrdinalIgnoreCase));
            if (product is null)
            {
                unmatched.Add(token);
                continue;
            }

            interests.Add(new ProspectInterest(product.Name, InterestStrength.Interested, $"Stated interest: {token}", now));
        }

        return interests;
    }

    public static ContactConsentStatus ParseConsent(string consent, string optOut)
    {
        if (OptedOutWords.Contains(optOut.Trim()))
        {
            // An opt-out column that says yes or opted out wins over any consent flag.
            return ContactConsentStatus.Withdrawn;
        }

        var value = consent.Trim();
        if (value.Length == 0) return ContactConsentStatus.NotRecorded;
        if (AgreedWords.Contains(value)) return ContactConsentStatus.Agreed;
        if (DeclinedWords.Contains(value)) return ContactConsentStatus.Declined;
        return ContactConsentStatus.NotRecorded;
    }

    /// <summary>International form with a plus sign, or the reason the number cannot be used. A number
    /// whose country is unclear is rejected rather than guessed.</summary>
    public static (string? Phone, string? Problem) NormalisePhone(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0) return (null, "No number given");

        // Excel often turns a long number into scientific notation, such as 2.56776678989E+11.
        if (Regex.IsMatch(text, @"^\d+(\.\d+)?[eE]\+?\d+$")
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var asNumber))
        {
            text = asNumber.ToString("F0", CultureInfo.InvariantCulture);
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        var international = text.StartsWith('+') || digits.StartsWith("00", StringComparison.Ordinal);
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];

        if (digits.StartsWith("256", StringComparison.Ordinal))
        {
            return digits.Length == 12 && digits[3] == '7' ? ($"+{digits}", null) : (null, "Not a valid Ugandan mobile number");
        }

        if (international)
        {
            return digits.Length is >= 8 and <= 15 ? ($"+{digits}", null) : (null, "International number has the wrong length");
        }

        if (digits.Length == 10 && digits[0] == '0' && digits[1] == '7') return ($"+256{digits[1..]}", null);
        if (digits.Length == 9 && digits[0] == '7') return ($"+256{digits}", null);
        return (null, "Not a recognisable Ugandan mobile number");
    }

    public static string MaskPhone(string phone) =>
        phone.Length <= 7 ? phone : $"{phone[..4]} *** ***{phone[^3..]}";

    private static string FirstName(string firstNameColumn, string fullName)
    {
        var source = string.IsNullOrWhiteSpace(firstNameColumn) ? fullName : firstNameColumn;
        return source.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    /// <summary>The approved wording of the product introduction template with its variables filled from
    /// confirmed data only. With no name, the greeting is neutral. With no matching cover, the product
    /// variable falls back to the word "insurance", so no cover is claimed without a reason.</summary>
    private static string BuildPreview(string firstName, string bankName, string? product)
    {
        var greeting = string.IsNullOrWhiteSpace(firstName) ? "Hello," : $"Hello {firstName},";
        var cover = string.IsNullOrWhiteSpace(product) ? "insurance" : product;
        return $"{greeting} thanks for asking about insurance at {bankName}. You can explore {cover} cover and learn what it includes. "
             + "Tap Learn more to chat with our assistant. Cover and eligibility depend on the insurer's policy terms. Reply STOP to opt out.";
    }

    private static (string Reason, string Fix) ExclusionFor(string? phone, string? phoneProblem, ContactConsentStatus consent)
    {
        if (phone is null)
        {
            return ($"Invalid number: {phoneProblem}", "Correct the number to a Ugandan mobile such as +2567XXXXXXXX.");
        }

        return consent switch
        {
            ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn =>
                ("Opted out of messaging", "None. Do not contact this person for marketing."),
            _ => ("No messaging consent recorded", "Collect explicit WhatsApp opt-in, for example through the QR registration form."),
        };
    }

    private static string GroupReason(string reason) =>
        reason.StartsWith("Invalid number", StringComparison.Ordinal) ? "Invalid number"
        : reason.StartsWith("Duplicate", StringComparison.Ordinal) ? "Duplicate"
        : reason;

    private static string ConsentLabel(ContactConsentStatus status) => status switch
    {
        ContactConsentStatus.Agreed => "Stated in file (evidence not verified)",
        ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn => "Opted out",
        _ => "Not recorded",
    };
}

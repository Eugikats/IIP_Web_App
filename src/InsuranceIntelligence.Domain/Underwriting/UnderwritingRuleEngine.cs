namespace InsuranceIntelligence.Domain.Underwriting;

/// <summary>
/// Outcome of a single underwriting check. Advisory only — the engine never
/// accepts, rejects, binds, or prices. An underwriter decides (DEC-014,
/// AGENTS.md human-supervised decision boundary).
/// </summary>
public enum UnderwritingCheckState
{
    Pass,
    Attention,
    MissingInformation
}

public sealed record UnderwritingSubmissionInput(
    string SubmissionId,
    string Applicant,
    string Broker,
    string ProductLine,
    decimal SumInsured,
    string Currency,
    IReadOnlyList<string> MissingItems);

public sealed record UnderwritingCheck(
    string CheckId,
    string Label,
    UnderwritingCheckState State,
    string Detail);

/// <summary>
/// Advisory underwriting review for one submission: rule results + drafted
/// clarifying questions. Contains no decision, premium, or bind action.
/// </summary>
public sealed record UnderwritingReview(
    string SubmissionId,
    string Applicant,
    IReadOnlyList<UnderwritingCheck> Checks,
    IReadOnlyList<string> DraftedQuestions);

public sealed record UnderwritingRuleOptions
{
    /// <summary>Sum-insured above this refers the submission to a senior underwriter.</summary>
    public decimal SeniorReferralThreshold { get; init; } = 250_000_000m;

    /// <summary>Product lines that always warrant a closer look regardless of value.</summary>
    public IReadOnlySet<string> HigherRiskProductLines { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Marine Cargo", "Fleet Comprehensive", "Aviation" };

    public static readonly UnderwritingRuleOptions Default = new();
}

/// <summary>
/// Deterministic, rule-based underwriting assist engine (v1, no LLM). Surfaces
/// missing information, coverage/limit referral flags, and drafts clarifying
/// questions for the underwriter. It never decides acceptance/rejection/premium.
/// </summary>
public static class UnderwritingRuleEngine
{
    public static UnderwritingReview Review(UnderwritingSubmissionInput submission, UnderwritingRuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(submission);
        options ??= UnderwritingRuleOptions.Default;

        var checks = new List<UnderwritingCheck>();
        var questions = new List<string>();

        // Completeness check.
        if (submission.MissingItems.Count > 0)
        {
            checks.Add(new UnderwritingCheck(
                "UW-COMPLETENESS",
                "Submission completeness",
                UnderwritingCheckState.MissingInformation,
                $"{submission.MissingItems.Count} required item(s) missing: {string.Join(", ", submission.MissingItems)}."));
            foreach (var item in submission.MissingItems)
            {
                questions.Add($"Could you please provide the {item} for submission {submission.SubmissionId}?");
            }
        }
        else
        {
            checks.Add(new UnderwritingCheck(
                "UW-COMPLETENESS",
                "Submission completeness",
                UnderwritingCheckState.Pass,
                "All required intake items are present."));
        }

        // Coverage / limit referral check.
        if (submission.SumInsured > options.SeniorReferralThreshold)
        {
            checks.Add(new UnderwritingCheck(
                "UW-LIMIT",
                "Sum-insured limit",
                UnderwritingCheckState.Attention,
                $"Sum insured {submission.Currency} {submission.SumInsured:N0} exceeds the senior-referral threshold of {submission.Currency} {options.SeniorReferralThreshold:N0}. Refer to a senior underwriter."));
        }
        else
        {
            checks.Add(new UnderwritingCheck(
                "UW-LIMIT",
                "Sum-insured limit",
                UnderwritingCheckState.Pass,
                $"Sum insured {submission.Currency} {submission.SumInsured:N0} is within standard authority."));
        }

        // Product-line risk check.
        if (options.HigherRiskProductLines.Contains(submission.ProductLine))
        {
            checks.Add(new UnderwritingCheck(
                "UW-PRODUCT",
                "Product-line risk",
                UnderwritingCheckState.Attention,
                $"{submission.ProductLine} is a higher-risk line — confirm survey/inspection requirements before quoting."));
        }

        return new UnderwritingReview(submission.SubmissionId, submission.Applicant, checks, questions);
    }

    public static IReadOnlyList<UnderwritingReview> Review(
        IReadOnlyList<UnderwritingSubmissionInput> submissions,
        UnderwritingRuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(submissions);
        return submissions.Select(s => Review(s, options)).ToArray();
    }
}

using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Fraud;

/// <summary>Where a reason for attention came from.</summary>
public enum RiskFactorSource
{
    /// <summary>A fixed rule over the workspace's claims (duplicate contact, claim velocity, and so on).</summary>
    RuleEngine,

    /// <summary>The authenticity check of a picture (item 16).</summary>
    PictureCheck,

    /// <summary>A model's reading of the claim. Counted at a low weight and never alone enough to raise the band above Moderate.</summary>
    ModelReading,
}

public enum RiskBand
{
    Low,
    Moderate,
    Elevated,
    High,
}

/// <summary>What a person has already done with a reason for attention.</summary>
public enum HumanReading
{
    /// <summary>Nobody has looked yet.</summary>
    NotLookedAt,

    /// <summary>A person is working on it (triaged or assigned).</summary>
    BeingWorked,

    /// <summary>A person looked and set it aside (no concern, could not be substantiated, continues under normal review). It no longer adds to the score.</summary>
    SetAside,

    /// <summary>A person wants it followed up or has referred it. It counts in full.</summary>
    FollowUpRequested,

    /// <summary>A person has raised a concern. It counts in full and the recommendation is to refer.</summary>
    ConcernRaised,
}

public enum SourceKind
{
    Claim,
    Document,
    Field,
    Signal,
    Picture,
    Checklist,
    ModelReading,
    Score,
}

/// <summary>Where a statement in an assessment comes from, so a person can open it.</summary>
public sealed record AssessmentSource(SourceKind Kind, string Id, string Label);

/// <summary>One reason the claim deserves a closer look, in plain words, with where it came from and
/// what a person has done about it. Never an accusation.</summary>
public sealed record RiskFactor(
    string Code,
    RiskFactorSource Source,
    decimal Weight,
    string Reason,
    AssessmentSource Reference,
    HumanReading Reading,
    string? ReadingNote = null)
{
    /// <summary>Whether this reason still adds to the score.</summary>
    public bool Counts => Reading != HumanReading.SetAside && Weight > 0m;
}

/// <summary>A score from 0 to 100 for how much a person should look at the claim, with every reason that
/// went into it. It is a prompt for attention, not a probability of fraud and not a decision.</summary>
public sealed record RiskScore(int Value, RiskBand Band, IReadOnlyList<RiskFactor> Factors)
{
    public IReadOnlyList<RiskFactor> Counted => Factors.Where(factor => factor.Counts).ToArray();
}

public static class ClaimRiskScorer
{
    /// <summary>The most a model's reading can contribute in total, however many notes it writes.</summary>
    public const int MaxModelFactors = 2;

    public const decimal ModelFactorWeight = 0.08m;

    /// <summary>Weight of a rule signal by its severity.</summary>
    public static decimal WeightFor(FraudSignalSeverity severity) => severity switch
    {
        FraudSignalSeverity.AdvisorySignal => 0.10m,
        FraudSignalSeverity.PotentialAnomaly => 0.25m,
        FraudSignalSeverity.RequiresInvestigation => 0.45m,
        _ => 0.10m,
    };

    public static decimal WeightFor(AuthenticitySeverity severity) => severity switch
    {
        AuthenticitySeverity.Info => 0m,
        AuthenticitySeverity.Low => 0.05m,
        AuthenticitySeverity.Medium => 0.20m,
        AuthenticitySeverity.High => 0.40m,
        _ => 0m,
    };

    public static HumanReading ReadingFor(FraudSignalDisposition? disposition)
    {
        if (disposition is null)
        {
            return HumanReading.NotLookedAt;
        }

        return disposition.Status switch
        {
            FraudDispositionStatus.Triaged or FraudDispositionStatus.UnderInvestigation => HumanReading.BeingWorked,
            _ => disposition.Outcome switch
            {
                FraudDispositionOutcome.ReferredForSpecialInvestigation => HumanReading.ConcernRaised,
                FraudDispositionOutcome.NoFurtherActionRequired
                    or FraudDispositionOutcome.UnableToSubstantiateConcern
                    or FraudDispositionOutcome.ClaimContinuesUnderNormalReview => HumanReading.SetAside,
                _ => HumanReading.BeingWorked,
            },
        };
    }

    public static HumanReading ReadingFor(AuthenticityDisposition disposition) => disposition switch
    {
        AuthenticityDisposition.NoConcern => HumanReading.SetAside,
        AuthenticityDisposition.NeedsFollowUp => HumanReading.FollowUpRequested,
        AuthenticityDisposition.ConcernRaised => HumanReading.ConcernRaised,
        _ => HumanReading.NotLookedAt,
    };

    /// <summary>Combines the counted reasons so that each one adds less than the last and the total
    /// stays under 100: 100 x (1 - product of (1 - weight)).</summary>
    public static RiskScore Score(IReadOnlyList<RiskFactor> factors)
    {
        var ordered = factors.OrderByDescending(factor => factor.Weight).ThenBy(factor => factor.Code, StringComparer.Ordinal).ToArray();

        // A model's reading is trimmed to two notes, so a model that writes many cannot move the score much.
        // The notes beyond the second are left out of the list as well as the arithmetic, so what is shown
        // is exactly what was counted.
        var seenModel = 0;
        var listed = new List<RiskFactor>();
        var counted = new List<RiskFactor>();
        foreach (var factor in ordered)
        {
            if (factor.Source == RiskFactorSource.ModelReading && ++seenModel > MaxModelFactors)
            {
                continue;
            }

            listed.Add(factor);
            if (factor.Counts)
            {
                counted.Add(factor);
            }
        }

        var remaining = 1m;
        foreach (var factor in counted)
        {
            remaining *= 1m - Math.Clamp(factor.Weight, 0m, 0.95m);
        }

        var value = (int)Math.Round(100m * (1m - remaining), MidpointRounding.AwayFromZero);
        return new RiskScore(Math.Clamp(value, 0, 100), BandFor(value), listed);
    }

    public static RiskBand BandFor(int value) => value switch
    {
        < 20 => RiskBand.Low,
        < 45 => RiskBand.Moderate,
        < 70 => RiskBand.Elevated,
        _ => RiskBand.High,
    };

    public static string LabelFor(RiskBand band) => band switch
    {
        RiskBand.Low => "Low",
        RiskBand.Moderate => "Moderate",
        RiskBand.Elevated => "Elevated",
        RiskBand.High => "High",
        _ => band.ToString(),
    };
}

public enum RecommendationKind
{
    /// <summary>Nothing outstanding in the evidence: a person can go on to decide.</summary>
    ReadyForDecision,

    /// <summary>The claim is missing documents, or some could not be read.</summary>
    RequestInformation,

    /// <summary>Reasons for attention are still waiting for a person to look at them.</summary>
    ReviewFlaggedItems,

    /// <summary>The picture is serious enough, or a person has raised a concern, to hand it to the investigations route.</summary>
    ReferForInvestigation,
}

public sealed record RecommendationReason(string Text, AssessmentSource Source);

/// <summary>The facts the recommendation is worked out from. All of them are things already recorded
/// elsewhere on the claim.</summary>
public sealed record RecommendationFacts(
    RiskScore Score,
    IReadOnlyList<MissingDocumentRequirement> MissingRequired,
    IReadOnlyList<(Guid DocumentId, string Name)> UnreadableDocuments,
    int PendingValueProposals,
    Guid CaseId);

/// <summary>What the claims team is advised to do next, with the reasons and where each comes from. Advice
/// for a person: it approves, rejects and settles nothing.</summary>
public sealed record ClaimRecommendation(
    RecommendationKind Kind,
    string Headline,
    IReadOnlyList<RecommendationReason> Reasons,
    IReadOnlyList<string> SuggestedRequests);

public static class ClaimRecommender
{
    public static ClaimRecommendation Recommend(RecommendationFacts facts)
    {
        var score = facts.Score;
        var reasons = new List<RecommendationReason>();
        var counted = score.Counted;

        var raised = counted.Where(factor => factor.Reading == HumanReading.ConcernRaised).ToArray();
        var unlooked = counted.Where(factor => factor.Reading is HumanReading.NotLookedAt or HumanReading.FollowUpRequested && factor.Weight >= 0.20m).ToArray();
        var inProgress = counted.Where(factor => factor.Reading == HumanReading.BeingWorked && factor.Weight >= 0.20m).ToArray();

        foreach (var factor in counted.Where(factor => factor.Weight >= 0.20m || factor.Reading == HumanReading.ConcernRaised))
        {
            reasons.Add(new RecommendationReason(factor.Reason + ReadingSuffix(factor), factor.Reference));
        }

        var requests = new List<string>();
        foreach (var missing in facts.MissingRequired)
        {
            requests.Add($"Ask the customer for: {missing.Label}");
            reasons.Add(new RecommendationReason($"{missing.Label} has not been received.", new AssessmentSource(SourceKind.Checklist, missing.Key, missing.Label)));
        }

        foreach (var (documentId, name) in facts.UnreadableDocuments)
        {
            requests.Add($"Ask the customer to send {name} again");
            reasons.Add(new RecommendationReason($"{name} could not be read.", new AssessmentSource(SourceKind.Document, documentId.ToString(), name)));
        }

        if (facts.PendingValueProposals > 0)
        {
            reasons.Add(new RecommendationReason(
                $"{facts.PendingValueProposals} value{(facts.PendingValueProposals == 1 ? "" : "s")} read from documents {(facts.PendingValueProposals == 1 ? "is" : "are")} waiting for a person to accept or correct.",
                new AssessmentSource(SourceKind.Field, "proposals", "Values from documents")));
        }

        if (score.Band == RiskBand.High || raised.Length > 0)
        {
            reasons.Insert(0, new RecommendationReason(
                $"The review score is {score.Value} ({ClaimRiskScorer.LabelFor(score.Band)}), made up of the reasons below.",
                new AssessmentSource(SourceKind.Score, "score", "Review score")));
            return new ClaimRecommendation(
                RecommendationKind.ReferForInvestigation,
                "Consider referring this claim to the investigations route before any decision.",
                reasons,
                requests);
        }

        if (score.Band == RiskBand.Elevated || unlooked.Length > 0 || inProgress.Length > 0)
        {
            return new ClaimRecommendation(
                RecommendationKind.ReviewFlaggedItems,
                unlooked.Length > 0
                    ? "Look at the flagged items before deciding."
                    : "Flagged items are still being worked. Wait for them before deciding.",
                reasons,
                requests);
        }

        if (requests.Count > 0 || facts.PendingValueProposals > 0)
        {
            return new ClaimRecommendation(
                RecommendationKind.RequestInformation,
                requests.Count > 0 ? "Ask the customer for what is missing before deciding." : "Check the values read from documents before deciding.",
                reasons,
                requests);
        }

        reasons.Insert(0, new RecommendationReason(
            "All the documents asked for are in, they could be read, and nothing flagged is waiting.",
            new AssessmentSource(SourceKind.Claim, facts.CaseId.ToString(), "The claim")));
        return new ClaimRecommendation(
            RecommendationKind.ReadyForDecision,
            "Nothing is outstanding in the evidence. A person can go on to decide.",
            reasons,
            requests);
    }

    public static string LabelFor(RecommendationKind kind) => kind switch
    {
        RecommendationKind.ReadyForDecision => "Ready for a decision",
        RecommendationKind.RequestInformation => "Ask for more",
        RecommendationKind.ReviewFlaggedItems => "Look at flagged items first",
        RecommendationKind.ReferForInvestigation => "Consider referring",
        _ => kind.ToString(),
    };

    private static string ReadingSuffix(RiskFactor factor) => factor.Reading switch
    {
        HumanReading.BeingWorked => " (being worked by a person)",
        HumanReading.FollowUpRequested => " (a person asked for follow-up)",
        HumanReading.ConcernRaised => " (a person raised a concern)",
        _ => string.Empty,
    };
}

/// <summary>Keeps the model's wording within what an advisory note may say.</summary>
public static class AdvisoryLanguage
{
    private static readonly string[] Forbidden =
    [
        "approve the claim", "approved", "reject", "rejected", "deny", "denied", "repudiat", "settle the claim",
        "pay the claim", "payout is", "fraudulent", "committed fraud", "is a fraud", "is fraud", "confirmed fraud", "liar", "lying", "guilty",
    ];

    /// <summary>True when the text contains none of the words that would turn advice into a decision or an accusation.</summary>
    public static bool IsAcceptable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return !Forbidden.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}

public enum AssessmentResponseKind
{
    /// <summary>The person agrees and will follow the advice.</summary>
    Agree,

    /// <summary>The person disagrees. A note is required, so the reason is on record.</summary>
    Disagree,
}

/// <summary>
/// The review score and recommendation prepared for one claim (build-loop item 18), kept with the
/// person's response. Prepared by the platform, advisory only: it never changes a claim, blocks a
/// step or decides anything, and a person has to respond before anyone can say it was followed.
/// </summary>
public sealed record ClaimAssessment
{
    public required Guid AssessmentId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required RiskScore Score { get; init; }
    public required ClaimRecommendation Recommendation { get; init; }

    /// <summary>A few sentences a model wrote from the facts above, or null when the model was not used or its text was not acceptable.</summary>
    public string? Narrative { get; init; }
    public string? NarrativeProvider { get; init; }

    /// <summary>Identifies the facts this assessment was worked out from. When the facts change the hash changes and the earlier response no longer applies.</summary>
    public required string InputsHash { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string GeneratedBy { get; init; }
    public required string CorrelationId { get; init; }

    public AssessmentResponseKind? Response { get; init; }
    public string? ResponseNote { get; init; }
    public string? ResponseByUserId { get; init; }
    public DateTimeOffset? ResponseAt { get; init; }

    public static string HashInputs(RiskScore score, ClaimRecommendation recommendation)
    {
        var builder = new StringBuilder();
        builder.Append(recommendation.Kind).Append('|').Append(score.Value).Append('|');
        foreach (var factor in score.Factors.OrderBy(factor => factor.Code, StringComparer.Ordinal))
        {
            builder.Append(factor.Code).Append(':').Append(factor.Weight).Append(':').Append(factor.Reading).Append(';');
        }

        foreach (var reason in recommendation.Reasons.OrderBy(reason => reason.Source.Id, StringComparer.Ordinal))
        {
            builder.Append(reason.Source.Kind).Append(':').Append(reason.Source.Id).Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..32];
    }
}

public interface IClaimAssessmentRepository
{
    Task SaveAsync(ClaimAssessment assessment, CancellationToken cancellationToken = default);

    Task<ClaimAssessment?> GetLatestAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

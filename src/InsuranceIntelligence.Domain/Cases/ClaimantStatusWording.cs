using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>What a customer reads about where their claim stands: a short headline and one or two
/// plain sentences.</summary>
public sealed record StatusWording(string Headline, string Detail);

/// <summary>
/// The customer's wording for each claim status (build-loop item 19). Staff see words such as
/// "Escalated" or "Ready for export"; a customer should read what it means for them. Every status has
/// a default below, and a workspace can replace any of them from Admin. The defaults are a starting
/// point written for this build and are to be confirmed by the insurer (and legal) before they are
/// relied on: they say nothing about cover, amounts or outcomes beyond what the status itself means.
/// </summary>
public static class ClaimantStatusWords
{
    public const int MaxHeadlineLength = 60;
    public const int MaxDetailLength = 400;

    private static readonly IReadOnlyDictionary<CaseStatus, StatusWording> DefaultWording = new Dictionary<CaseStatus, StatusWording>
    {
        [CaseStatus.New] = new("We have your claim", "We have received your claim and will confirm the details with you shortly."),
        [CaseStatus.Assigned] = new("Someone is looking after your claim", "A member of our claims team has been given your claim and will go through what you sent."),
        [CaseStatus.InReview] = new("We are reviewing your claim", "Our team is going through your claim and the documents you sent."),
        [CaseStatus.MissingDocuments] = new("We need something from you", "We cannot go further until we receive the documents listed below. Please send them when you can."),
        [CaseStatus.ReadyForReview] = new("Your claim is ready for a final look", "We have what we need. A senior colleague will look at your claim next."),
        [CaseStatus.PendingApproval] = new("Waiting for a decision", "Your claim is with the person who approves it. We will tell you as soon as there is an outcome."),
        [CaseStatus.ReadyForExport] = new("We are finalising your claim", "We are putting the final paperwork together and will contact you with the next step."),
        [CaseStatus.Escalated] = new("A senior colleague is looking at your claim", "Your claim has been passed to a senior colleague for a closer look. We will update you when we have news."),
        [CaseStatus.Settled] = new("Your claim has been settled", "Your claim has reached a settlement. Contact us if anything in the paperwork looks wrong."),
        [CaseStatus.Closed] = new("Your claim is closed", "This claim is now closed. If you think something is missing, ask us to look again."),
    };

    public static IReadOnlyDictionary<CaseStatus, StatusWording> Defaults => DefaultWording;

    public static StatusWording DefaultFor(CaseStatus status) =>
        DefaultWording.TryGetValue(status, out var wording) ? wording : new StatusWording("Your claim is being handled", "We will keep you updated.");

    /// <summary>The wording to show: the workspace's own for this status if it has one, otherwise the default.
    /// A workspace may replace only the headline or only the detail.</summary>
    public static StatusWording For(CaseStatus status, IReadOnlyDictionary<string, StatusWording>? overrides)
    {
        var fallback = DefaultFor(status);
        if (overrides is null || !overrides.TryGetValue(status.ToString(), out var custom))
        {
            return fallback;
        }

        return new StatusWording(
            string.IsNullOrWhiteSpace(custom.Headline) ? fallback.Headline : custom.Headline.Trim(),
            string.IsNullOrWhiteSpace(custom.Detail) ? fallback.Detail : custom.Detail.Trim());
    }

    /// <summary>Cleans what an administrator typed: unknown statuses are dropped, text is trimmed and scrubbed
    /// of dashes the platform does not use, and an entry equal to the default (or empty) is not kept.</summary>
    public static IReadOnlyDictionary<string, StatusWording>? Normalise(IReadOnlyDictionary<string, StatusWording>? input)
    {
        if (input is null)
        {
            return null;
        }

        var result = new Dictionary<string, StatusWording>(StringComparer.Ordinal);
        foreach (var (key, value) in input)
        {
            if (!Enum.TryParse<CaseStatus>(key?.Trim(), ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                continue;
            }

            var headline = DisplayText.WithoutEmDashes((value?.Headline ?? string.Empty).Trim());
            var detail = DisplayText.WithoutEmDashes((value?.Detail ?? string.Empty).Trim());
            var fallback = DefaultFor(status);
            if (headline == fallback.Headline) headline = string.Empty;
            if (detail == fallback.Detail) detail = string.Empty;
            if (headline.Length == 0 && detail.Length == 0)
            {
                continue;
            }

            result[status.ToString()] = new StatusWording(headline, detail);
        }

        return result.Count == 0 ? null : result;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<string, StatusWording>? overrides)
    {
        var errors = new List<string>();
        if (overrides is null)
        {
            return errors;
        }

        foreach (var (key, value) in overrides)
        {
            if (value.Headline.Length > MaxHeadlineLength)
            {
                errors.Add($"The headline for {key} can be at most {MaxHeadlineLength} characters.");
            }

            if (value.Detail.Length > MaxDetailLength)
            {
                errors.Add($"The explanation for {key} can be at most {MaxDetailLength} characters.");
            }
        }

        return errors;
    }

    public static bool SameAs(IReadOnlyDictionary<string, StatusWording>? left, IReadOnlyDictionary<string, StatusWording>? right)
    {
        left ??= new Dictionary<string, StatusWording>();
        right ??= new Dictionary<string, StatusWording>();
        return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var other) && other == pair.Value);
    }
}

public enum TimelineState
{
    Done,
    Current,
    Upcoming,
}

public sealed record ClaimantTimelineStep(string Key, string Label, TimelineState State);

/// <summary>
/// The steps a customer sees for their claim, taken from the product's own claim process (build-loop
/// item 19). A stage appears when it is meant for the customer: the default stages say so, and a stage a
/// product added shows only when it is marked visible and says at which claim status it is reached. A
/// product with no stages gets a plain five step timeline.
/// </summary>
public static class ClaimantTimeline
{
    private sealed record StageDefault(string Label, bool Visible, CaseStatus? Milestone);

    private static readonly IReadOnlyDictionary<string, StageDefault> StageDefaults = new Dictionary<string, StageDefault>(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultClaimStageLibrary.Intimation] = new("Claim received", true, CaseStatus.New),
        [DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest] = new("Documents and details", true, CaseStatus.Assigned),
        [DefaultClaimStageLibrary.AssessorOrSiteVisit] = new("Assessment", true, CaseStatus.InReview),
        [DefaultClaimStageLibrary.AdjustmentOrInvestigation] = new("Report prepared", true, CaseStatus.ReadyForReview),
        [DefaultClaimStageLibrary.OfferOrRepudiation] = new("Decision", true, CaseStatus.PendingApproval),
        [DefaultClaimStageLibrary.DischargeVoucher] = new("Paperwork", false, CaseStatus.ReadyForExport),
        [DefaultClaimStageLibrary.Payment] = new("Settlement", true, CaseStatus.Settled),
        [DefaultClaimStageLibrary.ComplaintEscalation] = new("Complaint", false, null),
    };

    private static readonly IReadOnlyList<(string Key, string Label, CaseStatus Milestone)> Fallback =
    [
        ("received", "Claim received", CaseStatus.New),
        ("review", "Under review", CaseStatus.Assigned),
        ("assessment", "Assessment", CaseStatus.InReview),
        ("decision", "Decision", CaseStatus.PendingApproval),
        ("settlement", "Settlement", CaseStatus.Settled),
    ];

    /// <summary>How far along the claim is, for comparing with a stage's milestone. Waiting on documents
    /// and escalation sit where the claim was working, not at the end.</summary>
    public static int Rank(CaseStatus status) => status switch
    {
        CaseStatus.New => 0,
        CaseStatus.Assigned => 1,
        CaseStatus.MissingDocuments => 1,
        CaseStatus.InReview => 2,
        CaseStatus.Escalated => 3,
        CaseStatus.ReadyForReview => 3,
        CaseStatus.PendingApproval => 4,
        CaseStatus.ReadyForExport => 5,
        CaseStatus.Settled => 6,
        CaseStatus.Closed => 6,
        _ => 0,
    };

    public static IReadOnlyList<ClaimantTimelineStep> Build(CaseStatus status, IReadOnlyList<ClaimStageDefinition>? stages)
    {
        var steps = new List<(string Key, string Label, CaseStatus Milestone)>();
        if (stages is { Count: > 0 })
        {
            foreach (var stage in stages.OrderBy(stage => stage.SortOrder))
            {
                StageDefaults.TryGetValue(stage.StageKey, out var defaults);
                var visible = stage.CustomerVisible ?? defaults?.Visible ?? false;
                var milestone = stage.MilestoneStatus ?? defaults?.Milestone;
                if (!visible || milestone is null)
                {
                    continue;
                }

                var label = !string.IsNullOrWhiteSpace(stage.CustomerLabel) ? stage.CustomerLabel.Trim() : defaults?.Label ?? stage.Name;
                steps.Add((stage.StageKey, label, milestone.Value));
            }
        }

        if (steps.Count == 0)
        {
            steps.AddRange(Fallback);
        }

        var finished = status is CaseStatus.Settled or CaseStatus.Closed;
        var rank = Rank(status);
        var currentIndex = -1;
        if (!finished)
        {
            // The current step is the first one the claim has not yet moved past.
            currentIndex = steps.FindIndex(step => Rank(step.Milestone) >= rank);
        }

        return steps
            .Select((step, index) => new ClaimantTimelineStep(
                step.Key,
                step.Label,
                finished ? TimelineState.Done : currentIndex < 0 || index < currentIndex ? TimelineState.Done : index == currentIndex ? TimelineState.Current : TimelineState.Upcoming))
            .ToArray();
    }
}

using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Cases;

/// <summary>What a customer is shown about where their claim stands.</summary>
public sealed record ClaimantStatusView(
    string Headline,
    string Detail,
    IReadOnlyList<ClaimantTimelineStep> Timeline,
    // When the claim first reached each step, by step key, from the recorded status changes. A step the
    // claim has not reached, or whose time was not recorded, has no entry.
    IReadOnlyDictionary<string, DateTimeOffset>? ReachedAt = null);

public interface IClaimantStatusPresenter
{
    /// <summary>The headline, explanation and timeline for a claim, in the claim's own workspace.</summary>
    Task<ClaimantStatusView> BuildAsync(ClaimCase claim, CancellationToken cancellationToken = default);

    /// <summary>Just the customer's short wording for a status in a workspace, for messages that name it.</summary>
    Task<StatusWording> WordingAsync(Domain.Common.TenantScope scope, CaseStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the customer's view of a claim's status (build-loop item 19) from the workspace's own wording
/// (or the defaults) and the claim's pinned product's stages. Customer-facing only: it names no staff
/// role, score or internal state.
/// </summary>
public sealed class ClaimantStatusPresenter(
    ITenantSettingsRepository settingsRepository,
    IProductRepository? productRepository = null,
    IAuditRepository? auditRepository = null) : IClaimantStatusPresenter
{
    public async Task<ClaimantStatusView> BuildAsync(ClaimCase claim, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(claim.Scope, cancellationToken);
        var wording = ClaimantStatusWords.For(claim.Status, settings.ClaimantStatusWording);

        IReadOnlyList<ClaimStageDefinition>? stages = null;
        if (productRepository is not null && claim.ProductId is { } productId && claim.ProductVersion is { } version)
        {
            stages = (await productRepository.GetVersionAsync(claim.Scope, productId, version, cancellationToken))?.Stages;
        }

        var timeline = ClaimantTimeline.Build(claim.Status, stages);
        return new ClaimantStatusView(wording.Headline, wording.Detail, timeline, await ReachedAtAsync(claim, timeline, cancellationToken));
    }

    private async Task<IReadOnlyDictionary<string, DateTimeOffset>> ReachedAtAsync(
        ClaimCase claim, IReadOnlyList<ClaimantTimelineStep> timeline, CancellationToken cancellationToken)
    {
        var reached = new Dictionary<string, DateTimeOffset>();
        var changes = new List<(DateTimeOffset At, int Rank)>();
        if (auditRepository is not null)
        {
            foreach (var auditEvent in await auditRepository.ListForCaseAsync(claim.CaseId, cancellationToken))
            {
                if (auditEvent.Scope == claim.Scope
                    && auditEvent.ActionName is "case.status.updated" or "case.status.changed"
                    && Enum.TryParse<CaseStatus>(auditEvent.AfterStatus, ignoreCase: true, out var after))
                {
                    changes.Add((auditEvent.Timestamp, ClaimantTimeline.Rank(after)));
                }
            }
        }

        changes.Add((claim.CreatedAt, ClaimantTimeline.Rank(CaseStatus.New)));
        changes = changes.OrderBy(change => change.At).ToList();
        foreach (var step in timeline.Where(step => step.State != TimelineState.Upcoming))
        {
            var rank = ClaimantTimeline.Rank(step.Milestone);
            var first = changes.Where(change => change.Rank >= rank).Select(change => (DateTimeOffset?)change.At).FirstOrDefault();
            if (first is { } at)
            {
                reached[step.Key] = at;
            }
        }

        return reached;
    }

    public async Task<StatusWording> WordingAsync(Domain.Common.TenantScope scope, CaseStatus status, CancellationToken cancellationToken = default) =>
        ClaimantStatusWords.For(status, (await settingsRepository.GetAsync(scope, cancellationToken)).ClaimantStatusWording);
}

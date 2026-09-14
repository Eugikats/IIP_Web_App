namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Names a real owner for a newly routed claim, closing the gap CaseIntakeService.cs left open:
/// a claim was routed to a team (ClaimRoutingPolicy) but never to a person, so "who is working
/// this?" had no answer until a supervisor happened to open it and assign it by hand.
///
/// There is no staff directory or team-roster table anywhere in this codebase (InsuranceUser is
/// declared but never wired to anything, and no ITeamRosterProvider exists), building one is a
/// materially bigger feature than "who gets the next claim." Rather than invent a roster, this
/// policy infers the routed team's current membership from case history: anyone who already holds
/// an open case on that team, for this tenant, is a real member of it right now. Among that
/// inferred roster, the least-loaded member gets the case; a tie (equal open-case count) breaks in
/// favour of whoever was assigned to that team least recently, so load naturally rotates.
///
/// A team no one has ever been assigned to yet falls back to the existing behaviour, team-only,
/// AssignedUserId null, exactly as before this policy existed. That is an honest "no one to name
/// yet," not a bug: the very first case on a brand-new team still needs a supervisor's first
/// manual assignment (CaseEndpoints.AssignCase, already a governed, audited action), which is
/// also how a genuinely new team's roster comes into existence for this policy to observe.
/// </summary>
public static class CaseAssignmentPolicy
{
    private static readonly CaseStatus[] OpenStatuses =
    [
        CaseStatus.New, CaseStatus.Assigned, CaseStatus.InReview, CaseStatus.MissingDocuments,
        CaseStatus.ReadyForReview, CaseStatus.PendingApproval, CaseStatus.ReadyForExport, CaseStatus.Escalated,
    ];

    /// <summary>The least-loaded existing member of <paramref name="team"/>, or null if the team
    /// has no case history yet to infer membership from.</summary>
    public static string? SelectAssignee(string team, IReadOnlyCollection<ClaimCase> existingCasesInScope)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            return null;
        }

        var teamCases = existingCasesInScope
            .Where(c => string.Equals(c.AssignedTeam, team, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(c.AssignedUserId))
            .ToArray();

        if (teamCases.Length == 0)
        {
            return null; // No one has ever worked this team's queue for this tenant yet.
        }

        var candidates = teamCases
            .GroupBy(c => c.AssignedUserId!, StringComparer.Ordinal)
            .Select(group => new
            {
                UserId = group.Key,
                OpenCaseCount = group.Count(c => OpenStatuses.Contains(c.Status)),
                MostRecentAssignmentAt = group.Max(c => c.UpdatedAt),
            })
            .OrderBy(candidate => candidate.OpenCaseCount)
            .ThenBy(candidate => candidate.MostRecentAssignmentAt) // round-robin among equally-loaded members
            .ToArray();

        return candidates[0].UserId;
    }
}

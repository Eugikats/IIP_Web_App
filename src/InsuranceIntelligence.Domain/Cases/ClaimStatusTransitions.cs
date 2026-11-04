namespace InsuranceIntelligence.Domain.Cases;

/// <summary>How a claim moves from one status to another.</summary>
public enum StatusChangePath
{
    /// <summary>The claim was filed.</summary>
    Intake,

    /// <summary>Someone was given the claim (the Assign action).</summary>
    Assignment,

    /// <summary>A person with the authority escalated it (the Escalate action), or the workflow rules did.</summary>
    Escalation,

    /// <summary>A person with the authority recorded it as settled or closed (the Resolve action).</summary>
    Resolution,

    /// <summary>An approval was decided and its next step executed (the Approvals queue).</summary>
    Approval,

    /// <summary>The workflow rules moved it after looking at the claim's documents and signals.</summary>
    Workflow,

    /// <summary>An officer changed it with the one audited "Change status" action.</summary>
    ManualChange,
}

/// <summary>One allowed move between statuses and the way it happens.</summary>
public sealed record StatusTransition(CaseStatus From, CaseStatus To, StatusChangePath Path, bool RequiresSupervisor = false);

/// <summary>
/// Every move a claim's status can make, and how (build-loop item 20). The platform used to change a claim's
/// status from five different places with no single list; this is the list. Only the moves marked
/// <see cref="StatusChangePath.ManualChange"/> are available from the "Change status" action. The rest keep
/// their own actions and their own rules (assigning, escalating, the approvals queue, resolving), so a
/// consequential step such as sending a claim to approval or recording a settlement cannot be reached by
/// choosing a status from a list.
/// </summary>
public static class ClaimStatusTransitions
{
    private static readonly IReadOnlyList<StatusTransition> Table = Build();

    public static IReadOnlyList<StatusTransition> All => Table;

    private static IReadOnlyList<StatusTransition> Build()
    {
        var all = new List<StatusTransition>
        {
            // Filing and assigning.
            new(CaseStatus.New, CaseStatus.Assigned, StatusChangePath.Assignment),
            new(CaseStatus.New, CaseStatus.Assigned, StatusChangePath.Intake),

            // The workflow rules look at a claim and move it along.
            new(CaseStatus.New, CaseStatus.InReview, StatusChangePath.Workflow),
            new(CaseStatus.Assigned, CaseStatus.InReview, StatusChangePath.Workflow),
            new(CaseStatus.New, CaseStatus.MissingDocuments, StatusChangePath.Workflow),
            new(CaseStatus.Assigned, CaseStatus.MissingDocuments, StatusChangePath.Workflow),
            new(CaseStatus.InReview, CaseStatus.MissingDocuments, StatusChangePath.Workflow),
            new(CaseStatus.MissingDocuments, CaseStatus.ReadyForReview, StatusChangePath.Workflow),
            new(CaseStatus.InReview, CaseStatus.ReadyForReview, StatusChangePath.Workflow),
            new(CaseStatus.ReadyForReview, CaseStatus.PendingApproval, StatusChangePath.Workflow),
            new(CaseStatus.InReview, CaseStatus.PendingApproval, StatusChangePath.Workflow),

            // An approved step ends in the hand-off for export.
            new(CaseStatus.PendingApproval, CaseStatus.ReadyForExport, StatusChangePath.Approval),

            // Escalation, by a person or by the workflow rules, from any open status.
            new(CaseStatus.New, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.Assigned, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.InReview, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.MissingDocuments, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.ReadyForReview, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.PendingApproval, CaseStatus.Escalated, StatusChangePath.Escalation),
            new(CaseStatus.ReadyForExport, CaseStatus.Escalated, StatusChangePath.Escalation),

            // Recording the end of a claim.
            new(CaseStatus.ReadyForExport, CaseStatus.Settled, StatusChangePath.Resolution),
            new(CaseStatus.ReadyForExport, CaseStatus.Closed, StatusChangePath.Resolution),
            new(CaseStatus.Escalated, CaseStatus.Settled, StatusChangePath.Resolution),
            new(CaseStatus.Escalated, CaseStatus.Closed, StatusChangePath.Resolution),

            // The one audited "Change status" action: working the claim back and forth between the
            // states an officer is allowed to decide for themselves.
            new(CaseStatus.New, CaseStatus.InReview, StatusChangePath.ManualChange),
            new(CaseStatus.New, CaseStatus.MissingDocuments, StatusChangePath.ManualChange),
            new(CaseStatus.Assigned, CaseStatus.InReview, StatusChangePath.ManualChange),
            new(CaseStatus.Assigned, CaseStatus.MissingDocuments, StatusChangePath.ManualChange),
            new(CaseStatus.InReview, CaseStatus.MissingDocuments, StatusChangePath.ManualChange),
            new(CaseStatus.InReview, CaseStatus.ReadyForReview, StatusChangePath.ManualChange),
            new(CaseStatus.MissingDocuments, CaseStatus.InReview, StatusChangePath.ManualChange),
            new(CaseStatus.MissingDocuments, CaseStatus.ReadyForReview, StatusChangePath.ManualChange),
            new(CaseStatus.ReadyForReview, CaseStatus.InReview, StatusChangePath.ManualChange),
            new(CaseStatus.ReadyForReview, CaseStatus.MissingDocuments, StatusChangePath.ManualChange),

            // Taking a claim out of escalation, or reopening a closed one, is a supervisor's call.
            new(CaseStatus.Escalated, CaseStatus.InReview, StatusChangePath.ManualChange, RequiresSupervisor: true),
            new(CaseStatus.Closed, CaseStatus.InReview, StatusChangePath.ManualChange, RequiresSupervisor: true),
        };
        return all;
    }

    /// <summary>The moves an officer can make from this status with the "Change status" action.</summary>
    public static IReadOnlyList<StatusTransition> ManualFrom(CaseStatus from) =>
        Table.Where(transition => transition.From == from && transition.Path == StatusChangePath.ManualChange).ToArray();

    public static StatusTransition? FindManual(CaseStatus from, CaseStatus to) =>
        Table.FirstOrDefault(transition => transition.From == from && transition.To == to && transition.Path == StatusChangePath.ManualChange);

    /// <summary>In words a claims officer can act on: why this status cannot be chosen from the list, and what to use instead.</summary>
    public static string HowItIsReached(CaseStatus to) => to switch
    {
        CaseStatus.New => "A claim starts here when it is filed.",
        CaseStatus.Assigned => "Use Assign to give the claim to someone.",
        CaseStatus.InReview => "Choose it with Change status, or the workflow rules set it.",
        CaseStatus.MissingDocuments => "Choose it with Change status, or the workflow rules set it when required documents are missing.",
        CaseStatus.ReadyForReview => "Choose it with Change status, or the workflow rules set it when the evidence is complete.",
        CaseStatus.PendingApproval => "The workflow rules set it when a supervisor has to approve a step. It is decided in the Approvals queue.",
        CaseStatus.ReadyForExport => "It is reached only when an approved step is executed in the Approvals queue.",
        CaseStatus.Escalated => "Use Escalate, with the reason, or the workflow rules do it.",
        CaseStatus.Settled => "Use Resolve, which needs a supervisor, to record a settlement.",
        CaseStatus.Closed => "Use Resolve, which needs a supervisor, to close a claim.",
        _ => "Not available.",
    };

    /// <summary>Every status that can be reached from a newly filed claim by some defined path.</summary>
    public static IReadOnlySet<CaseStatus> ReachableFromNew()
    {
        var reached = new HashSet<CaseStatus> { CaseStatus.New };
        var queue = new Queue<CaseStatus>([CaseStatus.New]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var transition in Table.Where(transition => transition.From == current))
            {
                if (reached.Add(transition.To))
                {
                    queue.Enqueue(transition.To);
                }
            }
        }

        return reached;
    }
}

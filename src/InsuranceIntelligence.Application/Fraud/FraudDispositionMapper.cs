using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Application.Fraud;

internal static class FraudDispositionMapper
{
    public static FraudDispositionDto ToDto(FraudSignalDisposition disposition) =>
        new(
            disposition.Status.ToString(),
            disposition.AssignedToUserId,
            disposition.Outcome?.ToString(),
            disposition.OutcomeNotes,
            disposition.CreatedByUserId,
            disposition.CreatedAt,
            disposition.UpdatedAt,
            Allowed(disposition.Status),
            Blocked(disposition.Status));

    public static IReadOnlyCollection<FraudDispositionActionDto> AllowedForUntriaged() =>
        [Action("triageFraudSignal", "Triage signal")];

    public static IReadOnlyCollection<FraudDispositionActionDto> BlockedForUntriaged() =>
        [
            Action("assignFraudSignal", "Assign for investigation", "The signal must be triaged first."),
            Action("recordFraudDispositionOutcome", "Record outcome", "The signal must be triaged first."),
            Action("closeFraudSignal", "Close signal", "The signal must be triaged first."),
        ];

    private static IReadOnlyCollection<FraudDispositionActionDto> Allowed(FraudDispositionStatus status)
    {
        var actions = new List<FraudDispositionActionDto>();
        if (status is FraudDispositionStatus.Triaged or FraudDispositionStatus.UnderInvestigation)
        {
            actions.Add(Action("assignFraudSignal", "Assign for investigation"));
        }

        if (status == FraudDispositionStatus.UnderInvestigation)
        {
            actions.Add(Action("recordFraudDispositionOutcome", "Record outcome"));
        }

        if (status == FraudDispositionStatus.OutcomeRecorded)
        {
            actions.Add(Action("closeFraudSignal", "Close signal"));
        }

        return actions;
    }

    private static IReadOnlyCollection<FraudDispositionActionDto> Blocked(FraudDispositionStatus status)
    {
        var actions = new List<FraudDispositionActionDto>();
        if (status is not (FraudDispositionStatus.Triaged or FraudDispositionStatus.UnderInvestigation))
        {
            actions.Add(Action("assignFraudSignal", "Assign for investigation", ReasonFor(status, "assigned")));
        }

        if (status != FraudDispositionStatus.UnderInvestigation)
        {
            actions.Add(Action("recordFraudDispositionOutcome", "Record outcome", ReasonFor(status, "have its outcome recorded")));
        }

        if (status != FraudDispositionStatus.OutcomeRecorded)
        {
            actions.Add(Action("closeFraudSignal", "Close signal", ReasonFor(status, "closed")));
        }

        return actions;
    }

    private static string ReasonFor(FraudDispositionStatus status, string verb) =>
        status == FraudDispositionStatus.Closed
            ? "This signal is already closed."
            : $"A signal in {status} status cannot currently be {verb}.";

    private static FraudDispositionActionDto Action(string action, string label, string? reason = null) =>
        new(action, label, Permission.ViewFraudSignals.ToString(), reason);
}

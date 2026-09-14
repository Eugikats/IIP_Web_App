using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Application.Fraud;

public sealed class FraudSignalNotFoundException : Exception;

public sealed class FraudDispositionConflictException(string message, string blockedAction) : Exception(message)
{
    public string BlockedAction { get; } = blockedAction;
}

// WO-19: human accountability on every transition (I7), every method here requires a real actor,
// a real audit reason, and writes a real AuditEvent. Outcomes are process outcomes only, never an
// adverse fraud determination (see FraudDispositionOutcome).
public interface IFraudDispositionService
{
    Task<FraudDispositionDto> TriageAsync(
        string ruleId, IReadOnlyList<Guid> relatedCaseIds, string auditReason, string correlationId, CancellationToken cancellationToken = default);

    Task<FraudDispositionDto> AssignAsync(
        Guid signalId, string assignedToUserId, string auditReason, string correlationId, CancellationToken cancellationToken = default);

    Task<FraudDispositionDto> RecordOutcomeAsync(
        Guid signalId, FraudDispositionOutcome outcome, string? notes, string auditReason, string correlationId, CancellationToken cancellationToken = default);

    Task<FraudDispositionDto> CloseAsync(
        Guid signalId, string auditReason, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class FraudDispositionService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IFraudSignalDispositionRepository repository,
    IAuditRepository auditRepository) : IFraudDispositionService
{
    public async Task<FraudDispositionDto> TriageAsync(
        string ruleId, IReadOnlyList<Guid> relatedCaseIds, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);
        var context = tenantContextAccessor.Current;
        var signalId = FraudSignalDisposition.DeterministicId(ruleId, relatedCaseIds);

        var existing = await repository.GetAsync(signalId, context.Scope, cancellationToken);
        if (existing is not null)
        {
            throw new FraudDispositionConflictException("This signal has already been triaged.", "triageFraudSignal");
        }

        var now = DateTimeOffset.UtcNow;
        var disposition = new FraudSignalDisposition
        {
            SignalId = signalId,
            Scope = context.Scope,
            RuleId = ruleId,
            RelatedCaseIds = relatedCaseIds,
            Status = FraudDispositionStatus.Triaged,
            CreatedByUserId = context.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };
        await repository.SaveAsync(disposition, cancellationToken);
        await WriteAuditAsync(disposition, "fraud_signal.triaged", null, "Triaged", auditReason, correlationId, cancellationToken);

        return FraudDispositionMapper.ToDto(disposition);
    }

    public async Task<FraudDispositionDto> AssignAsync(
        Guid signalId, string assignedToUserId, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);
        var context = tenantContextAccessor.Current;
        var disposition = await RequireAsync(signalId, context.Scope, cancellationToken);

        if (disposition.Status is not (FraudDispositionStatus.Triaged or FraudDispositionStatus.UnderInvestigation))
        {
            throw new FraudDispositionConflictException(
                "Only a triaged or already-under-investigation signal can be assigned for investigation.", "assignFraudSignal");
        }

        var before = disposition.Status.ToString();
        var updated = disposition with
        {
            Status = FraudDispositionStatus.UnderInvestigation,
            AssignedToUserId = assignedToUserId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync(updated, "fraud_signal.assigned", before, "UnderInvestigation", auditReason, correlationId, cancellationToken);

        return FraudDispositionMapper.ToDto(updated);
    }

    public async Task<FraudDispositionDto> RecordOutcomeAsync(
        Guid signalId, FraudDispositionOutcome outcome, string? notes, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);
        var context = tenantContextAccessor.Current;
        var disposition = await RequireAsync(signalId, context.Scope, cancellationToken);

        if (disposition.Status != FraudDispositionStatus.UnderInvestigation)
        {
            throw new FraudDispositionConflictException(
                "An outcome can only be recorded for a signal under investigation.", "recordFraudDispositionOutcome");
        }

        var updated = disposition with
        {
            Status = FraudDispositionStatus.OutcomeRecorded,
            Outcome = outcome,
            OutcomeNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync(updated, "fraud_signal.outcome_recorded", "UnderInvestigation", "OutcomeRecorded", auditReason, correlationId, cancellationToken);

        return FraudDispositionMapper.ToDto(updated);
    }

    public async Task<FraudDispositionDto> CloseAsync(
        Guid signalId, string auditReason, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);
        var context = tenantContextAccessor.Current;
        var disposition = await RequireAsync(signalId, context.Scope, cancellationToken);

        if (disposition.Status != FraudDispositionStatus.OutcomeRecorded)
        {
            throw new FraudDispositionConflictException(
                "A signal can only be closed after an outcome has been recorded.", "closeFraudSignal");
        }

        var updated = disposition with
        {
            Status = FraudDispositionStatus.Closed,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync(updated, "fraud_signal.closed", "OutcomeRecorded", "Closed", auditReason, correlationId, cancellationToken);

        return FraudDispositionMapper.ToDto(updated);
    }

    private async Task<FraudSignalDisposition> RequireAsync(Guid signalId, Domain.Common.TenantScope scope, CancellationToken cancellationToken) =>
        await repository.GetAsync(signalId, scope, cancellationToken) ?? throw new FraudSignalNotFoundException();

    private Task WriteAuditAsync(
        FraudSignalDisposition disposition,
        string actionName,
        string? beforeStatus,
        string afterStatus,
        string auditReason,
        string correlationId,
        CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                disposition.Scope,
                CaseId: disposition.RelatedCaseIds.Count == 1 ? disposition.RelatedCaseIds[0] : null,
                DocumentId: disposition.SignalId.ToString(),
                UserId: tenantContextAccessor.Current.UserId,
                ActorType.User,
                actionName,
                DateTimeOffset.UtcNow,
                beforeStatus,
                afterStatus,
                disposition.RelatedCaseIds.Select(id => id.ToString()).ToArray(),
                ModelProvider: null,
                Confidence: null,
                ValidationResult: "accepted",
                ApprovalId: null,
                AuditReason: auditReason,
                CorrelationId: correlationId),
            cancellationToken);
}

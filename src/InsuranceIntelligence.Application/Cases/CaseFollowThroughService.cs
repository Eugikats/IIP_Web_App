using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.Application.Cases;

public sealed record CaseFollowThroughResult(int MissingDocumentRemindersSent, int SlaWarningsSent);

/// <summary>
/// "Follow through without a person remembering" (build prompt outcome G): a customer reminder
/// once documents have been missing for the configured wait, and an officer warning before an SLA
/// breach rather than after it. Both idempotent (a reminder only repeats after the wait elapses
/// again; a warning fires at most once per claim, via NotificationService.NotifyOnceAsync's own
/// dedup), both audited. Run by CaseFollowThroughTimerFunction on a schedule; also callable
/// on demand for tests and manual runs, same code either way.
/// </summary>
public interface ICaseFollowThroughService
{
    Task<CaseFollowThroughResult> RunAsync(TenantScope scope, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class CaseFollowThroughService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IMissingDocumentRequestService missingDocumentRequestService,
    INotificationEmitter notificationEmitter,
    INotificationRepository notificationRepository,
    IAuditRepository auditRepository,
    IDocumentChecklistProvider? checklistProvider = null) : ICaseFollowThroughService
{
    private const string SystemActorId = "system:case-follow-through";

    // Fixed constants today; the build contract names these as a future tenant setting
    // ("timings are settings", outcome G), same status as ClaimBriefRanking's weights.
    private static readonly TimeSpan MissingDocumentReminderWait = TimeSpan.FromDays(3);
    private static readonly TimeSpan SlaWarningLeadTime = TimeSpan.FromHours(24);

    public async Task<CaseFollowThroughResult> RunAsync(TenantScope scope, string correlationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var allCases = await caseRepository.ListAsync(scope, cancellationToken);
        var openCases = allCases
            .Where(claimCase => claimCase.Status is not (CaseStatus.Settled or CaseStatus.Closed) && !claimCase.RequiresConfirmation)
            .ToArray();

        var remindersSent = await SendMissingDocumentRemindersAsync(openCases, scope, now, correlationId, cancellationToken);
        var warningsSent = await SendSlaWarningsAsync(openCases, scope, now, correlationId, cancellationToken);

        return new CaseFollowThroughResult(remindersSent, warningsSent);
    }

    private async Task<int> SendMissingDocumentRemindersAsync(
        IReadOnlyCollection<ClaimCase> openCases, TenantScope scope, DateTimeOffset now, string correlationId, CancellationToken cancellationToken)
    {
        var sent = 0;
        foreach (var claimCase in openCases)
        {
            var lastPrompt = claimCase.LastMissingDocumentReminderAt ?? claimCase.CreatedAt;
            if (now - lastPrompt < MissingDocumentReminderWait)
            {
                continue;
            }

            var documents = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            var checklist = await BuildChecklistAsync(claimCase, documents, cancellationToken);
            if (checklist.MissingRequiredDocuments.Count == 0)
            {
                continue;
            }

            var outcome = await missingDocumentRequestService.SendAsync(claimCase, checklist, correlationId, whatsAppBusinessPhoneNumberId: null, cancellationToken);

            // Stamp the attempt regardless of delivery outcome, not only a successful send: a
            // recipient that genuinely has no contact on file or a provider that is not configured
            // will not become sendable by retrying every run, and stamping prevents hammering it.
            await caseRepository.SaveAsync(claimCase with { LastMissingDocumentReminderAt = now, UpdatedAt = now }, cancellationToken);
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, claimCase.CaseId, null, SystemActorId, ActorType.System,
                    "case.follow_through.missing_document_reminder", now, null, outcome.ToString(),
                    [], "CaseFollowThroughService", null, outcome.ToString(), null,
                    $"Missing-document reminder for case {claimCase.CaseId}: {outcome}.", correlationId),
                cancellationToken);

            if (outcome == MissingDocumentRequestOutcome.Sent)
            {
                sent++;
            }
        }

        return sent;
    }

    private async Task<int> SendSlaWarningsAsync(
        IReadOnlyCollection<ClaimCase> openCases, TenantScope scope, DateTimeOffset now, string correlationId, CancellationToken cancellationToken)
    {
        var sent = 0;
        foreach (var claimCase in openCases)
        {
            if (claimCase.SlaDueAt is not { } dueAt || string.IsNullOrWhiteSpace(claimCase.AssignedUserId))
            {
                continue;
            }

            // Already breached: that is SlaBreach's job, recomputed reactively wherever a claim is
            // read (CaseEndpoints.ListCases). This is only ever the warning that comes before.
            if (dueAt <= now || dueAt - now > SlaWarningLeadTime)
            {
                continue;
            }

            var alreadyWarned = await notificationRepository.ExistsAsync(scope, claimCase.AssignedUserId, NotificationType.SlaAtRisk, claimCase.CaseId, cancellationToken);
            if (alreadyWarned)
            {
                continue;
            }

            await notificationEmitter.NotifyOnceAsync(
                scope,
                claimCase.AssignedUserId,
                NotificationType.SlaAtRisk,
                $"{claimCase.ClaimType} claim for {claimCase.CustomerName} is due within a working day.",
                claimCase.CaseId,
                cancellationToken);
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, claimCase.CaseId, null, SystemActorId, ActorType.System,
                    "case.follow_through.sla_warning", now, null, "warned",
                    [], "CaseFollowThroughService", null, "warned", null,
                    $"SLA-at-risk warning sent for case {claimCase.CaseId}.", correlationId),
                cancellationToken);
            sent++;
        }

        return sent;
    }

    /// <summary>The claim's checklist from the shared provider, so a reminder asks for exactly the
    /// documents the customer's own page asks for (the pinned product's matrix for this claim type).
    /// With no provider wired (a test or host that has not configured products) it falls back to
    /// the built-in claim-type rules, as it always did.</summary>
    private async Task<DocumentCompletenessChecklist> BuildChecklistAsync(ClaimCase claimCase, IReadOnlyCollection<CaseDocument> documents, CancellationToken cancellationToken)
    {
        if (checklistProvider is not null)
        {
            return await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        }

        return BuildBuiltInChecklist(claimCase, documents);
    }

    private static DocumentCompletenessChecklist BuildBuiltInChecklist(ClaimCase claimCase, IReadOnlyCollection<CaseDocument> documents)
    {
        var rules = DocumentChecklistRules.ForCase(claimCase);
        var received = documents
            .Where(document => document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved)
            .Select(document => document.DocumentType)
            .Distinct()
            .ToArray();
        var score = CompletenessScore.Calculate(rules.RequiredDocuments, received);
        var missing = rules.RequiredDocuments
            .Where(required => !received.Contains(required))
            .Select(required => new MissingDocumentRequirement(required.ToString(), StaticDocumentTypeLabel.For(required), Required: true))
            .ToArray();

        return new DocumentCompletenessChecklist(
            claimCase.CaseId, claimCase.ProductLine, rules.RequiredDocuments, rules.OptionalDocuments, received, missing, score);
    }
}

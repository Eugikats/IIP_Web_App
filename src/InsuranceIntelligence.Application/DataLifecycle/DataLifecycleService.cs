using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.DataLifecycle;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Storage;

namespace InsuranceIntelligence.Application.DataLifecycle;

public sealed record MatchedClaim(Guid CaseId, string Reference, string Status, bool OnLegalHold, string? LegalHoldReason, bool AlreadyErased);

public sealed record ErasurePreview(
    IReadOnlyList<MatchedClaim> Claims,
    IReadOnlyDictionary<string, int> Counts,
    int ClaimsThatWillBeErased,
    int ClaimsOnHold,
    bool NothingFound);

public sealed record HeldClaim(Guid CaseId, string Reference, string Status, string Reason, string PlacedBy, DateTimeOffset PlacedAt);

/// <summary>
/// Retention, deletion and the erasure of one person's data (build-loop item 34). Three rules run through all of it. Nothing is deleted
/// unless the insurer set a period or an administrator asked for an erasure with a reason. A claim on legal hold is never touched by either.
/// And a claim's stored files are deleted before its records, so a failure leaves the records that show what is left to delete, and a retry
/// finishes the job.
/// </summary>
public sealed class DataLifecycleService(
    IDataLifecycleStore store,
    ICaseRepository caseRepository,
    ITenantSettingsRepository settingsRepository,
    IAuditRepository auditRepository,
    IDocumentStorageProvider documentStorage,
    IEvidencePackStorageProvider packStorage,
    TimeProvider? clock = null)
{
    private const string SystemActor = "system:retention";
    private const int DocumentBatch = 500;
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    private DateTimeOffset Now => time.GetUtcNow();

    // ------------------------------------------------------------------ legal hold

    public async Task<IReadOnlyList<HeldClaim>> ListHeldAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        (await caseRepository.ListAsync(scope, cancellationToken))
        .Where(claim => claim.IsOnLegalHold)
        .OrderBy(claim => claim.LegalHoldPlacedAt)
        .Select(claim => new HeldClaim(claim.CaseId, ClaimReference.For(claim.CaseId), claim.Status.ToString(), claim.LegalHoldReason ?? string.Empty, claim.LegalHoldPlacedBy ?? string.Empty, claim.LegalHoldPlacedAt!.Value))
        .ToArray();

    public async Task<(HeldClaim? Held, string? Problem)> PlaceHoldAsync(TenantScope scope, string actor, string? claimReference, string? reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var why = (reason ?? string.Empty).Trim();
        if (why.Length < 5 || why.Length > 500)
        {
            return (null, "Say why the claim is being held (5 to 500 characters), for example the court case or investigation.");
        }

        var matches = (await caseRepository.ListAsync(scope, cancellationToken)).Where(claim => ClaimReference.Matches(claim.CaseId, claimReference)).ToArray();
        if (matches.Length != 1)
        {
            return (null, matches.Length == 0 ? "No claim in this workspace has that reference." : "More than one claim has that reference. Use the claim's full reference from its page.");
        }

        var claim = matches[0];
        if (claim.IsOnLegalHold)
        {
            return (null, "That claim is already on legal hold.");
        }

        var held = claim with { LegalHoldReason = why, LegalHoldPlacedBy = actor, LegalHoldPlacedAt = Now, UpdatedAt = Now };
        await caseRepository.SaveAsync(held, cancellationToken);
        await Audit(scope, actor, "data.legal_hold.placed", $"Claim {ClaimReference.For(claim.CaseId)} was placed on legal hold: {why}", correlationId, claim.CaseId, cancellationToken);
        return (new HeldClaim(claim.CaseId, ClaimReference.For(claim.CaseId), held.Status.ToString(), why, actor, held.LegalHoldPlacedAt!.Value), null);
    }

    public async Task<bool> ReleaseHoldAsync(TenantScope scope, string actor, Guid caseId, string correlationId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || !claim.Scope.Matches(scope) || !claim.IsOnLegalHold)
        {
            return false;
        }

        await caseRepository.SaveAsync(claim with { LegalHoldReason = null, LegalHoldPlacedBy = null, LegalHoldPlacedAt = null, UpdatedAt = Now }, cancellationToken);
        await Audit(scope, actor, "data.legal_hold.released", $"Legal hold on claim {ClaimReference.For(caseId)} was released.", correlationId, caseId, cancellationToken);
        return true;
    }

    // ------------------------------------------------------------------ erasure of one person's data

    private async Task<(ErasureSubject Subject, IReadOnlyList<ClaimCase> Claims)> ResolveAsync(TenantScope scope, ErasureSubject subject, CancellationToken cancellationToken)
    {
        var claims = (await caseRepository.ListRelatedAsync(scope, subject.NormalisedPolicy is { } policy ? [policy] : [], subject.Phone, subject.NormalisedEmail, cancellationToken))
            .Where(claim => subject.Matches(claim.CustomerPhone, claim.CustomerEmail, claim.PolicyNumber))
            .OrderBy(claim => claim.CreatedAt)
            .ToArray();
        // The person's other contact details on those claims lead to their chats and mail too.
        return (subject.WithContactsFrom(claims.Select(claim => ((string?)claim.CustomerPhone, (string?)claim.CustomerEmail))), claims);
    }

    public async Task<(ErasurePreview? Preview, string? Problem)> PreviewErasureAsync(TenantScope scope, ErasureSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return (null, "Give a phone number (with its country code), an email address or a policy number.");
        }

        var (expanded, claims) = await ResolveAsync(scope, subject, cancellationToken);
        var counts = new Dictionary<string, int>();
        foreach (var claim in claims.Where(claim => !claim.IsOnLegalHold))
        {
            Add(counts, (await store.FootprintAsync(scope, claim.CaseId, cancellationToken)).Counts);
        }

        Add(counts, await store.SubjectDataAsync(scope, expanded, delete: false, cancellationToken));
        var erasable = claims.Count(claim => !claim.IsOnLegalHold);
        counts[DataCategories.Claims] = erasable;
        var matched = claims.Select(claim => new MatchedClaim(claim.CaseId, ClaimReference.For(claim.CaseId), claim.Status.ToString(), claim.IsOnLegalHold, claim.LegalHoldReason, claim.PersonalDataErasedAt is not null)).ToArray();
        return (new ErasurePreview(matched, counts, erasable, claims.Count(claim => claim.IsOnLegalHold), claims.Count == 0 && counts.Where(pair => pair.Key != DataCategories.Claims).All(pair => pair.Value == 0)), null);
    }

    public async Task<(DeletionRecord? Record, string? Problem)> EraseAsync(TenantScope scope, string actor, ErasureSubject subject, string? reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var why = (reason ?? string.Empty).Trim();
        if (subject.IsEmpty)
        {
            return (null, "Give a phone number (with its country code), an email address or a policy number.");
        }

        if (why.Length < 5 || why.Length > 500)
        {
            return (null, "Say why (5 to 500 characters), for example the request received and its date.");
        }

        var (expanded, claims) = await ResolveAsync(scope, subject, cancellationToken);
        var counts = new Dictionary<string, int>();
        var failures = new List<string>();
        var held = claims.Where(claim => claim.IsOnLegalHold).Select(claim => ClaimReference.For(claim.CaseId)).ToList();
        var files = 0;
        foreach (var claim in claims.Where(claim => !claim.IsOnLegalHold))
        {
            var reference = ClaimReference.For(claim.CaseId);
            try
            {
                files += await DeleteStoredFilesAsync(scope, claim.CaseId, cancellationToken);
                Add(counts, await store.DeleteCaseDataAsync(scope, claim.CaseId, includeClaim: false, cancellationToken));
                await caseRepository.SaveAsync(Anonymised(claim), cancellationToken);
                counts[DataCategories.Claims] = counts.GetValueOrDefault(DataCategories.Claims) + 1;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add($"{reference}: {Describe(exception)}");
            }
        }

        Add(counts, await store.SubjectDataAsync(scope, expanded, delete: true, cancellationToken));
        var record = new DeletionRecord
        {
            DeletionId = Guid.NewGuid(),
            Scope = scope,
            Kind = DeletionKinds.Erasure,
            Subject = subject.Fingerprint(),
            Reason = why,
            Actor = actor,
            At = Now,
            Counts = counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value),
            StoredFilesDeleted = files,
            HeldClaims = held,
            Failures = failures,
        };
        await store.SaveDeletionAsync(record, cancellationToken);
        await Audit(scope, actor, "data.erasure.completed",
            $"A person's data was erased ({record.Total} records, {files} stored files; {held.Count} claim(s) kept on legal hold; {failures.Count} failure(s)). Reference {record.DeletionId}.",
            correlationId, null, cancellationToken);
        return (record, null);
    }

    /// <summary>What stays of a claim once the person's data is erased: its reference, status, dates, amounts and policy number, because an insurer must keep a record of the claim. Everything that says who the person was, or what they wrote, goes.</summary>
    private ClaimCase Anonymised(ClaimCase claim) =>
        claim with
        {
            CustomerName = "Erased",
            CustomerEmail = null,
            CustomerPhone = null,
            IncidentDescription = "Removed at the data subject's request.",
            OriginalIncidentText = null,
            OriginalIncidentLanguage = null,
            FieldValues = null,
            FieldProvenance = null,
            MotorFnolDetails = null,
            MedicalFnolDetails = null,
            AdvisoryRiskSignals = [],
            PersonalDataErasedAt = Now,
            UpdatedAt = Now,
        };

    // ------------------------------------------------------------------ retention periods

    public async Task<IReadOnlyList<DeletionRecord>> RunRetentionAllAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<DeletionRecord>();
        foreach (var scope in await store.ListScopesWithRetentionAsync(cancellationToken))
        {
            try
            {
                records.AddRange(await RunRetentionAsync(scope, SystemActor, "retention", cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One workspace's fault must not stop the others; it is tried again at the next pass.
                await Audit(scope, SystemActor, "data.retention.failed", $"The retention pass could not finish ({exception.GetType().Name}).", "retention", null, cancellationToken);
            }
        }

        return records;
    }

    /// <summary>Applies each retention period the workspace set. Returns one record for each kind of data that something was deleted from.</summary>
    public async Task<IReadOnlyList<DeletionRecord>> RunRetentionAsync(TenantScope scope, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        var records = new List<DeletionRecord>();
        var claims = (await caseRepository.ListAsync(scope, cancellationToken)).ToArray();
        var held = claims.Where(claim => claim.IsOnLegalHold).Select(claim => claim.CaseId).ToArray();
        var heldReferences = held.Select(ClaimReference.For).ToArray();

        // Conversations and messages first, then communications and documents, then whole claims, so a claim is the last thing to go.
        if (MonthsFor(settings, RetentionClasses.Conversations) is > 0 and var chatMonths)
        {
            var counts = await store.DeleteConversationsBeforeAsync(scope, Now.AddMonths(-chatMonths), held, cancellationToken);
            await RecordAsync(scope, actor, RetentionClasses.Conversations, chatMonths, counts, 0, heldReferences, [], correlationId, records, cancellationToken);
        }

        if (MonthsFor(settings, RetentionClasses.Communications) is > 0 and var messageMonths)
        {
            var deleted = await store.DeleteCommunicationsBeforeAsync(scope, Now.AddMonths(-messageMonths), held, cancellationToken);
            await RecordAsync(scope, actor, RetentionClasses.Communications, messageMonths, new Dictionary<string, int> { [DataCategories.Communications] = deleted }, 0, heldReferences, [], correlationId, records, cancellationToken);
        }

        if (MonthsFor(settings, RetentionClasses.Documents) is > 0 and var documentMonths)
        {
            var (deleted, files, failures) = await DeleteExpiredDocumentsAsync(scope, Now.AddMonths(-documentMonths), held, cancellationToken);
            await RecordAsync(scope, actor, RetentionClasses.Documents, documentMonths, new Dictionary<string, int> { [DataCategories.Documents] = deleted }, files, heldReferences, failures, correlationId, records, cancellationToken);
        }

        if (MonthsFor(settings, RetentionClasses.ClosedClaims) is > 0 and var claimMonths)
        {
            var cutoff = Now.AddMonths(-claimMonths);
            var counts = new Dictionary<string, int>();
            var failures = new List<string>();
            var files = 0;
            foreach (var claim in claims.Where(claim => !claim.IsOnLegalHold && claim.Status is CaseStatus.Closed or CaseStatus.Settled && claim.UpdatedAt < cutoff))
            {
                try
                {
                    files += await DeleteStoredFilesAsync(scope, claim.CaseId, cancellationToken);
                    Add(counts, await store.DeleteCaseDataAsync(scope, claim.CaseId, includeClaim: true, cancellationToken));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add($"{ClaimReference.For(claim.CaseId)}: {Describe(exception)}");
                }
            }

            await RecordAsync(scope, actor, RetentionClasses.ClosedClaims, claimMonths, counts, files, heldReferences, failures, correlationId, records, cancellationToken);
        }

        return records;
    }

    private static int MonthsFor(TenantSettings settings, string retentionClass) => RetentionClasses.MonthsFor(settings.RetentionMonths, retentionClass);

    private async Task<(int Deleted, int Files, List<string> Failures)> DeleteExpiredDocumentsAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> held, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var files = 0;
        var failures = new List<string>();
        while (true)
        {
            var batch = await store.DocumentsBeforeAsync(scope, cutoff, held, DocumentBatch, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            var done = new List<Guid>();
            foreach (var document in batch)
            {
                try
                {
                    files += await documentStorage.DeleteDocumentObjectsAsync(scope, document.CaseId, document.DocumentId, document.ObjectKey, cancellationToken);
                    done.Add(document.DocumentId);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add($"{ClaimReference.For(document.CaseId)}: {Describe(exception)}");
                }
            }

            if (done.Count == 0)
            {
                break;
            }

            deleted += await store.DeleteDocumentRowsAsync(scope, done, cancellationToken);
            if (batch.Count < DocumentBatch)
            {
                break;
            }
        }

        return (deleted, files, failures.Distinct().ToList());
    }

    private async Task RecordAsync(
        TenantScope scope, string actor, string retentionClass, int months, IReadOnlyDictionary<string, int> counts, int files,
        IReadOnlyList<string> held, IReadOnlyList<string> failures, string correlationId, List<DeletionRecord> records, CancellationToken cancellationToken)
    {
        var nonZero = counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (nonZero.Count == 0 && failures.Count == 0)
        {
            return;
        }

        var record = new DeletionRecord
        {
            DeletionId = Guid.NewGuid(),
            Scope = scope,
            Kind = DeletionKinds.Retention,
            Subject = retentionClass,
            Reason = $"{RetentionClasses.Label(retentionClass)} older than {months} month(s), as set in the operating settings.",
            Actor = actor,
            At = Now,
            Counts = nonZero,
            StoredFilesDeleted = files,
            HeldClaims = held,
            Failures = failures,
        };
        await store.SaveDeletionAsync(record, cancellationToken);
        await Audit(scope, actor, "data.retention.deleted", $"{record.Total} record(s) of '{RetentionClasses.Label(retentionClass)}' were deleted after {months} month(s) ({files} stored files). Reference {record.DeletionId}.", correlationId, null, cancellationToken);
        records.Add(record);
    }

    public Task<IReadOnlyList<DeletionRecord>> ListDeletionsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        store.ListDeletionsAsync(scope, 200, cancellationToken);

    // ------------------------------------------------------------------ helpers

    /// <summary>Deletes a claim's stored files (and its stored packs) and returns how many. Throws if the file store cannot, so the claim's records stay as the list of what is left.</summary>
    private async Task<int> DeleteStoredFilesAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken)
    {
        var footprint = await store.FootprintAsync(scope, caseId, cancellationToken);
        var files = 0;
        if (footprint.Documents > 0 || footprint.Counts.GetValueOrDefault(DataCategories.AiRecords) > 0)
        {
            files += await documentStorage.DeleteCaseObjectsAsync(scope, caseId, cancellationToken);
        }

        foreach (var key in footprint.PackObjectKeys)
        {
            files += await packStorage.DeleteObjectAsync(scope, key, cancellationToken);
        }

        return files;
    }

    private static void Add(Dictionary<string, int> into, IReadOnlyDictionary<string, int> from)
    {
        foreach (var (key, value) in from)
        {
            into[key] = into.GetValueOrDefault(key) + value;
        }
    }

    private static string Describe(Exception exception) => exception is NotSupportedException or InvalidOperationException
        ? "stored files could not be deleted because the file store is not available to this deployment"
        : "it could not be completed and will be tried again";

    private Task Audit(TenantScope scope, string actor, string action, string reason, string correlationId, Guid? caseId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, caseId, null, actor, ActorType.User, action, Now,
                BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: null, Confidence: null,
                ValidationResult: "accepted", ApprovalId: null, AuditReason: reason, CorrelationId: correlationId),
            cancellationToken);
}

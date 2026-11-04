using System.Text.Json;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Jobs;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Compliance;

/// <summary>The public address of the customer portal, where a shared pack link points. Null when none is configured.</summary>
public interface IPortalBaseUrl
{
    string? BaseUrl { get; }

    /// <summary>The address for this workspace's customers: its own web address when it has a verified one (build-loop item 29).</summary>
    string? BaseUrlFor(TenantScope scope) => BaseUrl;
}

public sealed record ClaimsPackSendResult(string Outcome, string Mode, int Recipients, string? Message, DateTimeOffset? LinkExpiresAt);

public interface IClaimsPackEmailService
{
    /// <summary>Emails the claim's pack to the workspace's recipients. An automatic reason is sent once per claim; a
    /// manual one ("manual:" and a fresh id) every time it is asked for.</summary>
    Task<ClaimsPackSendResult> SendAsync(TenantScope scope, Guid caseId, string reasonKey, string reason, string actorId, string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimsPackDispatch>> HistoryAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Emails a claims pack to the people an insurer has named (build-loop item 25), for example a reinsurer or an
/// assessor. A small pack is attached; a larger one is stored and sent as a secure link that expires after a week
/// and a few downloads, because large attachments are often refused and a link can be withdrawn. Every send is
/// recorded with who it went to (masked) and how it ended, and an automatic reason is never sent twice for a claim.
/// </summary>
public sealed class ClaimsPackEmailService(
    ITenantSettingsRepository settingsRepository,
    IClaimsPackBuilder packBuilder,
    ICommunicationSendingProvider emailSender,
    IEvidencePackStorageProvider packStorage,
    IClaimsPackShareLinkRepository shareLinks,
    IClaimsPackDispatchRepository dispatches,
    IPortalBaseUrl portalBaseUrl,
    IAuditRepository auditRepository) : IClaimsPackEmailService
{
    public async Task<ClaimsPackSendResult> SendAsync(TenantScope scope, Guid caseId, string reasonKey, string reason, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        var recipients = ClaimsPackDeliveryRules.Normalise(settings.ClaimsPackRecipients);
        if (recipients.Count == 0)
        {
            return new ClaimsPackSendResult("NoRecipients", "NotSent", 0, "No email address has been set to receive claims packs. Add one in Admin, Claims pack email.", null);
        }

        var automatic = !reasonKey.StartsWith("manual:", StringComparison.Ordinal);
        if (automatic)
        {
            var earlier = await dispatches.ListForCaseAsync(scope, caseId, cancellationToken);
            if (earlier.Any(item => item.ReasonKey == reasonKey && item.Outcome == "Sent"))
            {
                return new ClaimsPackSendResult("AlreadySent", "NotSent", 0, "This pack was already sent for that reason.", null);
            }
        }

        var pack = await packBuilder.BuildAsync(scope, caseId, actorId, cancellationToken);
        if (pack is null)
        {
            return new ClaimsPackSendResult("NotFound", "NotSent", 0, "Claim was not found.", null);
        }

        var reference = pack.Manifest.ClaimReference;
        var attach = pack.Content.Length <= ClaimsPackDeliveryRules.MaxAttachmentBytes;
        string? link = null;
        DateTimeOffset? expires = null;
        if (!attach)
        {
            var baseUrl = portalBaseUrl.BaseUrlFor(scope);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return await RecordAsync(scope, caseId, reasonKey, reason, actorId, recipients, "NotSent", "ConfigurationRequired", pack, null, correlationId, "The pack is too large to attach, and the customer portal address that a download link needs is not configured.", cancellationToken);
            }

            var stored = await packStorage.StoreAsync(new EvidencePackStorageRequest(scope, caseId, pack.FileName, "application/zip", new MemoryStream(pack.Content)), cancellationToken);
            var rawToken = ClaimantTokenGenerator.GenerateRawToken();
            var now = DateTimeOffset.UtcNow;
            expires = now.Add(ClaimsPackDeliveryRules.LinkLifetime);
            await shareLinks.SaveAsync(new ClaimsPackShareLink
            {
                LinkId = Guid.NewGuid(),
                Scope = scope,
                CaseId = caseId,
                TokenHash = ClaimantTokenGenerator.Hash(rawToken),
                ObjectKey = stored.ObjectKey,
                FileName = pack.FileName,
                SizeBytes = pack.Content.Length,
                CreatedAt = now,
                ExpiresAt = expires.Value,
            }, cancellationToken);
            link = $"{baseUrl.TrimEnd('/')}/shared/claims-pack?token={Uri.EscapeDataString(rawToken)}";
        }

        var failures = new List<string>();
        foreach (var recipient in recipients)
        {
            var body = ComposeBody(reference, reason, link, expires, pack.Manifest.Files.Count);
            var result = await emailSender.SendEmailAsync(
                new CommunicationSendRequest(
                    scope, caseId, CommunicationChannel.Email, recipient, body, actorId, Approved: true, LowRisk: false, correlationId,
                    Subject: $"Claims pack {reference}",
                    Attachments: attach ? [new OutboundAttachment(pack.FileName, "application/zip", pack.Content)] : null),
                cancellationToken);
            if (CustomerUpdateDelivery.IsFailure(result.DeliveryStatus) || result.DeliveryStatus is not ("Sent" or "SimulatedSent"))
            {
                failures.Add(result.DeliveryStatus);
            }
        }

        var mode = attach ? "Attached" : "Link";
        var outcome = failures.Count == 0 ? "Sent" : failures.Count == recipients.Count ? $"NotSent:{failures[0]}" : "PartlySent";
        var message = outcome == "Sent" ? null : "The email could not be sent to every address. Check the mail connection and try again.";
        return await RecordAsync(scope, caseId, reasonKey, reason, actorId, recipients, mode, outcome, pack, expires, correlationId, message, cancellationToken);
    }

    public Task<IReadOnlyList<ClaimsPackDispatch>> HistoryAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default) =>
        dispatches.ListForCaseAsync(scope, caseId, cancellationToken);

    private async Task<ClaimsPackSendResult> RecordAsync(
        TenantScope scope, Guid caseId, string reasonKey, string reason, string actorId, IReadOnlyList<string> recipients,
        string mode, string outcome, ClaimsPack pack, DateTimeOffset? expires, string correlationId, string? message, CancellationToken cancellationToken)
    {
        await dispatches.SaveAsync(new ClaimsPackDispatch
        {
            DispatchId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ReasonKey = reasonKey,
            Reason = reason,
            SentBy = actorId,
            At = DateTimeOffset.UtcNow,
            RecipientsMasked = recipients.Select(ClaimsPackDeliveryRules.MaskAddress).ToArray(),
            Mode = mode,
            Outcome = outcome,
            FileCount = pack.Manifest.Files.Count,
            PackBytes = pack.Content.Length,
            LinkExpiresAt = expires,
        }, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimsPackEmailed(scope, caseId, actorId, mode, recipients.Count, outcome, correlationId), cancellationToken);
        return new ClaimsPackSendResult(outcome == "Sent" ? "Sent" : outcome.StartsWith("NotSent", StringComparison.Ordinal) ? "NotSent" : outcome, mode, recipients.Count, message, expires);
    }

    private static string ComposeBody(string reference, string reason, string? link, DateTimeOffset? expires, int files)
    {
        var lines = new List<string>
        {
            $"Claims pack for claim {reference}.",
            $"Why you are receiving it: {reason}.",
            $"It holds {files} files: a summary of the claim, the customer's documents, the evidence as received and a list of file fingerprints (SHA-256).",
            "Parts of the summary prepared by the platform are marked advisory and decide nothing.",
            "It contains personal information. Keep it secure and share it only with people who need it.",
        };
        if (link is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"The pack is too large to attach. Download it here: {link}");
            lines.Add($"The link works until {expires:d MMMM yyyy} and for at most {ClaimsPackDeliveryRules.MaxLinkDownloads} downloads.");
        }
        else
        {
            lines.Add("The pack is attached to this email.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public interface IClaimsPackTrigger
{
    /// <summary>A claim's documents may be complete.</summary>
    Task OnCompletenessAsync(ClaimCase claim, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>A claim's status may just have changed.</summary>
    Task OnStatusChangedAsync(ClaimCase before, ClaimCase after, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>Starts the emailing of a pack, as a durable background job, when the workspace's chosen trigger happens (build-loop item 25).</summary>
public sealed class ClaimsPackTrigger(ITenantSettingsRepository settingsRepository, IBackgroundJobStore jobs) : IClaimsPackTrigger
{
    public async Task OnCompletenessAsync(ClaimCase claim, string correlationId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(claim.Scope, cancellationToken);
        if (ClaimsPackDeliveryRules.TryParseTrigger(settings.ClaimsPackTrigger, out var trigger)
            && ClaimsPackDeliveryRules.CompletenessTriggers(trigger, claim.CompletenessScore)
            && (settings.ClaimsPackRecipients?.Count ?? 0) > 0)
        {
            await EnqueueAsync(claim, "complete", "the documents for the claim are complete", correlationId, cancellationToken);
        }
    }

    public async Task OnStatusChangedAsync(ClaimCase before, ClaimCase after, string correlationId, CancellationToken cancellationToken = default)
    {
        if (before.Status == after.Status)
        {
            return;
        }

        var settings = await settingsRepository.GetAsync(after.Scope, cancellationToken);
        if (ClaimsPackDeliveryRules.TryParseTrigger(settings.ClaimsPackTrigger, out var trigger)
            && ClaimsPackDeliveryRules.StatusTriggers(trigger, settings.ClaimsPackTriggerStatus, before.Status, after.Status)
            && (settings.ClaimsPackRecipients?.Count ?? 0) > 0)
        {
            await EnqueueAsync(after, $"status:{after.Status}", $"the claim reached the status {Application.CommandCenter.ClaimStatusLabels.For(after.Status).ToLowerInvariant()}", correlationId, cancellationToken);
        }
    }

    private Task EnqueueAsync(ClaimCase claim, string reasonKey, string reason, string correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return jobs.EnqueueAsync(
            new BackgroundJob
            {
                JobId = Guid.NewGuid(),
                Scope = claim.Scope,
                Queue = BackgroundQueues.ClaimReview,
                Type = BackgroundJobTypes.ClaimsPackSend,
                Key = $"{claim.CaseId:N}:{reasonKey}",
                PayloadJson = JsonSerializer.Serialize(new { caseId = claim.CaseId, reasonKey, reason }),
                Status = BackgroundJobStatus.Pending,
                MaxAttempts = 3,
                NotBefore = now,
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = correlationId,
                CaseId = claim.CaseId,
            },
            coalesceWithRunning: true,
            cancellationToken: cancellationToken);
    }
}

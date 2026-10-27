using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Application.Knowledge;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Connectors;

public interface IConnectorSyncService
{
    /// <summary>One pass over a connector's folders. Never throws for a provider problem: the outcome is the answer.</summary>
    Task<ConnectorRun> RunAsync(DataConnector connector, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>Takes every file that came from the given folders out of use at once (a folder was removed or the connector deleted).</summary>
    Task<int> RetireFoldersAsync(DataConnector connector, IReadOnlyCollection<string> folderIds, string reason, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps the Data Hub in step with the folders an administrator chose. The rules that make it safe to leave running:
/// <list type="bullet">
/// <item>Reads use the owner's own sign-in, so it can never see more than the owner can open.</item>
/// <item>Everything arrives as a pending Draft with unknown sensitivity. A changed file is a new pending document: an approval
/// never carries over to different content.</item>
/// <item>A sign-in that expired or was revoked fails the run and removes nothing.</item>
/// <item>A folder that cannot be read, or that suddenly lists as empty, removes nothing from that folder. Only a provider's
/// positive statement that a file is gone, or a person removing the folder, takes files out of use.</item>
/// <item>A file that cannot be read this time is tried again next time and the rest still sync.</item>
/// </list>
/// </summary>
public sealed class ConnectorSyncService(
    IFileAccessTokenProvider tokens,
    IEnumerable<IConnectorFileSource> sources,
    IConnectorItemRepository items,
    IConnectorRunRepository runs,
    IDataConnectorRepository connectors,
    IKnowledgeIngestionSink knowledge,
    IAuditRepository audit,
    TimeProvider clock,
    InsuranceIntelligence.Application.BankEvents.IBankEventFileSink? eventSink = null) : IConnectorSyncService
{
    private const string Actor = "connector-sync";

    public async Task<ConnectorRun> RunAsync(DataConnector connector, string correlationId, CancellationToken cancellationToken = default)
    {
        var scope = connector.Scope;
        var run = new ConnectorRun { RunId = Guid.NewGuid(), ConnectorId = connector.ConnectorId, Scope = scope, StartedAt = clock.GetUtcNow() };
        var source = sources.FirstOrDefault(s => s.Provider == connector.Provider);
        if (source is null)
        {
            return await FinishAsync(connector, run, ConnectorRunOutcome.Failed, ConnectorStatus.Error, "provider_error", "This kind of connection cannot be synced yet.", correlationId, cancellationToken);
        }

        string token;
        try
        {
            token = await tokens.GetAccessTokenAsync(scope, connector.OwnerUserId, connector.Provider, cancellationToken);
        }
        catch (FileConnectionException failure) when (failure.Code is "sign_in_expired" or "not_connected")
        {
            return await FinishAsync(connector, run, ConnectorRunOutcome.SignInExpired, ConnectorStatus.NeedsReconnect, failure.Code,
                "The sign-in this connector reads with has expired or was removed. Nothing was changed. Ask the person who set it up to connect their account again.", correlationId, cancellationToken);
        }
        catch (FileConnectionException failure)
        {
            return await FinishAsync(connector, run, ConnectorRunOutcome.Failed, ConnectorStatus.Error, failure.Code, "The provider could not be reached. It will be tried again.", correlationId, cancellationToken);
        }

        var known = (await items.ListAsync(scope, connector.ConnectorId, cancellationToken)).ToDictionary(item => item.ExternalId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var removable = new HashSet<string>(StringComparer.Ordinal);
        int found = 0, added = 0, updated = 0, removed = 0, skipped = 0, failed = 0;
        string? firstProblem = null;

        foreach (var folder in connector.Folders)
        {
            ConnectorListing listing;
            try
            {
                listing = await source.ListAsync(token, folder, cancellationToken);
            }
            catch (ConnectorSourceException problem) when (problem.Code == "sign_in_expired")
            {
                // Abort the whole run before anything is removed: this must never look like "every file was deleted".
                return await FinishAsync(connector, run with { Found = found, Added = added, Updated = updated, Skipped = skipped, Failed = failed },
                    ConnectorRunOutcome.SignInExpired, ConnectorStatus.NeedsReconnect, problem.Code, problem.Message, correlationId, cancellationToken);
            }
            catch (ConnectorSourceException problem)
            {
                failed++;
                firstProblem ??= $"{folder.DisplayName}: {problem.Message}";
                continue;
            }

            var inFolder = known.Values.Where(item => item.FolderId == folder.FolderId && item.State == ConnectorItemState.Pending).ToArray();
            if (listing.Files.Count == 0 && inFolder.Length > 0 && !listing.EmptyIsAuthoritative)
            {
                failed++;
                firstProblem ??= $"{folder.DisplayName}: the folder listed as empty, so nothing was removed. If it was emptied on purpose, remove the folder from the connector.";
                foreach (var item in inFolder) seen.Add(item.ExternalId);
                continue;
            }

            removable.Add(folder.FolderId);
            foreach (var file in listing.Files)
            {
                found++;
                seen.Add(file.ExternalId);
                known.TryGetValue(file.ExternalId, out var existing);

                if (folder.Purpose == ConnectorFolderPurpose.BankEvents)
                {
                    var notEvents = !string.Equals(Path.GetExtension(file.Name), ".csv", StringComparison.OrdinalIgnoreCase)
                        ? "Only CSV files are read as bank events."
                        : file.Size <= 0 || file.Size > InsuranceIntelligence.Application.BankEvents.EventService.MaxImportBytes
                            ? "A file of events must be between 1 byte and 5 MB."
                            : eventSink is null ? "Reading bank events from a folder is not available here." : null;
                    if (notEvents is not null)
                    {
                        if (existing is null || existing.State != ConnectorItemState.Skipped || existing.SkipReason != notEvents)
                        {
                            await items.SaveAsync(NewItem(connector, file, null, ConnectorItemState.Skipped, notEvents), cancellationToken);
                        }

                        skipped++;
                        continue;
                    }

                    if (existing is { State: ConnectorItemState.Imported } && existing.Version == file.Version && existing.ModifiedAt == file.ModifiedAt)
                    {
                        continue;
                    }

                    byte[] content;
                    try
                    {
                        content = await source.DownloadAsync(token, file, cancellationToken);
                    }
                    catch (ConnectorSourceException problem) when (problem.Code == "sign_in_expired")
                    {
                        return await FinishAsync(connector, run with { Found = found, Added = added, Updated = updated, Skipped = skipped, Failed = failed },
                            ConnectorRunOutcome.SignInExpired, ConnectorStatus.NeedsReconnect, problem.Code, problem.Message, correlationId, cancellationToken);
                    }
                    catch (ConnectorSourceException problem)
                    {
                        failed++;
                        firstProblem ??= $"{file.Name}: {problem.Message}";
                        await items.SaveAsync(NewItem(connector, file, null, ConnectorItemState.Failed, problem.Message), cancellationToken);
                        continue;
                    }

                    try
                    {
                        var imported = await eventSink!.ImportAsync(scope, file.Name, content, connector.OwnerUserId, cancellationToken);
                        var summary = $"{imported.Created} new events, {imported.Duplicates} already known, {imported.Rejected.Count} refused.";
                        await items.SaveAsync(NewItem(connector, file, null, ConnectorItemState.Imported, summary), cancellationToken);
                        if (existing is null) added++; else updated++;
                    }
                    catch (InsuranceIntelligence.Application.BankEvents.EventException problem)
                    {
                        failed++;
                        firstProblem ??= $"{file.Name}: {problem.Message}";
                        await items.SaveAsync(NewItem(connector, file, null, ConnectorItemState.Failed, problem.Message), cancellationToken);
                    }

                    continue;
                }

                if (KnowledgeIngestionLimits.WhyNot(file.Name, file.Size) is { } why)
                {
                    if (existing is null || existing.State != ConnectorItemState.Skipped || existing.SkipReason != why)
                    {
                        await items.SaveAsync(NewItem(connector, file, null, ConnectorItemState.Skipped, why), cancellationToken);
                    }

                    skipped++;
                    continue;
                }

                if (existing is { State: ConnectorItemState.Pending } && existing.Version == file.Version && existing.ModifiedAt == file.ModifiedAt)
                {
                    continue;
                }

                byte[] bytes;
                try
                {
                    bytes = await source.DownloadAsync(token, file, cancellationToken);
                }
                catch (ConnectorSourceException problem) when (problem.Code == "sign_in_expired")
                {
                    return await FinishAsync(connector, run with { Found = found, Added = added, Updated = updated, Skipped = skipped, Failed = failed },
                        ConnectorRunOutcome.SignInExpired, ConnectorStatus.NeedsReconnect, problem.Code, problem.Message, correlationId, cancellationToken);
                }
                catch (ConnectorSourceException problem)
                {
                    failed++;
                    firstProblem ??= $"{file.Name}: {problem.Message}";
                    await items.SaveAsync(NewItem(connector, file, existing?.KnowledgeDocumentId, ConnectorItemState.Failed, problem.Message), cancellationToken);
                    continue;
                }

                await using var stream = new MemoryStream(bytes);
                var document = await knowledge.IngestPendingAsync(
                    new KnowledgeIngestionRequest(
                        scope, connector.OwnerUserId, Path.GetFileNameWithoutExtension(file.Name), folder.ProductLine, folder.DocumentType,
                        file.Version ?? file.ModifiedAt?.ToString("yyyy-MM-dd") ?? "synced", null, null, file.Name,
                        file.MimeType ?? "application/octet-stream", bytes.Length, stream, correlationId),
                    cancellationToken);

                if (existing?.KnowledgeDocumentId is { } previous)
                {
                    await knowledge.RetireAsync(scope, previous, Actor, "The file changed in the connected folder, so the earlier copy was replaced by a new one that needs approving.", correlationId, cancellationToken);
                    updated++;
                }
                else
                {
                    added++;
                }

                await items.SaveAsync(NewItem(connector, file, document.KnowledgeDocumentId, ConnectorItemState.Pending, null), cancellationToken);
            }
        }

        // Only files from a folder that was read properly this time, and not seen there, are gone.
        foreach (var item in known.Values.Where(i => i.State == ConnectorItemState.Pending && !seen.Contains(i.ExternalId) && removable.Contains(i.FolderId)))
        {
            if (item.KnowledgeDocumentId is { } documentId)
            {
                await knowledge.RetireAsync(scope, documentId, Actor, "The file is no longer in the connected folder.", correlationId, cancellationToken);
            }

            await items.SaveAsync(item with { State = ConnectorItemState.Removed, LastSeenAt = clock.GetUtcNow() }, cancellationToken);
            removed++;
        }

        var outcome = failed > 0 ? ConnectorRunOutcome.Partial : ConnectorRunOutcome.Succeeded;
        return await FinishAsync(connector,
            run with { Found = found, Added = added, Updated = updated, Removed = removed, Skipped = skipped, Failed = failed },
            outcome, ConnectorStatus.Active, failed > 0 ? "partial" : null, firstProblem, correlationId, cancellationToken);
    }

    public async Task<int> RetireFoldersAsync(DataConnector connector, IReadOnlyCollection<string> folderIds, string reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var retired = 0;
        var all = await items.ListAsync(connector.Scope, connector.ConnectorId, cancellationToken);
        foreach (var item in all.Where(i => folderIds.Contains(i.FolderId) && i.State != ConnectorItemState.Removed))
        {
            if (item.KnowledgeDocumentId is { } documentId)
            {
                await knowledge.RetireAsync(connector.Scope, documentId, Actor, reason, correlationId, cancellationToken);
            }

            await items.SaveAsync(item with { State = ConnectorItemState.Removed, LastSeenAt = clock.GetUtcNow() }, cancellationToken);
            retired++;
        }

        return retired;
    }

    private ConnectorItem NewItem(DataConnector connector, RemoteFile file, Guid? documentId, ConnectorItemState state, string? note) => new()
    {
        ItemId = ConnectorItem.StorageKey(connector.ConnectorId, file.ExternalId),
        ConnectorId = connector.ConnectorId,
        Scope = connector.Scope,
        ExternalId = file.ExternalId,
        FolderId = file.FolderId,
        FileName = file.Name,
        ModifiedAt = file.ModifiedAt,
        Version = file.Version,
        KnowledgeDocumentId = documentId,
        State = state,
        SkipReason = note,
        LastSeenAt = clock.GetUtcNow(),
    };

    private async Task<ConnectorRun> FinishAsync(
        DataConnector connector, ConnectorRun run, ConnectorRunOutcome outcome, ConnectorStatus status, string? errorCode, string? safeError,
        string correlationId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var finished = run with { FinishedAt = now, Outcome = outcome, ErrorCode = errorCode, SafeError = safeError };
        await runs.SaveAsync(finished, cancellationToken);
        // Reload before saving: a person may have changed the folders, paused it or deleted it while this ran, and a run must
        // only record its own result, never put back an older copy of what they changed.
        var latest = await connectors.GetAsync(connector.Scope, connector.ConnectorId, cancellationToken);
        if (latest is not null)
        {
            // A paused connector stays paused; a run it was given by hand does not switch it back on.
            await connectors.SaveAsync(latest with { Status = latest.Status == ConnectorStatus.Paused ? ConnectorStatus.Paused : status, LastRunAt = now, LastRunOutcome = outcome }, cancellationToken);
        }

        await audit.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), connector.Scope, null, null, Actor, ActorType.System, "connector.synced", now, null, null, [], "Connectors", null,
                outcome.ToString().ToLowerInvariant(), null,
                $"{connector.Provider} connector: {finished.Found} found, {finished.Added} added, {finished.Updated} changed, {finished.Removed} removed, {finished.Skipped} skipped, {finished.Failed} failed.",
                correlationId),
            cancellationToken);
        return finished;
    }
}

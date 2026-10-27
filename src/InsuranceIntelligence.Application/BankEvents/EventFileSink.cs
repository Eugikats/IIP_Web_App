using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.BankEvents;

/// <summary>Where a connected folder hands a file of bank events. The folder's own sign-in reads the file; this reads its rows.</summary>
public interface IBankEventFileSink
{
    Task<EventImportResult> ImportAsync(TenantScope scope, string fileName, byte[] content, string actorId, CancellationToken cancellationToken = default);
}

public sealed class EventFileSink(EventService events) : IBankEventFileSink
{
    public Task<EventImportResult> ImportAsync(TenantScope scope, string fileName, byte[] content, string actorId, CancellationToken cancellationToken = default) =>
        events.ImportCsvAsync(scope, fileName, Encoding.UTF8.GetString(content), actorId, cancellationToken);
}

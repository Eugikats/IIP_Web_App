using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Retries a database operation that failed while connecting, instead of failing the request.
///
/// Right after the API starts, the first connections to Azure SQL can fail during login (the
/// TCP handshake is established and then "an internal exception was caught") even though the database
/// is idle and healthy. With no retry that surfaced as an HTTP 500 on the first sign-in, which the
/// portal shows as "Sign-in could not be completed", and on the first WhatsApp message it would
/// escalate a customer to a person for nothing. The second attempt succeeds, so the request now makes it.
///
/// Safe here because nothing in this codebase opens an explicit transaction: every write is a single
/// SaveChanges, which EF retries as a unit.
/// </summary>
public static class SqlServerResilience
{
    public const int MaxRetryCount = 5;
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    /// <summary>The login-time errors seen in production logs, added to EF's own list of transient
    /// errors: 31 and 35 (handshake and internal login exception), 64 (connection dropped), 233
    /// (no process on the other end of the pipe), 10053, 10054 and 10060 (socket reset and timeout).</summary>
    public static readonly int[] AdditionalTransientErrors = [31, 35, 64, 233, 10053, 10054, 10060];

    public static void Configure(SqlServerDbContextOptionsBuilder sql) =>
        sql.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, AdditionalTransientErrors);
}

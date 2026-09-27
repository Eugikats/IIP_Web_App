using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>The database connection retries a failed login instead of failing the request. Without
/// it the first sign-in after a restart returned a 500 while the database sat idle and healthy.</summary>
public sealed class SqlServerResilienceTests
{
    private static InsuranceIntelligenceDbContext Context() =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseSqlServer("Server=tcp:localhost,1;Database=x;User Id=u;Password=p;Encrypt=True;TrustServerCertificate=True", SqlServerResilience.Configure)
            .Options);

    [Fact]
    public void TheContextRetriesOnFailure()
    {
        using var db = Context();

        var strategy = db.Database.CreateExecutionStrategy();

        Assert.True(strategy.RetriesOnFailure);
    }

    [Fact]
    public void TheLoginErrorsSeenInProductionAreTreatedAsTransient()
    {
        Assert.Contains(35, SqlServerResilience.AdditionalTransientErrors);
        Assert.Contains(31, SqlServerResilience.AdditionalTransientErrors);
        Assert.Contains(10054, SqlServerResilience.AdditionalTransientErrors);
    }

    [Fact]
    public void RetriesAreBoundedSoAnOutageStillFailsInsteadOfHangingForever()
    {
        Assert.InRange(SqlServerResilience.MaxRetryCount, 1, 10);
        Assert.True(SqlServerResilience.MaxRetryDelay <= TimeSpan.FromSeconds(30));
    }
}

using InsuranceIntelligence.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// In-memory storage exists so the team can run the platform with no Azure account. It must
/// never be reachable anywhere that could hold real data. These tests pin the deny-by-default
/// behaviour: only explicitly-local runtime profiles may use it, and anything unrecognised is
/// refused rather than allowed.
/// </summary>
public sealed class OfflineModeStorageGuardTests
{
    private static IServiceCollection Build(string runtimeProfile, string storageProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_RUNTIME_PROFILE"] = runtimeProfile,
                ["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] = storageProvider,
                ["INSURANCEINTELLIGENCE_AUTH_MODE"] = "Development",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInsuranceIntelligenceInfrastructure(configuration, new StubHostEnvironment(runtimeProfile));
        return services;
    }

    [Theory]
    [InlineData("LocalTest")]
    [InlineData("Development")]
    [InlineData("Test")]
    public void InMemoryStorage_IsAllowed_ForLocalDeveloperProfiles(string profile)
    {
        var exception = Record.Exception(() => Build(profile, "InMemory"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Demo")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("ManagedApplicationReviewer")]
    // An unrecognised profile must fail closed, not open, a typo or a newly-introduced
    // profile name can never silently unlock non-durable storage.
    [InlineData("Prod")]
    [InlineData("staging-2")]
    [InlineData("")]
    public void InMemoryStorage_IsRefused_ForEveryNonLocalProfile(string profile)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build(profile, "InMemory"));
        Assert.Contains("Unsupported storage provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqliteStorage_RemainsRefused_Everywhere()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build("LocalTest", "EfSqlite"));
        Assert.Contains("SQLite storage is not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlServerStorage_StillRequiresAConnectionString()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build("Production", "SqlServer"));
        Assert.Contains("SqlServer storage requires", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

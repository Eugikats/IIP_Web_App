namespace InsuranceIntelligence.Infrastructure.Configuration;

public static class ProviderModeSupport
{
    public static bool IsNotConfiguredMode(string mode) =>
        mode.Equals("NotConfigured", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    public static bool IsMockMode(string mode) =>
        mode.Equals("Mock", StringComparison.OrdinalIgnoreCase);

    public static bool IsForbiddenProtectedMode(string mode, bool includeSqlite = false) =>
        IsMockMode(mode)
        || mode.Equals("Simulated", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("Placeholder", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("DevelopmentAuth", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("Demo", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("SeededDemoBusinessData", StringComparison.OrdinalIgnoreCase)
        || mode.Equals("FallbackToMock", StringComparison.OrdinalIgnoreCase)
        || (includeSqlite && (mode.Equals("SQLite", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("EfSqlite", StringComparison.OrdinalIgnoreCase)));

    public static bool IsLiveProviderMode(string mode, string liveMode) =>
        !IsNotConfiguredMode(mode)
        && !IsMockMode(mode)
        && mode.Equals(liveMode, StringComparison.OrdinalIgnoreCase);

    public static bool IsProtectedRuntime(string runtimeProfile) =>
        runtimeProfile.Equals("Staging", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("Production", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase);

    public static string ResolveRuntimeProfile(InfrastructureOptions options, string environmentName)
    {
        if (!string.IsNullOrWhiteSpace(options.RuntimeProfile))
        {
            return options.RuntimeProfile;
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return "Development";
        }

        if (environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("LocalTest", StringComparison.OrdinalIgnoreCase))
        {
            return environmentName;
        }

        return environmentName.Equals("Test", StringComparison.OrdinalIgnoreCase)
            ? "LocalTest"
            : environmentName;
    }
}

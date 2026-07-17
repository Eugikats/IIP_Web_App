using System.Runtime.CompilerServices;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ObservabilityWiringTests
{
    [Fact]
    public void ApiHost_RegistersApplicationInsightsTelemetry()
    {
        var program = ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Api", "Program.cs"));
        var project = ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Api", "InsuranceIntelligence.Api.csproj"));

        Assert.Contains("AddApplicationInsightsTelemetry()", program, StringComparison.Ordinal);
        Assert.Contains("ApplicationInsightsLoggerProvider", program, StringComparison.Ordinal);
        Assert.Contains("Microsoft.ApplicationInsights.AspNetCore", project, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionsHost_RegistersDirectApplicationInsightsTelemetry()
    {
        var program = ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Functions", "Program.cs"));
        var project = ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Functions", "InsuranceIntelligence.Functions.csproj"));

        Assert.Contains("AddApplicationInsightsTelemetryWorkerService()", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureFunctionsApplicationInsights()", program, StringComparison.Ordinal);
        Assert.Contains("ApplicationInsightsLoggerProvider", program, StringComparison.Ordinal);
        Assert.Contains("Microsoft.ApplicationInsights.WorkerService", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Azure.Functions.Worker.ApplicationInsights", project, StringComparison.Ordinal);
    }

    [Fact]
    public void FoundationIaC_WiresWorkspaceBasedTelemetryAndOperationalAlerts()
    {
        var bicep = ReadRepositoryFile(Path.Combine("infra", "bicep", "phase3-dev-foundation.bicep"));

        Assert.Contains("Microsoft.OperationalInsights/workspaces", bicep, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Insights/components", bicep, StringComparison.Ordinal);
        Assert.Contains("WorkspaceResourceId: logAnalytics.id", bicep, StringComparison.Ordinal);
        Assert.Contains("APPLICATIONINSIGHTS_CONNECTION_STRING", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("APPINSIGHTS_INSTRUMENTATIONKEY", bicep, StringComparison.Ordinal);
        Assert.Contains("apiHigh5xxAlert", bicep, StringComparison.Ordinal);
        Assert.Contains("functionHigh5xxAlert", bicep, StringComparison.Ordinal);
        Assert.Contains("serviceBusDeadLetterAlert", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void WebHostingIaC_EnablesHttp2AndKeepsProtectedRuntimesWarm()
    {
        var mainTemplate = ReadRepositoryFile(Path.Combine("infra", "mainTemplate.bicep"));
        var devFoundation = ReadRepositoryFile(Path.Combine("infra", "bicep", "phase3-dev-foundation.bicep"));

        Assert.Equal(2, mainTemplate.Split("http20Enabled: true", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, devFoundation.Split("http20Enabled: true", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, mainTemplate.Split("alwaysOn: deploymentSize != 'dev'", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void DeployedApi_UsesSafeExceptionResponsesAndDisablesLocalDiagnostics()
    {
        var program = ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Api", "Program.cs"));
        var mainTemplate = ReadRepositoryFile(Path.Combine("infra", "mainTemplate.bicep"));
        var devFoundation = ReadRepositoryFile(Path.Combine("infra", "bicep", "phase3-dev-foundation.bicep"));

        Assert.Contains("builder.Services.AddProblemDetails()", program, StringComparison.Ordinal);
        Assert.Contains("app.UseExceptionHandler()", program, StringComparison.Ordinal);
        Assert.Contains("builder.Environment.IsDevelopment()", program, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "environmentName == 'dev' ? 'Development' : 'Production'",
            mainTemplate,
            StringComparison.Ordinal);
        Assert.Contains("name: 'ASPNETCORE_ENVIRONMENT'", devFoundation, StringComparison.Ordinal);
        Assert.Contains("value: 'Production'", devFoundation, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
    }
}

using System.Runtime.CompilerServices;

namespace InsuranceIntelligence.Api.Tests;

// Source-level guardrails for the W2.5 wiring: the Phase 2 read routes must be mapped with the
// right permission filter, and the query services + read-repository adapters must be registered
// in DI (so the endpoints resolve at runtime rather than throwing on first request).
public sealed class Phase2RouteWiringTests
{
    [Theory]
    [InlineData("app.MapGet(\"/api/v1/channels/overview\", ChannelEndpoints.GetOverview)", "Permission.ViewChannels")]
    [InlineData("app.MapGet(\"/api/v1/customers/{customerId}\", CustomerProfileEndpoints.GetCustomerProfile)", "Permission.ViewCustomers")]
    [InlineData("app.MapGet(\"/api/v1/sales-leads/{leadId}\", SalesLeadEndpoints.GetLeadDetail)", "Permission.ViewSalesLeads")]
    [InlineData("app.MapGet(\"/api/v1/audit/events\", AuditEndpoints.ListEvents)", "Permission.ViewAuditLog")]
    public void Phase2Routes_AreMappedWithPermissionFilters(string route, string permissionExpression)
    {
        var program = ReadProgramSource();
        var block = RouteBlock(program, route);

        Assert.Contains("PermissionEndpointFilter", block);
        Assert.Contains(permissionExpression, block);
    }

    [Theory]
    [InlineData("builder.Services.AddScoped<IChannelQueryService, ChannelQueryService>();")]
    [InlineData("builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();")]
    [InlineData("builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();")]
    [InlineData("builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();")]
    public void Phase2QueryServices_AreRegistered(string registration)
    {
        Assert.Contains(registration, ReadProgramSource());
    }

    [Theory]
    [InlineData("services.AddScoped<IAuditReadRepository, EfAuditReadRepository>();")]
    [InlineData("services.AddScoped<ICustomerReadRepository, EfCustomerReadRepository>();")]
    [InlineData("services.AddScoped<IChannelReadRepository, EfChannelReadRepository>();")]
    [InlineData("services.AddScoped<ISalesLeadReadRepository>(sp => sp.GetRequiredService<EfSalesLeadRepository>());")]
    public void Phase2ReadRepositories_AreRegistered(string registration)
    {
        Assert.Contains(registration, ReadInfrastructureRegistrationSource());
    }

    private static string ReadProgramSource([CallerFilePath] string sourceFilePath = "") =>
        ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Api", "Program.cs"), sourceFilePath);

    private static string ReadInfrastructureRegistrationSource([CallerFilePath] string sourceFilePath = "") =>
        ReadRepositoryFile(
            Path.Combine("src", "InsuranceIntelligence.Infrastructure", "DependencyInjection", "InfrastructureRegistration.cs"),
            sourceFilePath);

    private static string ReadRepositoryFile(string relativePath, string sourceFilePath)
    {
        foreach (var startPath in new[]
                 {
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from the test runtime paths.");
    }

    private static string RouteBlock(string program, string route)
    {
        var start = program.IndexOf(route, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Route not found in Program.cs: {route}");

        var nextRoute = program.IndexOf("\napp.Map", start + route.Length, StringComparison.Ordinal);
        return nextRoute < 0 ? program[start..] : program[start..nextRoute];
    }
}

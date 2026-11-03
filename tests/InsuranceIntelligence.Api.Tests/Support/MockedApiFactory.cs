using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InsuranceIntelligence.Api.Tests.Support;

/// <summary>
/// Boots the real API in-process with every provider on its deterministic Mock adapter, in-memory
/// storage and Development header identity. This is the same configuration as
/// <c>scripts/run-local-mock.sh</c>, so HTTP-level tests exercise the real endpoints, filters,
/// repositories and tenant scoping with no external service.
///
/// EF's in-memory database is named and therefore shared by every host in the test process, so
/// each test must use its own tenant id (see <see cref="NewTenant"/>) rather than assume an empty
/// store. That is also what makes tenant-isolation assertions meaningful.
/// </summary>
public sealed class MockedApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string StorageRoot = Path.Combine(Path.GetTempPath(), "iip-tests-storage", Guid.NewGuid().ToString("N"));

    static MockedApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Set("INSURANCEINTELLIGENCE_AUTH_MODE", "Development");
        Set("INSURANCEINTELLIGENCE_STORAGE_PROVIDER", "InMemory");
        foreach (var name in new[] { "BLOB", "AI", "DOCUMENT", "RETRIEVAL", "QUEUE", "EMAIL", "COMMUNICATION", "NOTIFICATION", "SMS" })
        {
            Set($"INSURANCEINTELLIGENCE_{name}_PROVIDER", "Mock");
        }

        Set("INSURANCEINTELLIGENCE_SEED_DEMO_DATA", "false");
        Set("INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH", StorageRoot);
    }

    private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    public static string NewTenant() => $"t-{Guid.NewGuid():N}"[..14];

    /// <summary>An HTTP client that identifies as the given role in the given tenant workspace.</summary>
    public HttpClient CreateScopedClient(string tenantId, string workspaceId = "workspace-main", string role = "Admin", string userId = "test-admin")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId);
        client.DefaultRequestHeaders.Add("x-demo-role", role);
        client.DefaultRequestHeaders.Add("x-demo-user-id", userId);
        return client;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}

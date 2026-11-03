using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Api.Tests.Support;

public sealed record CaseInfo(Guid CaseId, string PolicyNumber, string Email, string Phone, string TenantId, string WorkspaceId);

/// <summary>Builders for HTTP-level tests: staff cases, files, and a claimant portal session that
/// is obtained exactly the way a customer gets one (magic link, read from the mock outbox).</summary>
public static class TestData
{
    public static async Task<CaseInfo> CreateCaseAsync(
        HttpClient staff,
        string tenantId,
        string workspaceId = "workspace-main",
        string productLine = "Motor",
        string claimType = "Own damage")
    {
        var policy = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var email = $"customer-{Guid.NewGuid():N}"[..20] + "@example.com";
        var phone = "+2567" + Random.Shared.Next(10_000_000, 99_999_999);
        var response = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Test Customer",
            customerEmail = email,
            customerPhone = phone,
            policyNumber = policy,
            productLine,
            claimType,
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway.",
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await MockedApiFactory.ReadJsonAsync(response);
        return new CaseInfo(body.GetProperty("caseId").GetGuid(), policy, email, phone, tenantId, workspaceId);
    }

    public static MultipartFormDataContent FileForm(string fileName, string content, string? documentTypeKey = null, string contentType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        if (documentTypeKey is not null)
        {
            form.Add(new StringContent(documentTypeKey), "documentTypeKey");
        }

        return form;
    }

    /// <summary>Signs a claimant in the way a customer does: asks for a magic link by policy number
    /// and email, reads the link from the mock outbox, redeems it. Returns a client carrying the
    /// session and the tenant headers a claimant's browser would send (and nothing staff-like).</summary>
    public static async Task<HttpClient> ClaimantClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, HttpClient staff, CaseInfo claim)
    {
        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", claim.TenantId);
        anonymous.DefaultRequestHeaders.Add("X-Workspace-Id", claim.WorkspaceId);

        var request = await anonymous.PostAsJsonAsync("/api/v1/client-portal/magic-links", new { policyNumber = claim.PolicyNumber, contactValue = claim.Email });
        Assert.True(request.IsSuccessStatusCode, await request.Content.ReadAsStringAsync());

        var outbox = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/dev/mock-outbox"));
        var token = outbox.EnumerateArray()
            .Select(message => Regex.Match(message.GetProperty("body").GetString() ?? string.Empty, "token=([A-Za-z0-9_-]+)"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(token), "the magic link was not delivered to the mock outbox");

        var redeem = await anonymous.PostAsJsonAsync("/api/v1/client-portal/magic-links/redeem", new { token });
        Assert.True(redeem.IsSuccessStatusCode, await redeem.Content.ReadAsStringAsync());
        var session = (await MockedApiFactory.ReadJsonAsync(redeem)).GetProperty("sessionToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", claim.TenantId);
        client.DefaultRequestHeaders.Add("X-Workspace-Id", claim.WorkspaceId);
        client.DefaultRequestHeaders.Add("X-Client-Portal-Session", session);
        return client;
    }
}

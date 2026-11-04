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

    /// <summary>Creates, configures, submits and approves a product with two different admins (the
    /// maker/checker rule), and returns its id. Claim types and a document matrix are optional.</summary>
    public static async Task<Guid> PublishProductAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string tenantId,
        string productClass = "Motor",
        object[]? claimTypes = null,
        object[]? documentMatrix = null,
        string workspaceId = "workspace-main")
    {
        using var maker = factory.CreateScopedClient(tenantId, workspaceId, "Admin", "maker-1");
        using var checker = factory.CreateScopedClient(tenantId, workspaceId, "Admin", "checker-1");

        var created = await maker.PostAsJsonAsync("/api/v1/products", new { name = $"{productClass} Cover", productClass, changeReason = "test" });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var productId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("productId").GetGuid();

        var updated = await maker.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            iraApprovalReference = "TEST-REF-1",
            iraApprovalEffectiveDate = "2026-01-01",
            claimTypes,
            documentMatrix,
            changeReason = "configure for test",
        });
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());

        var submitted = await maker.PostAsJsonAsync($"/api/v1/products/{productId}/submit", new { changeReason = "ready" });
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync());
        var approved = await checker.PostAsJsonAsync($"/api/v1/products/{productId}/approve", new { changeReason = "approved" });
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        return productId;
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
    /// session and the tenant headers a claimant's browser would send (and nothing staff-like).
    /// The claimant has already read and agreed to the privacy notice (build-loop item 9), through
    /// the real endpoint, because most tests are about what happens after that; pass
    /// <c>agreeToPrivacyNotice: false</c> to test the consent step itself.</summary>
    public static async Task<HttpClient> ClaimantClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, HttpClient staff, CaseInfo claim, bool agreeToPrivacyNotice = true)
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
        if (agreeToPrivacyNotice)
        {
            var notice = await MockedApiFactory.ReadJsonAsync(await client.GetAsync("/api/v1/client-portal/privacy-notice"));
            var agreed = await client.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = $"v{notice.GetProperty("noticeVersion").GetInt32()}" });
            Assert.True(agreed.IsSuccessStatusCode, await agreed.Content.ReadAsStringAsync());
        }

        return client;
    }
}

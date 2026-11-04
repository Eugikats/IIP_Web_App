using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageMagick;
using ImageMagick.Drawing;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 18: a review score and a recommendation, both explained, both advice, both
/// behind a person's own reading and response.</summary>
public sealed class ClaimAssessmentApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static MagickImage Picture(int seed)
    {
        var random = new Random(seed);
        var image = new MagickImage(MagickColors.White, 800, 600);
        for (var i = 0; i < 14; i++)
        {
            var x = random.Next(0, 90) / 100.0 * 800;
            var y = random.Next(0, 90) / 100.0 * 600;
            var colour = new MagickColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            image.Draw(new DrawableFillColor(colour), new DrawableRectangle(x, y, x + random.Next(60, 300), y + random.Next(60, 250)));
        }

        return image;
    }

    /// <summary>A photo whose camera says it was taken a month before the incident: a High finding.</summary>
    private static byte[] OldPhoto(int seed)
    {
        var image = Picture(seed);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Make, "TestCam");
        profile.SetValue(ExifTag.Model, "Evidence 1");
        profile.SetValue(ExifTag.DateTimeOriginal, DateTime.UtcNow.AddDays(-40).ToString("yyyy:MM:dd HH:mm:ss"));
        image.SetProfile(profile);
        image.Format = MagickFormat.Jpeg;
        return image.ToByteArray();
    }

    private static async Task UploadPhotoAsync(HttpClient claimant, Guid caseId, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "files", "IMG_1.jpg");
        form.Add(new StringContent("PhotoEvidence"), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        return (tenant, staff, await TestData.CreateCaseAsync(staff, tenant));
    }

    private static async Task<JsonElement> AssessmentAsync(HttpClient staff, Guid caseId)
    {
        var response = await staff.GetAsync($"/api/v1/cases/{caseId}/assessment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private static JsonElement[] Factors(JsonElement assessment) =>
        assessment.GetProperty("score").GetProperty("factors").EnumerateArray().ToArray();

    private static async Task<JsonElement> RefreshAsync(HttpClient staff, Guid caseId)
    {
        var response = await staff.PostAsync($"/api/v1/cases/{caseId}/assessment/refresh", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private static async Task<HttpResponseMessage> PostResponseAsync(HttpClient staff, Guid caseId, string response, string? note = null) =>
        await staff.PostAsJsonAsync($"/api/v1/cases/{caseId}/assessment/response", new { response, note });

    [Fact]
    public async Task ANewClaimWithNothingFlaggedIsToldWhatIsMissingAndItsScoreIsZero()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var assessment = await AssessmentAsync(staff, claim.CaseId);

        Assert.Equal(0, assessment.GetProperty("score").GetProperty("value").GetInt32());
        Assert.Equal("Low", assessment.GetProperty("score").GetProperty("band").GetString());
        Assert.Empty(Factors(assessment));
        Assert.Equal("RequestInformation", assessment.GetProperty("recommendation").GetProperty("kind").GetString());
        Assert.NotEmpty(assessment.GetProperty("recommendation").GetProperty("suggestedRequests").EnumerateArray());
        Assert.Equal($"CLM-{claim.CaseId.ToString("N")[..8].ToUpperInvariant()}", assessment.GetProperty("claimReference").GetString());
        Assert.Contains("does not approve", assessment.GetProperty("notice").GetString());
    }

    [Fact]
    public async Task RefreshAddsAWrittenExplanationLabelledAsMockAndSavesIt()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var refreshed = await RefreshAsync(staff, claim.CaseId);

        Assert.True(refreshed.GetProperty("prepared").GetBoolean());
        Assert.StartsWith("MOCK AI OUTPUT", refreshed.GetProperty("narrative").GetString());
        Assert.True(refreshed.GetProperty("narrativeIsMock").GetBoolean());
        Assert.DoesNotContain((char)0x2014, refreshed.GetProperty("narrative").GetString()!);

        var later = await AssessmentAsync(staff, claim.CaseId);
        Assert.True(later.GetProperty("prepared").GetBoolean());
        Assert.False(later.GetProperty("stale").GetBoolean());
        Assert.Equal(refreshed.GetProperty("narrative").GetString(), later.GetProperty("narrative").GetString());
        Assert.Contains("claim.assessment.prepared", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"));
    }

    [Fact]
    public async Task TheReviewJobPreparesTheAssessmentWithoutAnyoneAskingWhenADocumentArrives()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(new BackgroundPipelineOptions
            {
                Inline = true,
                WorkerEnabled = false,
                BackoffSeconds = [0],
                ReviewSettleSeconds = 0,
            }))));
        var tenant = MockedApiFactory.NewTenant();
        using var staff = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(host, staff, claim);

        using var form = TestData.FileForm("police.pdf", "IIP-MOCK-DOC:PoliceAbstract", "PoliceAbstract", "application/pdf");
        Assert.True((await claimant.PostAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents", form)).IsSuccessStatusCode);
        Assert.False((await AssessmentAsync(staff, claim.CaseId)).GetProperty("prepared").GetBoolean());

        var processor = host.Services.GetRequiredService<IBackgroundJobProcessor>();
        Assert.True(await processor.ProcessNextAsync([BackgroundQueues.ClaimReview], "test-worker", CancellationToken.None, TenantScope.Create(tenant, "workspace-main")));

        var assessment = await AssessmentAsync(staff, claim.CaseId);
        Assert.True(assessment.GetProperty("prepared").GetBoolean(), assessment.GetRawText());
        Assert.Equal("system:claim-triage", assessment.GetProperty("generatedBy").GetString());
        Assert.StartsWith("MOCK AI OUTPUT", assessment.GetProperty("narrative").GetString());
    }

    [Fact]
    public async Task AModelNoticedConcernIsNamedAsTheModelsAndCountedLightly()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var create = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Test Customer",
            customerEmail = $"model-{Guid.NewGuid():N}"[..20] + "@example.com",
            customerPhone = "+2567" + Random.Shared.Next(10_000_000, 99_999_999),
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            productLine = "Motor",
            claimType = "Own damage",
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision. IIP-MOCK-RISK",
        });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        var caseId = (await MockedApiFactory.ReadJsonAsync(create)).GetProperty("caseId").GetGuid();
        Assert.True((await staff.PostAsync($"/api/v1/cases/{caseId}/summary", null)).IsSuccessStatusCode);

        var assessment = await AssessmentAsync(staff, caseId);

        var factor = Assert.Single(Factors(assessment));
        Assert.Equal("ModelReading", factor.GetProperty("source").GetString());
        Assert.Equal("Model reading", factor.GetProperty("sourceLabel").GetString());
        Assert.Equal("Light", factor.GetProperty("strength").GetString());
        Assert.Equal(8, assessment.GetProperty("score").GetProperty("value").GetInt32());
        Assert.Equal("Low", assessment.GetProperty("score").GetProperty("band").GetString());
        Assert.Equal("Not looked at yet", factor.GetProperty("readingLabel").GetString());
    }

    [Fact]
    public async Task APictureFindingRaisesTheScoreAndAPersonsReadingOfItChangesBothTheScoreAndTheAdvice()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);
        await UploadPhotoAsync(claimant, claim.CaseId, OldPhoto(7));
        var report = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/authenticity"))).EnumerateArray().Single();
        var documentId = report.GetProperty("documentId").GetGuid();

        var flagged = await AssessmentAsync(staff, claim.CaseId);
        var factor = Assert.Single(Factors(flagged));
        Assert.Equal("PictureCheck", factor.GetProperty("source").GetString());
        Assert.Equal("Picture", factor.GetProperty("reference").GetProperty("kind").GetString());
        Assert.Equal(documentId.ToString(), factor.GetProperty("reference").GetProperty("id").GetString());
        Assert.Contains("days before the incident", factor.GetProperty("reason").GetString());
        Assert.Equal(40, flagged.GetProperty("score").GetProperty("value").GetInt32());
        Assert.Equal("Moderate", flagged.GetProperty("score").GetProperty("band").GetString());
        Assert.Equal("ReviewFlaggedItems", flagged.GetProperty("recommendation").GetProperty("kind").GetString());

        // A person looks and is satisfied: it stops counting and the advice moves on.
        Assert.True((await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/authenticity/disposition", new { disposition = "NoConcern" })).IsSuccessStatusCode);
        var settled = await AssessmentAsync(staff, claim.CaseId);
        Assert.Equal(0, settled.GetProperty("score").GetProperty("value").GetInt32());
        Assert.False(Factors(settled).Single().GetProperty("counts").GetBoolean());
        Assert.Equal("Set aside by a person", Factors(settled).Single().GetProperty("readingLabel").GetString());
        Assert.NotEqual("ReviewFlaggedItems", settled.GetProperty("recommendation").GetProperty("kind").GetString());

        // A person raises a concern: the advice is to consider referring.
        Assert.True((await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/authenticity/disposition", new { disposition = "ConcernRaised", note = "The date on the photo does not fit." })).IsSuccessStatusCode);
        var raised = await AssessmentAsync(staff, claim.CaseId);
        Assert.Equal("ReferForInvestigation", raised.GetProperty("recommendation").GetProperty("kind").GetString());
        Assert.Equal("Concern raised by a person", Factors(raised).Single().GetProperty("readingLabel").GetString());
        var reasons = raised.GetProperty("recommendation").GetProperty("reasons").EnumerateArray().ToArray();
        Assert.Contains(reasons, reason => reason.GetProperty("source").GetProperty("kind").GetString() == "Picture" && reason.GetProperty("source").GetProperty("id").GetString() == documentId.ToString());
    }

    [Fact]
    public async Task ASharedContactIsAFixedRuleReasonAndAPersonsTriageIsReflected()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var email = $"shared-{Guid.NewGuid():N}"[..20] + "@example.com";

        async Task<Guid> CreateAsync()
        {
            var response = await staff.PostAsJsonAsync("/api/v1/cases", new
            {
                customerName = "Test Customer",
                customerEmail = email,
                customerPhone = "+2567" + Random.Shared.Next(10_000_000, 99_999_999),
                policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                productLine = "Motor",
                claimType = "Own damage",
                channel = "Phone",
                incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
                incidentDescription = "Collision on the highway.",
            });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("caseId").GetGuid();
        }

        var first = await CreateAsync();
        var second = await CreateAsync();

        var assessment = await AssessmentAsync(staff, first);
        var factor = Assert.Single(Factors(assessment));
        Assert.Equal("RuleEngine", factor.GetProperty("source").GetString());
        Assert.Equal("Fixed rule", factor.GetProperty("sourceLabel").GetString());
        Assert.Equal("Signal", factor.GetProperty("reference").GetProperty("kind").GetString());
        Assert.Contains("share the same email", factor.GetProperty("reason").GetString());
        Assert.DoesNotMatch("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", factor.GetProperty("reason").GetString()!);
        Assert.Equal(25, assessment.GetProperty("score").GetProperty("value").GetInt32());
        Assert.Equal("Not looked at yet", factor.GetProperty("readingLabel").GetString());

        // The same signal is on the other claim too.
        Assert.Single(Factors(await AssessmentAsync(staff, second)));

        // A person triages it: it is now being worked and the advice says to wait for that.
        using var triage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fraud/signals/triage")
        {
            Content = JsonContent.Create(new { ruleId = "R-DUP-CONTACT", relatedCaseIds = new[] { first, second } }),
        };
        triage.Headers.Add("X-Audit-Reason", "Checking whether this is one household.");
        Assert.True((await staff.SendAsync(triage)).IsSuccessStatusCode);
        var working = await AssessmentAsync(staff, first);
        Assert.Equal("Being worked", Factors(working).Single().GetProperty("readingLabel").GetString());
        Assert.Equal("ReviewFlaggedItems", working.GetProperty("recommendation").GetProperty("kind").GetString());
        Assert.Contains("still being worked", working.GetProperty("recommendation").GetProperty("headline").GetString());

        // Another tenant sharing the same email is not related.
        var otherTenant = MockedApiFactory.NewTenant();
        using var otherStaff = factory.CreateScopedClient(otherTenant);
        var otherResponse = await otherStaff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Test Customer",
            customerEmail = email,
            customerPhone = "+2567" + Random.Shared.Next(10_000_000, 99_999_999),
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            productLine = "Motor",
            claimType = "Own damage",
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway.",
        });
        var otherCase = (await MockedApiFactory.ReadJsonAsync(otherResponse)).GetProperty("caseId").GetGuid();
        Assert.Empty(Factors(await AssessmentAsync(otherStaff, otherCase)));
    }

    [Fact]
    public async Task DisagreeingNeedsAReasonAndAResponseIsRecordedAndAudited()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        Assert.Equal(HttpStatusCode.BadRequest, (await PostResponseAsync(staff, claim.CaseId, "Disagree")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostResponseAsync(staff, claim.CaseId, "Maybe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostResponseAsync(staff, claim.CaseId, "Disagree", new string('x', 1001))).StatusCode);

        var ok = await PostResponseAsync(staff, claim.CaseId, "Disagree", "The customer already sent it by post.");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(ok);
        Assert.Equal("Disagree", body.GetProperty("response").GetProperty("kind").GetString());
        Assert.Equal("The customer already sent it by post.", body.GetProperty("response").GetProperty("note").GetString());
        Assert.Equal("test-admin", body.GetProperty("response").GetProperty("byUserId").GetString());

        var read = await AssessmentAsync(staff, claim.CaseId);
        Assert.Equal("Disagree", read.GetProperty("response").GetProperty("kind").GetString());
        Assert.Contains("claim.assessment.response", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"));
    }

    [Fact]
    public async Task AResponseBelongsToTheFactsItWasGivenOnAndDoesNotSurviveNewOnes()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        Assert.True((await PostResponseAsync(staff, claim.CaseId, "Agree")).IsSuccessStatusCode);
        Assert.Equal("Agree", (await AssessmentAsync(staff, claim.CaseId)).GetProperty("response").GetProperty("kind").GetString());

        await UploadPhotoAsync(claimant, claim.CaseId, OldPhoto(11));

        var after = await AssessmentAsync(staff, claim.CaseId);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("response").ValueKind);
        Assert.True(after.GetProperty("stale").GetBoolean() || after.GetProperty("prepared").GetBoolean());
    }

    [Fact]
    public async Task WordingThatSoundsLikeADecisionIsDroppedButTheScoreAndAdviceRemain()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IChatCompletionProvider>(new FixedChat("The claim is approved and the customer committed fraud.")))));
        var tenant = MockedApiFactory.NewTenant();
        using var staff = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var refreshed = await RefreshAsync(staff, claim.CaseId);

        Assert.Equal(JsonValueKind.Null, refreshed.GetProperty("narrative").ValueKind);
        Assert.Equal("RequestInformation", refreshed.GetProperty("recommendation").GetProperty("kind").GetString());
        Assert.NotNull(refreshed.GetProperty("score").GetProperty("band").GetString());
    }

    [Fact]
    public async Task AModelThatFailsNeverTakesTheScoreOrTheAdviceWithIt()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IChatCompletionProvider>(new FixedChat(null)))));
        var tenant = MockedApiFactory.NewTenant();
        using var staff = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var refreshed = await RefreshAsync(staff, claim.CaseId);

        Assert.Equal(JsonValueKind.Null, refreshed.GetProperty("narrative").ValueKind);
        Assert.True(refreshed.GetProperty("prepared").GetBoolean());
        Assert.Equal("RequestInformation", refreshed.GetProperty("recommendation").GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    [InlineData("Client")]
    public async Task OnlyRolesThatSeeFraudSignalsCanReadTheAssessment(string role)
    {
        var (tenant, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/cases/{claim.CaseId}/assessment/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostResponseAsync(other, claim.CaseId, "Agree")).StatusCode);
    }

    [Theory]
    [InlineData("ClaimsSupervisor")]
    [InlineData("Compliance")]
    public async Task SupervisorsAndComplianceCanReadAndRespond(string role)
    {
        var (tenant, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var other = factory.CreateScopedClient(tenant, role: role, userId: "reviewer-1");

        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment")).StatusCode);
        var response = await PostResponseAsync(other, claim.CaseId, "Agree");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reviewer-1", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("response").GetProperty("byUserId").GetString());
    }

    [Fact]
    public async Task AnotherTenantCannotReadRefreshOrRespondToThisClaimsAssessment()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await RefreshAsync(staff, claim.CaseId);
        using var intruder = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/v1/cases/{claim.CaseId}/assessment/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostResponseAsync(intruder, claim.CaseId, "Agree")).StatusCode);

        // A different workspace of the same tenant is just as separate.
        using var otherWorkspace = factory.CreateScopedClient(claim.TenantId, workspaceId: "workspace-other");
        Assert.Equal(HttpStatusCode.NotFound, (await otherWorkspace.GetAsync($"/api/v1/cases/{claim.CaseId}/assessment")).StatusCode);

        // And the owner still sees its own, untouched.
        Assert.True((await AssessmentAsync(staff, claim.CaseId)).GetProperty("prepared").GetBoolean());
    }

    [Fact]
    public async Task AnUnknownClaimIsNotFound()
    {
        var (_, staff, _) = await ArrangeAsync();
        using var _s = staff;

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/cases/{Guid.NewGuid()}/assessment")).StatusCode);
    }

    private sealed class FixedChat(string? content) : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            if (content is null)
            {
                throw new InvalidOperationException("The model is unavailable.");
            }

            return Task.FromResult(new ChatCompletionResult(content, "Fixed", "fixed-chat", DateTimeOffset.UtcNow, "fixed-v1", 1, 1));
        }
    }
}

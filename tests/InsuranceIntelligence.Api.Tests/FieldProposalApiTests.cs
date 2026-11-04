using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 15: what a document says is offered as a proposal for the claim's
/// questions, with source, page and confidence; an officer accepts, corrects or rejects; a value a
/// person already put on the claim is never replaced on its own.</summary>
public sealed class FieldProposalApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Stage = "acknowledgement_and_document_request";

    private static object[] ReadingRules(params (string Extracted, string Field)[] rules) =>
        rules.Select(rule => (object)new { extractedKey = rule.Extracted, claimFieldKey = rule.Field }).ToArray();

    /// <summary>A workspace whose Motor product asks for a police report number and a report date and
    /// whose police report is read into them.</summary>
    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim)> ArrangeAsync(params (string Extracted, string Field)[] rules)
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        using var maker = factory.CreateScopedClient(tenant, "workspace-main", "Admin", "maker-1");
        using var checker = factory.CreateScopedClient(tenant, "workspace-main", "Admin", "checker-1");
        var created = await maker.PostAsJsonAsync("/api/v1/products", new { name = "Motor Cover", productClass = "Motor", changeReason = "test" });
        var productId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("productId").GetGuid();
        var updated = await maker.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            iraApprovalReference = "TEST-REF-1",
            iraApprovalEffectiveDate = "2026-01-01",
            claimTypes = new object[] { new { name = "Own damage", active = true } },
            documentMatrix = new object[]
            {
                new { documentKey = "PoliceAbstract", customerLabel = "Police report", reason = "needed", isRequired = true, requiredAtStageKey = Stage, isRegulatoryOrWordingMandated = false },
            },
            claimFields = new object[]
            {
                new { label = "Police report number", type = "Text", required = false, sortOrder = 1, customerVisible = true, claimTypeKeys = Array.Empty<string>() },
                new { label = "Report date", key = "reportDate", type = "Date", required = false, sortOrder = 2, customerVisible = true, claimTypeKeys = Array.Empty<string>() },
            },
            changeReason = "configure",
        });
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        Assert.True((await maker.PostAsJsonAsync($"/api/v1/products/{productId}/submit", new { changeReason = "go" })).IsSuccessStatusCode);
        Assert.True((await checker.PostAsJsonAsync($"/api/v1/products/{productId}/approve", new { changeReason = "ok" })).IsSuccessStatusCode);

        var rulesToUse = rules.Length > 0 ? rules : [("ReportNumber", "policeReportNumber"), ("ReportDate", "reportDate")];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/document-types/PoliceAbstract")
        {
            Content = JsonContent.Create(new { customerLabel = "Police report", description = "Confirms the incident was reported.", active = true, extractionFields = ReadingRules(rulesToUse) }),
        };
        request.Headers.Add("X-Audit-Reason", "Set the reading rules");
        var saved = await staff.SendAsync(request);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());

        var claim = await TestData.CreateCaseAsync(staff, tenant);
        return (tenant, staff, claim);
    }

    private static async Task UploadPoliceReportAsync(HttpClient staff, Guid caseId, string content = "IIP-MOCK-DOC:PoliceAbstract", string name = "police.pdf")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", name);
        var response = await staff.PostAsync($"/api/v1/cases/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement[]> ProposalsAsync(HttpClient staff, Guid caseId) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/field-proposals"))).EnumerateArray().ToArray();

    private static JsonElement ForField(JsonElement[] proposals, string field) =>
        proposals.Single(proposal => proposal.GetProperty("fieldKey").GetString() == field);

    private static async Task<JsonElement> FieldAsync(HttpClient staff, Guid caseId, string key) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/fields")))
            .GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("key").GetString() == key);

    [Fact]
    public async Task WhatADocumentSaysIsOfferedWithItsSourceAndConfidence_AndNothingChangesOnTheClaim()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        await UploadPoliceReportAsync(staff, claim.CaseId);

        var proposals = await ProposalsAsync(staff, claim.CaseId);
        Assert.Equal(2, proposals.Length);
        var number = ForField(proposals, "policeReportNumber");
        Assert.Equal("MOCK-POL-0001", number.GetProperty("value").GetString());
        Assert.Equal("Proposed", number.GetProperty("status").GetString());
        Assert.Equal("Police report number", number.GetProperty("fieldLabel").GetString());
        Assert.StartsWith("PoliceReport_CLM-", number.GetProperty("documentName").GetString());
        Assert.True(number.GetProperty("confidence").GetDecimal() > 0.5m);
        Assert.False(number.GetProperty("conflictsWithConfirmed").GetBoolean());
        Assert.Equal("2026-10-01", ForField(proposals, "reportDate").GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Null, (await FieldAsync(staff, claim.CaseId, "policeReportNumber")).GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task AcceptingPutsTheValueOnTheClaimWithItsSource_AndTheAuditTrailHoldsKeysNotValues()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId);
        var proposal = ForField(await ProposalsAsync(staff, claim.CaseId), "policeReportNumber");

        var accepted = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{proposal.GetProperty("proposalId").GetGuid()}/accept", new { });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var field = await FieldAsync(staff, claim.CaseId, "policeReportNumber");
        Assert.Equal("MOCK-POL-0001", field.GetProperty("value").GetString());
        Assert.Equal("Document", field.GetProperty("source").GetString());
        Assert.Equal(proposal.GetProperty("documentId").GetGuid(), field.GetProperty("sourceDocumentId").GetGuid());
        Assert.Equal("test-admin", field.GetProperty("confirmedBy").GetString());
        var audit = await staff.GetStringAsync("/api/v1/audit/events");
        Assert.Contains("claim.fields.proposal.accepted", audit);
        Assert.Contains("claim.fields.proposed", audit);
        Assert.DoesNotContain("MOCK-POL-0001", audit);
    }

    [Fact]
    public async Task AnOfficerCanCorrectAValueWhileAccepting()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId);
        var proposal = ForField(await ProposalsAsync(staff, claim.CaseId), "policeReportNumber");

        var accepted = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{proposal.GetProperty("proposalId").GetGuid()}/accept", new { value = "CRB/2026/4471" });

        var result = await MockedApiFactory.ReadJsonAsync(accepted);
        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        Assert.Equal("MOCK-POL-0001", result.GetProperty("value").GetString());
        Assert.Equal("CRB/2026/4471", result.GetProperty("acceptedValue").GetString());
        Assert.Equal("CRB/2026/4471", (await FieldAsync(staff, claim.CaseId, "policeReportNumber")).GetProperty("value").GetString());
        Assert.Contains("claim.fields.proposal.corrected", await staff.GetStringAsync("/api/v1/audit/events"));
    }

    [Fact]
    public async Task ARejectedSuggestionStaysRejectedAndIsNotOfferedAgainForTheSameReading()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId);
        var proposals = await ProposalsAsync(staff, claim.CaseId);
        var proposal = ForField(proposals, "policeReportNumber");
        var documentId = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).EnumerateArray().Single().GetProperty("documentId").GetGuid();

        var rejected = await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{proposal.GetProperty("proposalId").GetGuid()}/reject", null);
        await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/reprocess", null);

        Assert.Equal("Rejected", (await MockedApiFactory.ReadJsonAsync(rejected)).GetProperty("status").GetString());
        var after = await ProposalsAsync(staff, claim.CaseId);
        Assert.Equal("Rejected", ForField(after, "policeReportNumber").GetProperty("status").GetString());
        Assert.Single(after, candidate => candidate.GetProperty("fieldKey").GetString() == "policeReportNumber");
        Assert.Equal(JsonValueKind.Null, (await FieldAsync(staff, claim.CaseId, "policeReportNumber")).GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task APersonConfirmedValueIsNeverReplacedUnlessSomeoneExplicitlyChoosesTo()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        var typed = await staff.PutAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-values", new { values = new Dictionary<string, string> { ["policeReportNumber"] = "TYPED-1" } });
        Assert.True(typed.IsSuccessStatusCode, await typed.Content.ReadAsStringAsync());

        await UploadPoliceReportAsync(staff, claim.CaseId);

        var proposal = ForField(await ProposalsAsync(staff, claim.CaseId), "policeReportNumber");
        Assert.True(proposal.GetProperty("conflictsWithConfirmed").GetBoolean());
        Assert.Equal("TYPED-1", proposal.GetProperty("currentValue").GetString());
        var id = proposal.GetProperty("proposalId").GetGuid();

        var refused = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("needs_replace", (await MockedApiFactory.ReadJsonAsync(refused)).GetProperty("code").GetString());
        Assert.Equal("TYPED-1", (await FieldAsync(staff, claim.CaseId, "policeReportNumber")).GetProperty("value").GetString());

        var replaced = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { replaceConfirmed = true });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal("MOCK-POL-0001", (await FieldAsync(staff, claim.CaseId, "policeReportNumber")).GetProperty("value").GetString());
    }

    [Fact]
    public async Task AValueTheClaimAlreadyHasIsNotProposedAgain()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await staff.PutAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-values", new { values = new Dictionary<string, string> { ["policeReportNumber"] = "MOCK-POL-0001" } });

        await UploadPoliceReportAsync(staff, claim.CaseId);

        var proposals = await ProposalsAsync(staff, claim.CaseId);
        Assert.DoesNotContain(proposals, proposal => proposal.GetProperty("fieldKey").GetString() == "policeReportNumber");
        Assert.Contains(proposals, proposal => proposal.GetProperty("fieldKey").GetString() == "reportDate");
    }

    [Fact]
    public async Task AValueThatDoesNotFitTheQuestionIsFlagged_AndCannotBeAcceptedUntilCorrected()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId, "IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FIELD:ReportDate=last tuesday");
        var proposal = ForField(await ProposalsAsync(staff, claim.CaseId), "reportDate");
        Assert.Equal("last tuesday", proposal.GetProperty("value").GetString());
        Assert.False(string.IsNullOrWhiteSpace(proposal.GetProperty("warning").GetString()));
        var url = $"/api/v1/cases/{claim.CaseId}/field-proposals/{proposal.GetProperty("proposalId").GetGuid()}/accept";

        var wrong = await staff.PostAsJsonAsync(url, new { });
        var fixedValue = await staff.PostAsJsonAsync(url, new { value = "2026-10-02" });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fixedValue.StatusCode);
        Assert.Equal("2026-10-02", (await FieldAsync(staff, claim.CaseId, "reportDate")).GetProperty("value").GetString());
    }

    [Fact]
    public async Task OnlyQuestionsTheClaimActuallyAsksAreProposed()
    {
        var (_, staff, claim) = await ArrangeAsync(("ReportNumber", "someQuestionNobodyAsks"), ("ReportDate", "reportDate"));
        using var _s = staff;

        await UploadPoliceReportAsync(staff, claim.CaseId);

        var proposals = await ProposalsAsync(staff, claim.CaseId);
        Assert.Single(proposals);
        Assert.Equal("reportDate", proposals[0].GetProperty("fieldKey").GetString());
    }

    [Fact]
    public async Task ADecidedSuggestionCannotBeDecidedAgain()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId);
        var id = ForField(await ProposalsAsync(staff, claim.CaseId), "reportDate").GetProperty("proposalId").GetGuid();
        await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { });

        var again = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { });
        var reject = await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/reject", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
    }

    [Fact]
    public async Task AnotherWorkspaceAndRolesWithoutClaimAccessCannotSeeOrDecide()
    {
        var (tenant, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await UploadPoliceReportAsync(staff, claim.CaseId);
        var id = ForField(await ProposalsAsync(staff, claim.CaseId), "reportDate").GetProperty("proposalId").GetGuid();
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var broker = factory.CreateScopedClient(tenant, role: "Broker");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/cases/{claim.CaseId}/field-proposals")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.GetAsync($"/api/v1/cases/{claim.CaseId}/field-proposals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/field-proposals/{id}/accept", new { })).StatusCode);
    }

    [Fact]
    public async Task ReadingRulesAreKeptPerDocumentTypeAndValidated()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        async Task<HttpResponseMessage> Save(object rules)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/document-types/LogBook")
            {
                Content = JsonContent.Create(new { customerLabel = "Vehicle log book", description = "The registration document.", active = true, extractionFields = rules }),
            };
            request.Headers.Add("X-Audit-Reason", "rules");
            return await staff.SendAsync(request);
        }

        var good = await Save(ReadingRules(("RegistrationNumber", "vehicleRegistration")));
        var half = await Save(new[] { new { extractedKey = "RegistrationNumber", claimFieldKey = "" } });
        var twice = await Save(ReadingRules(("A", "vehicleRegistration"), ("B", "vehicleRegistration")));

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal("vehicleRegistration", (await MockedApiFactory.ReadJsonAsync(good)).GetProperty("extractionFields")[0].GetProperty("claimFieldKey").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        using var again = new HttpRequestMessage(HttpMethod.Post, "/api/v1/document-types/LogBook")
        {
            Content = JsonContent.Create(new { customerLabel = "Vehicle log book (new wording)", description = "The registration document.", active = true }),
        };
        again.Headers.Add("X-Audit-Reason", "reword only");
        var reworded = await MockedApiFactory.ReadJsonAsync(await staff.SendAsync(again));
        Assert.Equal(1, reworded.GetProperty("extractionFields").GetArrayLength());
    }
}

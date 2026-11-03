using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 7: a policy wording is read into proposed product setup, every item
/// quotes its clause, a person decides each item, and only accepted items reach the product.</summary>
public sealed class WordingDraftApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static string WordingText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "web", "portal", "tests", "fixtures", "claims-core", "sample_policy_wording.txt")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory!.FullName, "web", "portal", "tests", "fixtures", "claims-core", "sample_policy_wording.txt"));
    }

    private async Task<(HttpClient Admin, Guid ProductId, Guid WordingId)> ArrangeAsync(string? text = null, bool attach = true)
    {
        var tenant = MockedApiFactory.NewTenant();
        var admin = factory.CreateScopedClient(tenant);
        var created = await admin.PostAsJsonAsync("/api/v1/products", new { name = "Motor Cover", productClass = "Motor", changeReason = "new" });
        var productId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("productId").GetGuid();

        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text ?? WordingText())), "file", "wording.txt" },
            { new StringContent("Private motor wording"), "documentName" },
            { new StringContent("Motor"), "productLine" },
            { new StringContent("1.0"), "version" },
            { new StringContent("PolicyWording"), "documentType" },
        };
        var uploaded = await admin.PostAsync("/api/v1/knowledge-documents", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var wordingId = (await MockedApiFactory.ReadJsonAsync(uploaded)).GetProperty("knowledgeDocumentId").GetGuid();

        if (attach)
        {
            var attached = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { approvedWordingKnowledgeDocumentId = wordingId.ToString(), changeReason = "attach wording" });
            Assert.True(attached.IsSuccessStatusCode, await attached.Content.ReadAsStringAsync());
        }

        return (admin, productId, wordingId);
    }

    private static async Task<JsonElement> DraftAsync(HttpClient admin, Guid productId)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private static JsonElement Item(JsonElement draft, string kind, string titleContains)
    {
        var matches = draft.GetProperty("items").EnumerateArray().Where(item =>
            item.GetProperty("kind").GetString() == kind && item.GetProperty("title").GetString()!.Contains(titleContains, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(matches.Length > 0,
            $"No {kind} item contained '{titleContains}'. Actual items: {draft.GetProperty("items").GetRawText()}");
        return matches[0];
    }

    private static Task<HttpResponseMessage> Decide(HttpClient admin, Guid productId, JsonElement draft, JsonElement item, object body) =>
        admin.PutAsJsonAsync(
            $"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/items/{item.GetProperty("itemId").GetString()}",
            body);

    [Fact]
    public async Task TheWordingIsReadIntoItemsThatEachQuoteTheirClause()
    {
        var (admin, productId, _) = await ArrangeAsync();

        var draft = await DraftAsync(admin, productId);

        Assert.Equal("InReview", draft.GetProperty("status").GetString());
        var kinds = draft.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("kind").GetString()).Distinct().ToArray();
        Assert.Contains("Covered", kinds);
        Assert.Contains("Exclusion", kinds);
        Assert.Contains("ClaimType", kinds);
        Assert.Contains("Document", kinds);
        Assert.Contains("Field", kinds);

        foreach (var item in draft.GetProperty("items").EnumerateArray())
        {
            var citation = item.GetProperty("citation");
            Assert.True(citation.GetProperty("verified").GetBoolean(), item.GetProperty("title").GetString());
            Assert.False(string.IsNullOrWhiteSpace(citation.GetProperty("quote").GetString()));
            Assert.Equal("Pending", item.GetProperty("decision").GetString());
        }

        var theft = Item(draft, "Covered", "Theft");
        Assert.Equal("1.2", theft.GetProperty("citation").GetProperty("clause").GetString());
        Assert.Equal(1, theft.GetProperty("citation").GetProperty("page").GetInt32());
        var claimsClause = Item(draft, "ClaimType", "Theft");
        Assert.Equal(2, claimsClause.GetProperty("citation").GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task NothingChangesTheProductUntilItemsAreAcceptedAndApplied()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var before = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));

        var draft = await DraftAsync(admin, productId);
        var after = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));

        Assert.Equal(before.GetProperty("version").GetInt32(), after.GetProperty("version").GetInt32());
        Assert.Equal(0, after.GetProperty("coverageItems").GetArrayLength());

        var empty = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("Accept at least one", await empty.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AcceptedItemsAreWrittenIntoANewDraftVersionAndRejectedOnesAreNot()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);

        var covered = Item(draft, "Covered", "Fire");
        var excluded = Item(draft, "Exclusion", "Wear and tear");
        var claimType = Item(draft, "ClaimType", "Theft");
        var police = draft.GetProperty("items").EnumerateArray().First(item =>
            item.GetProperty("kind").GetString() == "Document" && item.GetProperty("documentKey").GetString() == "PoliceAbstract"
            && item.GetProperty("claimTypeNames").EnumerateArray().Any(name => name.GetString() == "Theft"));
        var skipped = Item(draft, "Covered", "Windscreen");

        foreach (var item in new[] { covered, excluded, claimType, police })
        {
            Assert.Equal(HttpStatusCode.OK, (await Decide(admin, productId, draft, item, new { decision = "Accepted" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await Decide(admin, productId, draft, skipped, new { decision = "Rejected" })).StatusCode);

        var applied = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { changeReason = "From the wording" });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(applied);
        Assert.Equal(2, result.GetProperty("coverageAdded").GetInt32());
        Assert.Equal(1, result.GetProperty("claimTypesAdded").GetInt32());
        Assert.Equal(1, result.GetProperty("documentsAdded").GetInt32());
        Assert.Equal("Applied", result.GetProperty("draft").GetProperty("status").GetString());

        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));
        Assert.Equal("Draft", product.GetProperty("status").GetString());
        var coverage = product.GetProperty("coverageItems").EnumerateArray().ToArray();
        Assert.Contains(coverage, entry => entry.GetProperty("kind").GetString() == "Covered" && entry.GetProperty("title").GetString()!.Contains("Fire"));
        Assert.Contains(coverage, entry => entry.GetProperty("kind").GetString() == "Excluded" && entry.GetProperty("clause").GetString() == "2.1");
        Assert.DoesNotContain(coverage, entry => entry.GetProperty("title").GetString()!.Contains("Windscreen"));
        Assert.Contains(product.GetProperty("claimTypes").EnumerateArray(), type => type.GetProperty("key").GetString() == "theft");
        var requirement = product.GetProperty("documentMatrix").EnumerateArray().Single(entry => entry.GetProperty("documentKey").GetString() == "PoliceAbstract");
        Assert.Equal("theft", requirement.GetProperty("claimTypeKeys").EnumerateArray().Single().GetString());
        Assert.True(requirement.GetProperty("isRegulatoryOrWordingMandated").GetBoolean());

        var again = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task ADocumentForAClaimTypeThatIsNotAcceptedIsLeftOutWithTheReason()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var photos = draft.GetProperty("items").EnumerateArray().First(item =>
            item.GetProperty("kind").GetString() == "Document" && item.GetProperty("documentKey").GetString() == "PhotoEvidence");
        var fire = Item(draft, "Covered", "Fire");
        await Decide(admin, productId, draft, photos, new { decision = "Accepted" });
        await Decide(admin, productId, draft, fire, new { decision = "Accepted" });

        var applied = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { });

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(applied);
        Assert.Equal(0, result.GetProperty("documentsAdded").GetInt32());
        var reason = result.GetProperty("skipped").EnumerateArray().Single().GetProperty("reason").GetString();
        Assert.Contains("no claim type called", reason);
    }

    [Fact]
    public async Task AnItemWithNoMatchingDocumentTypeCannotBeAcceptedUntilOneIsChosen()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var logBook = draft.GetProperty("items").EnumerateArray().First(item =>
            item.GetProperty("kind").GetString() == "Document" && item.GetProperty("title").GetString()!.Contains("log book", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("document type", logBook.GetProperty("problem").GetString());

        var refused = await Decide(admin, productId, draft, logBook, new { decision = "Accepted" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var chosen = await Decide(admin, productId, draft, logBook, new { documentKey = "invoice", decision = "Accepted" });
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        var updated = (await MockedApiFactory.ReadJsonAsync(chosen)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("itemId").GetString() == logBook.GetProperty("itemId").GetString());
        Assert.Equal("Invoice", updated.GetProperty("documentKey").GetString());
        Assert.True(updated.GetProperty("edited").GetBoolean());
        Assert.Equal("Accepted", updated.GetProperty("decision").GetString());

        var unknown = await Decide(admin, productId, draft, logBook, new { documentKey = "NotAType" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task EditingAnAcceptedItemSendsItBackForAcceptance()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var fire = Item(draft, "Covered", "Fire");
        await Decide(admin, productId, draft, fire, new { decision = "Accepted" });

        var edited = await Decide(admin, productId, draft, fire, new { title = "Fire and lightning" });

        var item = (await MockedApiFactory.ReadJsonAsync(edited)).GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("itemId").GetString() == fire.GetProperty("itemId").GetString());
        Assert.Equal("Fire and lightning", item.GetProperty("title").GetString());
        Assert.Equal("Pending", item.GetProperty("decision").GetString());
        Assert.True(item.GetProperty("edited").GetBoolean());
        Assert.True(item.GetProperty("citation").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public async Task AnAppliedQuestionKeepsTheBuiltInQuestionsAndIsOffered()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var question = Item(draft, "Field", "Place of the loss");
        await Decide(admin, productId, draft, question, new { decision = "Accepted" });

        var applied = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));
        var labels = product.GetProperty("claimFields").EnumerateArray().Select(field => field.GetProperty("label").GetString()).ToArray();
        Assert.Contains(labels, label => label == "Place of the loss" || label!.Contains("Where it happened", StringComparison.OrdinalIgnoreCase));
        Assert.True(labels.Length > 1);
    }

    [Fact]
    public async Task AWordingThatCannotBeReadIsRefusedWithAReason()
    {
        var (admin, productId, _) = await ArrangeAsync(attach: false);

        var noWording = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noWording.StatusCode);
        Assert.Contains("Attach a policy wording", await noWording.Content.ReadAsStringAsync());

        var missing = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts", new { knowledgeDocumentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task TheDraftAndTheAuditTrailNeverHoldTheWordingText()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        await Decide(admin, productId, draft, Item(draft, "Covered", "Fire"), new { decision = "Accepted" });

        var audit = await (await admin.GetAsync("/api/v1/audit/events")).Content.ReadAsStringAsync();

        Assert.Contains("product.wording.drafted", audit);
        Assert.Contains("product.wording.item.decided", audit);
        Assert.DoesNotContain("lightning or explosion", audit);
        Assert.DoesNotContain("ACME ASSURANCE", audit);
    }

    [Fact]
    public async Task OnlyProductManagersMayReadOrDecide()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var tenant = MockedApiFactory.NewTenant();
        foreach (var role in new[] { "ClaimsOfficer", "CustomerService", "Broker" })
        {
            using var other = factory.CreateScopedClient(tenant, role: role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/products/{productId}/wording-drafts")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}/apply", new { })).StatusCode);
        }
    }

    [Fact]
    public async Task AnotherWorkspaceCannotSeeOrUseADraft()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var draftId = draft.GetProperty("draftId").GetGuid();
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.BadRequest, (await stranger.GetAsync($"/api/v1/products/{productId}/wording-drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/products/{productId}/wording-drafts/{draftId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draftId}/apply", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draftId}/items/i1", new { decision = "Accepted" })).StatusCode);
    }

    [Fact]
    public async Task ADraftBelongsToItsProduct()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        var other = await admin.PostAsJsonAsync("/api/v1/products", new { name = "Other", productClass = "Medical", changeReason = "x" });
        var otherId = (await MockedApiFactory.ReadJsonAsync(other)).GetProperty("productId").GetGuid();

        var response = await admin.GetAsync($"/api/v1/products/{otherId}/wording-drafts/{draft.GetProperty("draftId").GetGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ADiscardedReadingCannotBeAppliedAndThePublishedProductIsUntouched()
    {
        var (admin, productId, _) = await ArrangeAsync();
        var draft = await DraftAsync(admin, productId);
        await Decide(admin, productId, draft, Item(draft, "Covered", "Fire"), new { decision = "Accepted" });
        var draftId = draft.GetProperty("draftId").GetGuid();

        var discarded = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draftId}/discard", new { });
        Assert.Equal("Discarded", (await MockedApiFactory.ReadJsonAsync(discarded)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/products/{productId}/wording-drafts/{draftId}/apply", new { })).StatusCode);
        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));
        Assert.Equal(0, product.GetProperty("coverageItems").GetArrayLength());
    }
}

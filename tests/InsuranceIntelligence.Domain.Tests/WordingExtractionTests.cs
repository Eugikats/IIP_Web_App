using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class WordingExtractionTests
{
    private const string Wording =
        "SECTION 1. WHAT IS COVERED 1.1 Accidental damage. We cover loss of the vehicle caused by collision. Page 2 "
        + "SECTION 3. MAKING A CLAIM 3.2 For an accident claim we need a police report.";

    private static readonly WordingExtractionContext Context = new(
        ["ClaimForm", "PoliceAbstract", "Invoice"],
        ["Theft"],
        [],
        ["Place of the loss"]);

    private static string Json(params string[] items) => "{\"items\":[" + string.Join(",", items) + "]}";

    [Fact]
    public void AQuoteFoundInTheWordingIsVerifiedWithItsClauseAndPage()
    {
        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"covered\",\"title\":\"Accidental damage\",\"detail\":\"d\",\"quote\":\"We cover loss of the vehicle caused by collision.\"}"),
            Wording,
            Context);

        var citation = Assert.Single(items).Citation;
        Assert.True(citation.Verified);
        Assert.Equal("1.1", citation.Clause);
        Assert.Null(citation.Page);
    }

    [Fact]
    public void ThePageComesFromTheLastPageMarkerBeforeTheQuote()
    {
        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"document\",\"title\":\"Police report\",\"quote\":\"we need a police report\",\"documentKey\":\"policeabstract\"}"),
            Wording,
            Context);

        var item = Assert.Single(items);
        Assert.Equal(2, item.Citation.Page);
        Assert.Equal("3.2", item.Citation.Clause);
        Assert.Equal("PoliceAbstract", item.DocumentKey);
        Assert.Null(item.Problem);
    }

    [Fact]
    public void FormFeedsCountPagesInExtractedPdfText()
    {
        var text = "first page text about nothing\fsecond page We cover fire damage to the insured vehicle.";

        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"covered\",\"title\":\"Fire\",\"quote\":\"We cover fire damage to the insured vehicle.\"}"),
            text,
            Context);

        Assert.Equal(2, Assert.Single(items).Citation.Page);
    }

    [Fact]
    public void AnInventedQuoteIsKeptButFlaggedAndCannotBeAccepted()
    {
        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"covered\",\"title\":\"Flood\",\"quote\":\"We cover all flood damage anywhere in the world.\"}"),
            Wording,
            Context);

        var item = Assert.Single(items);
        Assert.False(item.Citation.Verified);
        Assert.Contains("not found in the wording", item.Problem);
    }

    [Fact]
    public void ATooShortQuoteIsNotEnoughToVerify()
    {
        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"covered\",\"title\":\"Cover\",\"quote\":\"We cover\"}"),
            Wording,
            Context);

        Assert.False(Assert.Single(items).Citation.Verified);
    }

    [Fact]
    public void ADocumentWithoutAKnownTypeHasAProblemAndOneWithAKnownTypeDoesNot()
    {
        var items = WordingExtraction.Parse(
            Json(
                "{\"kind\":\"document\",\"title\":\"Log book\",\"quote\":\"we need a police report\",\"documentKey\":\"LogBook\"}",
                "{\"kind\":\"document\",\"title\":\"Nothing\",\"quote\":\"we need a police report\",\"documentKey\":\"\",\"claimTypes\":[\"Accident\"]}"),
            Wording,
            Context);

        Assert.Equal(2, items.Count);
        Assert.Contains("'LogBook'", items[0].Problem);
        Assert.Contains("No document type matches", items[1].Problem);
    }

    [Fact]
    public void WhatTheProductAlreadyHasIsNotedNotBlocked()
    {
        var items = WordingExtraction.Parse(
            Json(
                "{\"kind\":\"claim_type\",\"title\":\"theft\",\"quote\":\"We cover loss of the vehicle caused by collision.\"}",
                "{\"kind\":\"field\",\"title\":\"Place of the loss\",\"quote\":\"We cover loss of the vehicle caused by collision.\"}"),
            Wording,
            Context);

        Assert.All(items, item => Assert.NotNull(item.Note));
        Assert.All(items, item => Assert.Null(item.Problem));
    }

    [Fact]
    public void ASelectQuestionWithoutChoicesBecomesText()
    {
        var items = WordingExtraction.Parse(
            Json("{\"kind\":\"field\",\"title\":\"Damage level\",\"quote\":\"We cover loss of the vehicle caused by collision.\",\"fieldType\":\"Select\",\"options\":[\"Minor\"]}"),
            Wording,
            Context);

        var item = Assert.Single(items);
        Assert.Equal("Text", item.FieldType);
        Assert.Empty(item.Options);
    }

    [Fact]
    public void ModelOutputWrappedInProseOrFencesIsStillRead()
    {
        var items = WordingExtraction.Parse(
            "Here you go:\n```json\n" + Json("{\"kind\":\"exclusion\",\"title\":\"Collision\",\"quote\":\"caused by collision\"}") + "\n```",
            Wording,
            Context);

        Assert.Equal(WordingItemKind.Exclusion, Assert.Single(items).Kind);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"nothing\":[]}")]
    [InlineData("{\"items\":\"no\"}")]
    public void UnreadableOutputIsReportedNotGuessedAt(string output)
    {
        Assert.Throws<WordingExtractionException>(() => WordingExtraction.Parse(output, Wording, Context));
    }

    [Fact]
    public void ItemsWithoutATitleOrQuoteOrAKnownKindAreDropped_AndDuplicatesCollapse()
    {
        var quote = "\"quote\":\"We cover loss of the vehicle caused by collision.\"";
        var items = WordingExtraction.Parse(
            Json(
                "{\"kind\":\"covered\",\"title\":\"\"," + quote + "}",
                "{\"kind\":\"covered\",\"title\":\"No quote\"}",
                "{\"kind\":\"verdict\",\"title\":\"Pay it\"," + quote + "}",
                "{\"kind\":\"covered\",\"title\":\"Collision\"," + quote + "}",
                "{\"kind\":\"covered\",\"title\":\"collision\"," + quote + "}"),
            Wording,
            Context);

        Assert.Equal("Collision", Assert.Single(items).Title);
    }

    [Fact]
    public void ALongWordingIsCutAndSaidToBeCut()
    {
        var (text, truncated) = WordingExtraction.Prepare(new string('a', WordingExtraction.MaxCharacters + 10));

        Assert.True(truncated);
        Assert.Equal(WordingExtraction.MaxCharacters, text.Length);
        Assert.False(WordingExtraction.Prepare("short").Truncated);
    }

    [Fact]
    public void ThePromptCarriesTheRulesTheKeysAndTheWording()
    {
        var prompt = WordingExtraction.BuildPrompt("My wording", "THE TEXT", ["ClaimForm", "Invoice"]);

        Assert.Contains(WordingExtraction.PromptMarker, prompt);
        Assert.Contains("ClaimForm, Invoice", prompt);
        Assert.Contains("THE TEXT", prompt);
        Assert.Contains("exact words", prompt);
        Assert.DoesNotContain('—', prompt);
    }

    // ---- merging accepted items into the product ----

    private static readonly TenantScope Scope = TenantScope.Create("t", "w");

    private static ProductDto Product(IReadOnlyList<ClaimTypeDto>? claimTypes = null, IReadOnlyList<DocumentMatrixEntryDto>? matrix = null, IReadOnlyList<ClaimFieldDto>? fields = null, string productClass = "General") =>
        new(
            Guid.NewGuid(), 1, "P", productClass, null, null, null, "Draft", false,
            [new ClaimStageDto("acknowledgement_and_document_request", "Ack", "Claims Officer", false, 1, null, null)],
            matrix ?? [],
            "u", DateTimeOffset.UtcNow, "r", null, null, null, null, null, null, null,
            [],
            claimTypes ?? [],
            fields ?? [],
            []);

    private static WordingDraftItem Accepted(WordingItemKind kind, string title, Func<WordingDraftItem, WordingDraftItem>? shape = null)
    {
        var item = new WordingDraftItem
        {
            ItemId = "i" + title,
            Kind = kind,
            Title = title,
            Detail = "detail",
            Citation = new WordingCitation("quote words here", "1.1", 1, true),
            Decision = WordingItemDecision.Accepted,
        };
        return shape is null ? item : shape(item);
    }

    private static WordingDraft Draft(params WordingDraftItem[] items) =>
        new()
        {
            DraftId = Guid.NewGuid(), Scope = Scope, ProductId = Guid.NewGuid(), KnowledgeDocumentId = Guid.NewGuid(),
            DocumentName = "w", Status = WordingDraftStatus.InReview, Items = items, Provider = "Mock",
            CreatedByUserId = "u", CreatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void OnlyAcceptedItemsWithoutAProblemAreMerged()
    {
        var pending = Accepted(WordingItemKind.Covered, "Pending one") with { Decision = WordingItemDecision.Pending };
        var rejected = Accepted(WordingItemKind.Covered, "Rejected one") with { Decision = WordingItemDecision.Rejected };
        var blocked = Accepted(WordingItemKind.Covered, "Blocked one") with { Problem = "no" };
        var good = Accepted(WordingItemKind.Exclusion, "Good one");

        var (request, report) = WordingApplication.Merge(Product(), Draft(pending, rejected, blocked, good), "why");

        Assert.Equal(1, report.TotalAdded);
        var added = Assert.Single(request.CoverageItems!);
        Assert.Equal("Excluded", added.Kind);
        Assert.Null(request.ClaimTypes);
        Assert.Null(request.DocumentMatrix);
        Assert.Null(request.ClaimFields);
    }

    [Fact]
    public void ADocumentCanNameAClaimTypeAcceptedInTheSamePass_AndAnUnknownOneIsReported()
    {
        var claimType = Accepted(WordingItemKind.ClaimType, "Own damage");
        var good = Accepted(WordingItemKind.Document, "Police report", item => item with { DocumentKey = "PoliceAbstract", ClaimTypeNames = ["Own damage"] });
        var bad = Accepted(WordingItemKind.Document, "Invoice", item => item with { DocumentKey = "Invoice", ClaimTypeNames = ["Hail"] });

        var (request, report) = WordingApplication.Merge(Product(), Draft(claimType, good, bad), "why");

        Assert.Equal(1, report.ClaimTypesAdded);
        Assert.Equal(1, report.DocumentsAdded);
        Assert.Equal(["own-damage"], request.DocumentMatrix!.Single().ClaimTypeKeys!);
        Assert.Equal("acknowledgement_and_document_request", request.DocumentMatrix!.Single().RequiredAtStageKey);
        Assert.Contains("'Hail'", Assert.Single(report.Skipped).Reason);
    }

    [Fact]
    public void WhatTheProductAlreadyHasIsSkippedNotDuplicated()
    {
        var current = Product(
            [new ClaimTypeDto("theft", "Theft", "d", true, 1, [])],
            [new DocumentMatrixEntryDto("PoliceAbstract", "Police", "r", true, "acknowledgement_and_document_request", true, ["theft"])],
            [new ClaimFieldDto("placeOfTheLoss", "Place of the loss", "Text", true, [], null, 1, true, [])]);
        var items = new[]
        {
            Accepted(WordingItemKind.ClaimType, "theft"),
            Accepted(WordingItemKind.Document, "Police report", item => item with { DocumentKey = "PoliceAbstract", ClaimTypeNames = ["Theft"] }),
            Accepted(WordingItemKind.Field, "Place of the loss"),
        };

        var (_, report) = WordingApplication.Merge(current, Draft(items), "why");

        Assert.Equal(0, report.TotalAdded);
        Assert.Equal(3, report.Skipped.Count);
    }

    [Fact]
    public void AQuestionAddedToAProductWithNoneKeepsTheBuiltInQuestions()
    {
        var question = Accepted(WordingItemKind.Field, "Policy excess paid", item => item with { FieldType = "YesNo" });

        var (request, _) = WordingApplication.Merge(Product(productClass: "Motor"), Draft(question), "why");

        Assert.True(request.ClaimFields!.Count > 1);
        Assert.Contains(request.ClaimFields!, field => field.Label == "Policy excess paid" && field.Type == "YesNo");
    }
}

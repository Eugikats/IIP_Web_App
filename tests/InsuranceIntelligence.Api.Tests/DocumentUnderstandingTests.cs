using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Xunit;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentUnderstandingTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    private static ClaimCase Case(string line = "Motor") => new()
    {
        CaseId = Guid.NewGuid(), Scope = Scope, CustomerName = "Amina Okello", PolicyNumber = "POL-1",
        ProductLine = line, ClaimType = "Accident", Channel = "WebPortal", IncidentDate = new DateOnly(2026, 5, 24),
        IncidentDescription = "Collision", Status = CaseStatus.MissingDocuments, CompletenessScore = 0m,
        CreatedByUserId = "u", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "c",
    };

    private static CaseDocument Doc(ClaimCase claim, DocumentType type, string? key = null, Dictionary<string, string>? fields = null,
        Dictionary<string, DocumentFieldCorrection>? corrections = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new CaseDocument
        {
            DocumentId = Guid.NewGuid(), CaseId = claim.CaseId, Scope = claim.Scope, OriginalFileName = "x.pdf", ContentType = "application/pdf",
            FileSizeBytes = 10, ObjectKey = "k", DocumentType = type, DocumentTypeKey = key,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(type, 0.7m, "t", now),
            Extraction = new DocumentExtractionResult(fields ?? new Dictionary<string, string>(), new Dictionary<string, decimal>(), "t", now, false, null, null, corrections),
            ValidationWarnings = [], UploadedByUserId = "u", UploadedAt = now, CorrelationId = "c",
        };
    }

    private static StaticDocumentChecklistProvider Provider() => new(new CompletenessScoringService());

    [Theory]
    [InlineData("claim_form.pdf", DocumentType.ClaimForm)]
    [InlineData("Police Abstract.pdf", DocumentType.PoliceAbstract)]
    [InlineData("photo_front.jpg", DocumentType.PhotoEvidence)]
    [InlineData("driving_licence.jpg", DocumentType.CustomerId)]
    [InlineData("national_id.png", DocumentType.CustomerId)]
    [InlineData("IMG-20261027-WA0001.jpg", DocumentType.Unknown)]
    [InlineData("scan001.pdf", DocumentType.Unknown)]
    [InlineData("accident_report.jpg", DocumentType.Unknown)]
    [InlineData("provider_letter.pdf", DocumentType.Unknown)]
    [InlineData("video_01.pdf", DocumentType.Unknown)]
    public void File_names_are_matched_as_whole_words_never_as_pieces_of_a_word(string fileName, DocumentType expected)
    {
        Assert.Equal(expected, DocumentContentClassifier.Classify(fileName).Type);
    }

    [Fact]
    public void A_guess_from_the_file_name_alone_stays_below_the_human_verification_threshold()
    {
        var guess = DocumentContentClassifier.Classify("claim_form.pdf");
        Assert.True(guess.Confidence < 0.8m);
        Assert.Contains("file name", guess.Basis);
    }

    [Fact]
    public void The_printed_words_decide_and_beat_a_misleading_file_name()
    {
        Assert.Equal(DocumentType.Unknown, DocumentContentClassifier.Classify("scan001.pdf").Type);
        var police = DocumentContentClassifier.Classify("scan001.pdf", "POLICE ABSTRACT\nOB No 1234/26");
        Assert.Equal(DocumentType.PoliceAbstract, police.Type);
        Assert.True(police.Confidence >= 0.8m);
    }

    [Fact]
    public void A_claim_form_that_mentions_a_police_report_in_its_body_is_still_a_claim_form()
    {
        var text = "MOTOR ACCIDENT CLAIM FORM\nPolicy No\nHave you reported to the police? Police abstract number:\nDiagnosis, if injured:";
        Assert.Equal(DocumentType.ClaimForm, DocumentContentClassifier.Classify("scan.pdf", text).Type);
    }

    [Fact]
    public async Task A_customer_choosing_the_police_abstract_slot_counts_even_when_the_classifier_cannot_tell()
    {
        var claim = Case();
        var checklist = await Provider().BuildChecklistAsync(claim,
            [Doc(claim, DocumentType.ClaimForm), Doc(claim, DocumentType.PhotoEvidence), Doc(claim, DocumentType.Unknown, "PoliceAbstract")]);
        Assert.Empty(checklist.MissingRequiredDocuments);
        Assert.Equal(100m, checklist.CompletenessScore.Percentage);
    }

    [Fact]
    public async Task A_missing_required_document_is_named_and_lowers_the_score()
    {
        var claim = Case();
        var checklist = await Provider().BuildChecklistAsync(claim, [Doc(claim, DocumentType.ClaimForm), Doc(claim, DocumentType.PhotoEvidence)]);
        Assert.Equal(66.67m, checklist.CompletenessScore.Percentage);
        Assert.Equal(new[] { "Police abstract" }, checklist.MissingRequiredDocuments.Select(item => item.Label));
    }

    [Fact]
    public async Task A_claim_form_with_no_policy_number_or_date_reports_those_details_as_missing()
    {
        var claim = Case();
        var form = Doc(claim, DocumentType.ClaimForm, fields: new()
        {
            ["keyValue.Name_of_Insured"] = "Amina Okello",
            ["keyValue.Vehicle_Reg._No."] = "UBA 123X",
            ["keyValue.Brief_description"] = "Rear-ended at a junction",
        });
        var checklist = await Provider().BuildChecklistAsync(claim, [form]);
        var check = Assert.Single(checklist.DetailChecks);
        Assert.True(check.Readable);
        Assert.Equal(new[] { "Policy number", "Date of the accident or loss" }, check.Missing.Select(field => field.Label));
        Assert.Equal(3, checklist.FieldCompleteness.Found);
        Assert.Equal(5, checklist.FieldCompleteness.Total);
    }

    [Fact]
    public async Task A_person_correcting_a_field_fills_in_a_detail_the_reader_missed()
    {
        var claim = Case();
        var form = Doc(claim, DocumentType.ClaimForm,
            fields: new() { ["keyValue.Name_of_Insured"] = "Amina Okello" },
            corrections: new() { ["keyValue.Policy_No"] = new("keyValue.Policy_No", "POL-1", "officer", DateTimeOffset.UtcNow, "c") });
        var check = (await Provider().BuildChecklistAsync(claim, [form])).DetailChecks.Single();
        Assert.DoesNotContain(check.Missing, field => field.Key == "policy_number");
    }

    [Fact]
    public async Task A_document_nothing_could_be_read_from_is_not_reported_as_missing_every_detail()
    {
        var claim = Case();
        var scan = Doc(claim, DocumentType.ClaimForm, fields: new() { ["sourceFileName"] = "scan.pdf", ["objectKey"] = "k", ["pageCount"] = "1" });
        var checklist = await Provider().BuildChecklistAsync(claim, [scan]);
        var check = Assert.Single(checklist.DetailChecks);
        Assert.False(check.Readable);
        Assert.Empty(check.Missing);
        Assert.Equal(new[] { "Claim form" }, checklist.UnreadDocuments.Select(item => item.DocumentLabel));
        Assert.Equal(0, checklist.FieldCompleteness.Total);
    }

    [Fact]
    public async Task Field_names_are_matched_as_words_so_unrelated_keys_never_count()
    {
        var claim = Case();
        // "objectKey" contains the letters "ob"; it is not an OB number. Only field. and keyValue. keys are read at all.
        var abstractDoc = Doc(claim, DocumentType.PoliceAbstract, fields: new() { ["objectKey"] = "abc", ["keyValue.Station"] = "Kampala Central" });
        var check = (await Provider().BuildChecklistAsync(claim, [abstractDoc])).DetailChecks.Single();
        Assert.Contains(check.Missing, field => field.Key == "reference");
        Assert.DoesNotContain(check.Missing, field => field.Key == "station");
    }

    [Fact]
    public async Task A_photo_has_no_required_details()
    {
        var claim = Case();
        var photo = Doc(claim, DocumentType.PhotoEvidence, fields: new() { ["pageCount"] = "1" });
        var check = (await Provider().BuildChecklistAsync(claim, [photo])).DetailChecks.Single();
        Assert.True(check.Readable);
        Assert.Empty(check.Missing);
    }
}

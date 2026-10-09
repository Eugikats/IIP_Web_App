using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Application.Knowledge;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Tests;

public sealed class KnowledgeChunkerTests
{
    private const string Wording = """
        MOTOR COMPREHENSIVE POLICY

        1. What is covered
        We pay for loss of or damage to your car caused by accident, fire or theft. We also pay for damage to another person's property up to the limit on your schedule.

        2. What is not covered
        We do not pay for damage when the driver is under the influence of alcohol. We do not pay for wear and tear. We do not pay if the car is used for hire without our written agreement.

        3. Making a claim
        Tell us within 7 days. Send the police abstract and your driving permit.
        """;

    [Fact]
    public void Each_piece_remembers_the_heading_it_sits_under()
    {
        var chunks = KnowledgeChunker.Chunk(Wording);

        Assert.Contains(chunks, c => c.Section == "1. What is covered" && c.Content.Contains("accident, fire or theft"));
        Assert.Contains(chunks, c => c.Section == "2. What is not covered" && c.Content.Contains("under the influence of alcohol"));
        Assert.Contains(chunks, c => c.Section == "3. Making a claim" && c.Content.Contains("police abstract"));
    }

    [Fact]
    public void A_piece_never_mixes_two_sections()
    {
        var chunks = KnowledgeChunker.Chunk(Wording);

        Assert.DoesNotContain(chunks, c => c.Content.Contains("accident, fire or theft") && c.Content.Contains("under the influence"));
        Assert.DoesNotContain(chunks, c => c.Content.Contains("wear and tear") && c.Content.Contains("police abstract"));
    }

    [Fact]
    public void Headings_are_not_kept_as_text_of_their_own_and_blank_input_gives_nothing()
    {
        Assert.DoesNotContain(KnowledgeChunker.Chunk(Wording), c => c.Content == "1. What is covered");
        Assert.Empty(KnowledgeChunker.Chunk(null));
        Assert.Empty(KnowledgeChunker.Chunk("   \n  \r\n "));
    }

    [Fact]
    public void A_long_section_is_cut_between_sentences_never_through_a_word_and_carries_the_last_sentence_forward()
    {
        var sentences = Enumerable.Range(1, 60).Select(i => $"Clause {i} says that the insurer will review item number {i} within fourteen working days.").ToArray();
        var text = "EXCLUSIONS\n\n" + string.Join(' ', sentences);

        var chunks = KnowledgeChunker.Chunk(text);

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c =>
        {
            Assert.Equal("EXCLUSIONS", c.Section);
            Assert.True(c.Content.Length <= KnowledgeChunker.MaxLength + 250, $"piece too long: {c.Content.Length}");
            Assert.EndsWith(".", c.Content);
            Assert.StartsWith("Clause", c.Content);
        });
        // The sentence that ends one piece also opens the next, so a rule across the boundary is found whole.
        for (var i = 1; i < chunks.Count; i++)
        {
            var lastOfPrevious = chunks[i - 1].Content[(chunks[i - 1].Content.LastIndexOf("Clause", StringComparison.Ordinal))..];
            Assert.StartsWith(lastOfPrevious, chunks[i].Content);
        }
    }

    [Fact]
    public void A_single_enormous_paragraph_with_no_full_stops_is_still_cut_at_spaces()
    {
        var text = string.Join(' ', Enumerable.Range(1, 800).Select(i => $"word{i}"));

        var chunks = KnowledgeChunker.Chunk(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Content.Length <= KnowledgeChunker.MaxLength + 1));
        Assert.All(chunks, c => Assert.Matches(@"^word\d+( word\d+)*$", c.Content));
    }

    [Theory]
    [InlineData("EXCLUSIONS", true)]
    [InlineData("2. What is not covered", true)]
    [InlineData("Making a claim:", true)]
    [InlineData("We do not pay for wear and tear.", false)]
    [InlineData("Tell us within 7 days", false)]
    [InlineData("", false)]
    public void Headings_are_told_from_sentences(string line, bool expected)
    {
        Assert.Equal(expected, KnowledgeChunker.IsHeading(line));
    }

    [Fact]
    public void Only_wording_in_force_today_answers_and_the_filter_stays_inside_the_tenant()
    {
        var scope = TenantScope.Create("tenant-a", "workspace-1");

        var filter = AzureAiSearchKnowledgeIndexProvider.BuildSearchFilter(scope, "Motor", new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("tenantId eq 'tenant-a'", filter);
        Assert.Contains("productLine eq 'Motor'", filter);
        Assert.Contains("(effectiveDate eq null or effectiveDate le '2026-10-09')", filter);
        Assert.Contains("(expiryDate eq null or expiryDate ge '2026-10-09')", filter);
    }

    [Fact]
    public void Removing_a_document_finds_its_pieces_by_id_inside_the_tenant()
    {
        var id = Guid.NewGuid();

        var filter = AzureAiSearchKnowledgeIndexProvider.BuildDocumentFilter(TenantScope.Create("tenant-a", "workspace-1"), id);

        Assert.Equal($"tenantId eq 'tenant-a' and workspaceId eq 'workspace-1' and knowledgeDocId eq '{id}'", filter);
    }
}

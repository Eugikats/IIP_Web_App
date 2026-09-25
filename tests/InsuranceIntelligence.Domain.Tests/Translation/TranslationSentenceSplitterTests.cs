using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class TranslationSentenceSplitterTests
{
    [Fact]
    public void Short_text_is_not_split()
    {
        var result = TranslationSentenceSplitter.Split("Nina bima.");

        Assert.Equal(["Nina bima."], result);
    }

    [Fact]
    public void Long_text_is_split_into_sentences()
    {
        var text = "I had an accident yesterday on Jinja Road, my car was hit from behind. " +
                    "Please help me start a claim as soon as possible.";

        var result = TranslationSentenceSplitter.Split(text);

        Assert.Equal(2, result.Count);
        Assert.StartsWith("I had an accident", result[0]);
        Assert.StartsWith("Please help me", result[1]);
    }

    [Fact]
    public void Long_text_with_no_sentence_boundary_is_kept_whole()
    {
        var text = new string('a', 100);

        var result = TranslationSentenceSplitter.Split(text);

        Assert.Equal([text], result);
    }
}

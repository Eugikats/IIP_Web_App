using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>One lesson found for a question, with the part of it worth showing the model.</summary>
public sealed record ZenithHit(string CourseId, string CourseTitle, string LessonId, string LessonTitle, string Excerpt, double Score);

/// <summary>
/// Finds the lessons that speak to a question, so Zenith can answer from them and point at them. Plain keyword search with
/// inverse-document-frequency weighting over the Academy's own lesson text: it needs no model and no extra service, and a
/// question the lessons do not cover finds nothing, which is the signal not to ask a model at all.
/// </summary>
public sealed partial class ZenithIndex
{
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "but", "not", "you", "your", "with", "this", "that", "from", "have", "has", "had", "was", "were", "will", "can",
        "could", "would", "should", "what", "which", "when", "where", "who", "whom", "how", "why", "does", "did", "doing", "into", "about", "than",
        "then", "them", "they", "their", "there", "these", "those", "its", "also", "any", "all", "some", "more", "most", "such", "only", "own", "same",
        "too", "very", "just", "out", "off", "over", "under", "again", "once", "here", "both", "each", "few", "other", "tell", "explain", "please",
        "give", "show", "mean", "means", "meant", "work", "works", "make", "makes", "use", "used", "using", "need", "needs", "get", "gets", "let",
        "say", "says", "know", "want", "like", "much", "many", "being", "been", "because", "while", "between", "during", "before", "after",
    };

    private sealed record Doc(string CourseId, string CourseTitle, string LessonId, string LessonTitle, Dictionary<string, int> Terms, IReadOnlyList<string> Blocks);

    private readonly List<Doc> docs = [];
    private readonly Dictionary<string, int> documentFrequency = new(StringComparer.Ordinal);

    public ZenithIndex(AcademyCatalogue catalogue)
    {
        foreach (var course in catalogue.Courses.Where(item => item.IsReady))
        {
            foreach (var lesson in course.AllLessons)
            {
                var blocks = lesson.Blocks.Select(BlockText).Where(text => text.Length > 0).ToList();
                var terms = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var token in Tokens(lesson.Title)) terms[token] = terms.GetValueOrDefault(token) + 3;
                foreach (var token in Tokens(lesson.Outcome)) terms[token] = terms.GetValueOrDefault(token) + 2;
                foreach (var block in blocks) foreach (var token in Tokens(block)) terms[token] = terms.GetValueOrDefault(token) + 1;
                docs.Add(new Doc(course.Id, course.Title, lesson.Id, lesson.Title, terms, blocks));
                foreach (var token in terms.Keys) documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
            }
        }
    }

    public int Count => docs.Count;

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word();

    public static IReadOnlyList<string> Tokens(string? text)
    {
        var result = new List<string>();
        foreach (Match match in Word().Matches((text ?? string.Empty).ToLowerInvariant()))
        {
            var word = match.Value;
            if (word.Length < 3 || Stop.Contains(word))
            {
                continue;
            }

            if (word.Length > 4 && word.EndsWith('s') && !word.EndsWith("ss"))
            {
                word = word[..^1];
            }

            result.Add(word);
        }

        return result;
    }

    private static string BlockText(AcademyBlock block)
    {
        var parts = new List<string>();
        if (block.Text is not null) parts.Add(block.Text);
        if (block.Title is not null) parts.Add(block.Title);
        if (block.Note is not null) parts.Add(block.Note);
        if (block.Caption is not null) parts.Add(block.Caption);
        if (block.Items is not null) parts.AddRange(block.Items);
        if (block.Steps is not null) parts.AddRange(block.Steps);
        if (block.Callouts is not null) parts.AddRange(block.Callouts);
        if (block.Head is not null) parts.Add(string.Join(" | ", block.Head));
        if (block.Rows is not null) parts.AddRange(block.Rows.Select(row => string.Join(" | ", row)));
        return string.Join('\n', parts).Trim();
    }

    /// <summary>The best lessons among the allowed courses, or none when the question is not covered.</summary>
    public IReadOnlyList<ZenithHit> Search(string question, IReadOnlySet<string> courseIds, int take = 3)
    {
        var query = Tokens(question).Distinct().ToList();
        if (query.Count == 0)
        {
            return [];
        }

        var needed = Math.Min(2, query.Count);
        var hits = new List<(Doc Doc, double Score)>();
        foreach (var doc in docs.Where(item => courseIds.Contains(item.CourseId)))
        {
            var score = 0.0;
            var matched = 0;
            foreach (var term in query)
            {
                if (!doc.Terms.TryGetValue(term, out var frequency))
                {
                    continue;
                }

                matched++;
                score += (1 + Math.Log(frequency)) * Math.Log(1 + (double)docs.Count / documentFrequency[term]);
            }

            if (matched >= needed)
            {
                hits.Add((doc, score));
            }
        }

        return hits
            .OrderByDescending(item => item.Score)
            .Take(take)
            .Select(item => new ZenithHit(item.Doc.CourseId, item.Doc.CourseTitle, item.Doc.LessonId, item.Doc.LessonTitle, Excerpt(item.Doc, query), item.Score))
            .ToList();
    }

    private static string Excerpt(Doc doc, IReadOnlyList<string> query)
    {
        var scored = doc.Blocks
            .Select((text, index) => (Text: text, Index: index, Score: Tokens(text).Count(token => query.Contains(token))))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Take(3)
            .OrderBy(item => item.Index)
            .Select(item => item.Text);
        var joined = string.Join('\n', scored);
        return joined.Length <= 1500 ? joined : joined[..1500];
    }
}

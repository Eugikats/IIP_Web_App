namespace InsuranceIntelligence.Domain.Academy;

/// <summary>
/// The Academy's courses as the API serves them, built by <c>web/portal/scripts/build-academy-content.mjs</c> from the
/// supplied course content. Questions carry their answers here, on the server only: what is sent to a browser before a
/// person submits an assessment never includes them.
/// </summary>
public sealed record AcademyCatalogue(IReadOnlyList<AcademyCourse> Courses);

public sealed record AcademyCourse
{
    public required string Id { get; init; }
    public int Number { get; init; }
    public string Category { get; init; } = string.Empty;
    public required string Title { get; init; }
    public string Short { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string? Level { get; init; }
    public IReadOnlyList<string> Hue { get; init; } = [];
    public string Icon { get; init; } = "book";

    /// <summary>"ready" can be taken now; "coming_soon" is listed with its outline and cannot be started.</summary>
    public string Status { get; init; } = "ready";

    public IReadOnlyList<string> Outcomes { get; init; } = [];
    public IReadOnlyList<string> Who { get; init; } = [];
    public IReadOnlyList<string> IipAreas { get; init; } = [];
    public IReadOnlyList<AcademyModule> Modules { get; init; } = [];
    public IReadOnlyList<AcademyOutlineItem> Outline { get; init; } = [];
    public AcademyAssessment? Check { get; init; }
    public AcademyFinalTask? FinalTask { get; init; }
    public string Requirements { get; init; } = string.Empty;
    public IReadOnlyList<AcademySource> Sources { get; init; } = [];
    public string? ContentStatus { get; init; }

    public bool IsReady => string.Equals(Status, "ready", StringComparison.OrdinalIgnoreCase) && Modules.Count > 0;

    public IEnumerable<AcademyLesson> AllLessons => Modules.SelectMany(module => module.Lessons);
}

public sealed record AcademyModule
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<AcademyLesson> Lessons { get; init; } = [];
    public AcademyAssessment? Assessment { get; init; }
}

public sealed record AcademyLesson
{
    public required string Id { get; init; }
    public string? Number { get; init; }
    public required string Title { get; init; }
    public int Minutes { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public IReadOnlyList<AcademyBlock> Blocks { get; init; } = [];
    public IReadOnlyList<string> Sources { get; init; } = [];
    public string? TryIt { get; init; }

    /// <summary>A recording, when there is one. None has been produced yet, so this is empty and the page says so.</summary>
    public string? Video { get; init; }

    public AcademyAssessment? Quiz { get; init; }
}

/// <summary>One piece of a lesson. Which fields are used depends on <see cref="Type"/>.</summary>
public sealed record AcademyBlock
{
    public required string Type { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<string>? Items { get; init; }
    public bool? Ordered { get; init; }
    public IReadOnlyList<string>? Head { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Rows { get; init; }
    public IReadOnlyList<string>? Steps { get; init; }
    public string? Note { get; init; }
    public string? File { get; init; }
    public string? Caption { get; init; }
    public IReadOnlyList<string>? Callouts { get; init; }
    public string? Title { get; init; }
}

public sealed record AcademyAssessment
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int PassMark { get; init; } = 70;

    /// <summary>0 means unlimited.</summary>
    public int MaxAttempts { get; init; }

    public IReadOnlyList<AcademyQuestion> Questions { get; init; } = [];
}

public sealed record AcademyQuestion
{
    public required string Id { get; init; }
    public required string Q { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
    public int Answer { get; init; }
    public string Why { get; init; } = string.Empty;
}

public sealed record AcademyRubricRow(string Criterion, int Marks, string Distinction);

public sealed record AcademyFinalTask
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Brief { get; init; } = string.Empty;
    public string Deliverables { get; init; } = string.Empty;
    public IReadOnlyList<AcademyRubricRow> Rubric { get; init; } = [];
    public int PassMark { get; init; } = 60;
    public int OutOf { get; init; } = 100;
    public string Integrity { get; init; } = string.Empty;
    public string Requirement { get; init; } = string.Empty;
    public IReadOnlyList<string> CanvasBoxes { get; init; } = [];
    public int MinWords { get; init; }
    public int MaxWords { get; init; }
}

public sealed record AcademyOutlineItem(string? Module, string Unit, string Outcome);

public sealed record AcademySource(string Id, string Title, string Type, string Date, string Status);

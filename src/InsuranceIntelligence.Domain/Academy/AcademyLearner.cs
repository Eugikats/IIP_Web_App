namespace InsuranceIntelligence.Domain.Academy;

/// <summary>
/// One stored Academy record. All of the Academy's learner and community data is kept as records of a kind (enrolment,
/// progress, certificate, post and so on) owned by a person, in the Academy's own table, apart from every insurer's data.
/// </summary>
public sealed record AcademyRecord(string Kind, string Owner, string Key, string Json, DateTimeOffset UpdatedAt);

public interface IAcademyRecordStore
{
    Task<AcademyRecord?> GetAsync(string kind, string owner, string key, CancellationToken cancellationToken = default);

    Task SaveAsync(string kind, string owner, string key, string json, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademyRecord>> ListByOwnerAsync(string kind, string owner, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<AcademyRecord>> ListByKindAsync(string kind, int take, CancellationToken cancellationToken = default);

    Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default);

    Task<int> CountAsync(string kind, CancellationToken cancellationToken = default);
}

public static class AcademyKinds
{
    public const string Enrolment = "enrolment";
    public const string Progress = "progress";
    public const string Certificate = "certificate";
    public const string Notification = "notification";
    public const string Profile = "profile";
    public const string Minutes = "minutes";
    public const string Activity = "activity";
    public const string Post = "post";
    public const string Group = "group";
    public const string Membership = "membership";
    public const string Event = "event";
    public const string Rsvp = "rsvp";
    public const string Consent = "consent";
    public const string Report = "report";
    public const string ZenithUse = "zenith-use";
    public const string Setting = "setting";
    public const string CertificateCode = "certificate-code";
}

public sealed record AcademyEnrolment(string CourseId, DateTimeOffset EnrolledAt, string EnrolledBy);

public sealed record AcademyAssessmentRecord(int Attempts, int Best, bool Passed, DateTimeOffset? LastAt);

public sealed record AcademyFinalSubmission
{
    public string Status { get; init; } = "none";
    public IReadOnlyList<string> Canvas { get; init; } = [];
    public string Proposal { get; init; } = string.Empty;
    public DateTimeOffset? SubmittedAt { get; init; }
    public int? Score { get; init; }
    public IReadOnlyList<AcademyRubricScore> Scores { get; init; } = [];
    public string? Feedback { get; init; }
    public string? MarkedBy { get; init; }
    public DateTimeOffset? MarkedAt { get; init; }
}

public sealed record AcademyRubricScore(string Criterion, int Marks, int OutOf);

public sealed record AcademyProgress
{
    public required string CourseId { get; init; }
    public IReadOnlyList<string> DoneLessons { get; init; } = [];
    public IReadOnlyList<string> PassedQuizzes { get; init; } = [];
    public IReadOnlyDictionary<string, AcademyAssessmentRecord> Assessments { get; init; } = new Dictionary<string, AcademyAssessmentRecord>();
    public bool CheckPassed { get; init; }
    public AcademyFinalSubmission FinalTask { get; init; } = new();
    public string? LastLessonId { get; init; }
    public IReadOnlyDictionary<string, string> Notes { get; init; } = new Dictionary<string, string>();
}

public sealed record AcademyCertificate(string Id, string Code, string UserId, string CourseId, string CourseTitle, string Name, DateTimeOffset IssuedAt);

public sealed record AcademyNotification(string Id, string UserId, string Text, string Href, string Icon, bool Read, DateTimeOffset CreatedAt);

public sealed record AcademyProfile
{
    public string Name { get; init; } = string.Empty;

    /// <summary>From the sign-in, never edited here. Lets an operator find a person to enrol.</summary>
    public string Email { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string Organisation { get; init; } = string.Empty;
    public string About { get; init; } = string.Empty;
    public string Language { get; init; } = "English";
    public string Goal { get; init; } = "30";
    public bool EmailNotifications { get; init; } = true;
    public bool WeeklySummary { get; init; } = true;
    public bool Replies { get; init; } = true;
    public bool AiSuggestions { get; init; } = true;
    public bool CommunityConsent { get; init; }
    public DateTimeOffset? CommunityConsentAt { get; init; }
}

/// <summary>Minutes of learning per day, for the last two weeks, keyed yyyy-MM-dd.</summary>
public sealed record AcademyMinutes(IReadOnlyDictionary<string, int> Days);

public sealed record AcademyActivityEntry(string Text, DateTimeOffset At);

public sealed record AcademyActivity(IReadOnlyList<AcademyActivityEntry> Entries);

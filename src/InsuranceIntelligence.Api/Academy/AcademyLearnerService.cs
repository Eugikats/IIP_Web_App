using System.Security.Cryptography;
using System.Text.Json;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>A refusal the learner can act on. The status is what the endpoint answers with.</summary>
public sealed class AcademyRuleException(int status, string message, string? code = null) : Exception(message)
{
    public int Status { get; } = status;

    public string? Code { get; } = code;
}

/// <summary>
/// Everything a learner does: enrol, read lessons, take quizzes, submit the final task, earn a certificate. All rules live
/// here and run on the server; the browser only asks. Questions leave this class without their answers, and a
/// certificate is issued only by <see cref="TryIssueCertificateAsync"/> once every requirement is met.
/// </summary>
public sealed class AcademyLearnerService(IAcademyRecordStore store, IAcademyContentProvider content, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxNoteLength = 4000;
    private const int MaxActivity = 20;
    private const int KeepDays = 60;

    private static string Owner(string userId) => userId.Trim().ToLowerInvariant();

    private async Task<T?> ReadAsync<T>(string kind, string owner, string key, CancellationToken ct) where T : class
    {
        var record = await store.GetAsync(kind, owner, key, ct);
        return record is null ? null : JsonSerializer.Deserialize<T>(record.Json, Json);
    }

    private Task WriteAsync<T>(string kind, string owner, string key, T value, CancellationToken ct) =>
        store.SaveAsync(kind, owner, key, JsonSerializer.Serialize(value, Json), ct);

    private AcademyCourse ReadyCourse(string courseId)
    {
        var course = content.Course(courseId) ?? throw new AcademyRuleException(404, "That course was not found.");
        return course.IsReady ? course : throw new AcademyRuleException(409, "That course is coming soon.", "coming_soon");
    }

    // ---------------------------------------------------------------- profile

    public async Task<AcademyProfile> ProfileAsync(AcademyLearner learner, CancellationToken ct)
    {
        var owner = Owner(learner.UserId);
        var profile = await ReadAsync<AcademyProfile>(AcademyKinds.Profile, owner, "profile", ct);
        if (profile is null || profile.Email != (learner.Email ?? string.Empty))
        {
            profile = (profile ?? new AcademyProfile { Name = learner.Name }) with { Email = learner.Email ?? string.Empty };
            await WriteAsync(AcademyKinds.Profile, owner, "profile", profile, ct);
        }

        return profile;
    }

    public async Task<AcademyProfile> UpdateProfileAsync(AcademyLearner learner, ProfileUpdate update, CancellationToken ct)
    {
        var current = await ProfileAsync(learner, ct);
        var name = (update.Name ?? current.Name).Trim();
        if (name.Length is < 2 or > 80)
        {
            throw new AcademyRuleException(400, "Enter your name, between 2 and 80 characters.");
        }

        var goal = update.Goal ?? current.Goal;
        if (goal is not ("15" or "30" or "60" or "90"))
        {
            throw new AcademyRuleException(400, "Choose a daily goal of 15, 30, 60 or 90 minutes.");
        }

        var updated = current with
        {
            Name = name,
            JobTitle = Clip(update.JobTitle ?? current.JobTitle, 100),
            Organisation = Clip(update.Organisation ?? current.Organisation, 100),
            About = Clip(update.About ?? current.About, 600),
            Language = Clip(update.Language ?? current.Language, 40),
            Goal = goal,
            EmailNotifications = update.EmailNotifications ?? current.EmailNotifications,
            WeeklySummary = update.WeeklySummary ?? current.WeeklySummary,
            Replies = update.Replies ?? current.Replies,
            AiSuggestions = update.AiSuggestions ?? current.AiSuggestions,
        };
        await WriteAsync(AcademyKinds.Profile, Owner(learner.UserId), "profile", updated, ct);
        return updated;
    }

    private static string Clip(string value, int length)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }

    // ---------------------------------------------------------------- the learner's whole state

    public async Task<object> StateAsync(AcademyLearner learner, CancellationToken ct)
    {
        var owner = Owner(learner.UserId);
        var profile = await ProfileAsync(learner, ct);
        var enrolments = await store.ListByOwnerAsync(AcademyKinds.Enrolment, owner, ct);
        var progress = await store.ListByOwnerAsync(AcademyKinds.Progress, owner, ct);
        var certificates = await store.ListByOwnerAsync(AcademyKinds.Certificate, owner, ct);
        var notifications = await store.ListByOwnerAsync(AcademyKinds.Notification, owner, ct);
        var minutes = await ReadAsync<AcademyMinutes>(AcademyKinds.Minutes, owner, "minutes", ct);
        var activity = await ReadAsync<AcademyActivity>(AcademyKinds.Activity, owner, "activity", ct);

        var progressByCourse = progress
            .Select(record => JsonSerializer.Deserialize<AcademyProgress>(record.Json, Json))
            .Where(item => item is not null)
            .ToDictionary(item => item!.CourseId, item => ProgressView(item!), StringComparer.OrdinalIgnoreCase);

        return new
        {
            profile = ProfileView(profile),
            enrolled = enrolments
                .Select(record => JsonSerializer.Deserialize<AcademyEnrolment>(record.Json, Json)!)
                .OrderBy(item => item.EnrolledAt)
                .Select(item => new { courseId = item.CourseId, enrolledAt = item.EnrolledAt }),
            progress = progressByCourse,
            certificates = certificates
                .Select(record => JsonSerializer.Deserialize<AcademyCertificate>(record.Json, Json)!)
                .OrderByDescending(item => item.IssuedAt)
                .Select(item => new { id = item.Id, code = item.Code, courseId = item.CourseId, courseTitle = item.CourseTitle, name = item.Name, issuedAt = item.IssuedAt }),
            notifications = notifications
                .Select(record => JsonSerializer.Deserialize<AcademyNotification>(record.Json, Json)!)
                .OrderByDescending(item => item.CreatedAt)
                .Take(30)
                .Select(item => new { id = item.Id, text = item.Text, href = item.Href, icon = item.Icon, read = item.Read, createdAt = item.CreatedAt }),
            stats = Stats(minutes),
            activity = (activity?.Entries ?? []).Select(entry => new { text = entry.Text, at = entry.At }),
            groups = Array.Empty<string>(),
        };
    }

    private static object ProfileView(AcademyProfile profile) => new
    {
        name = profile.Name,
        jobTitle = profile.JobTitle,
        organisation = profile.Organisation,
        about = profile.About,
        language = profile.Language,
        goal = profile.Goal,
        emailNotifications = profile.EmailNotifications,
        weeklySummary = profile.WeeklySummary,
        replies = profile.Replies,
        aiSuggestions = profile.AiSuggestions,
        communityConsent = profile.CommunityConsent,
    };

    private static object ProgressView(AcademyProgress progress) => new
    {
        doneLessons = progress.DoneLessons,
        passedQuizzes = progress.PassedQuizzes,
        lastLessonId = progress.LastLessonId,
        assessments = progress.Assessments.ToDictionary(pair => pair.Key, pair => new { attempts = pair.Value.Attempts, best = pair.Value.Best, passed = pair.Value.Passed }),
        checkPassed = progress.CheckPassed,
        finalTask = new
        {
            status = progress.FinalTask.Status is "submitted" or "passed" or "not_passed" ? progress.FinalTask.Status : "none",
            score = progress.FinalTask.Score,
            feedback = progress.FinalTask.Feedback,
            submittedAt = progress.FinalTask.SubmittedAt,
        },
    };

    private object Stats(AcademyMinutes? minutes)
    {
        var today = Today();
        var days = minutes?.Days ?? new Dictionary<string, int>();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var week = Enumerable.Range(0, 7).Select(offset => days.TryGetValue(Key(monday.AddDays(offset)), out var value) ? value : 0).ToArray();
        var cursor = days.GetValueOrDefault(Key(today)) > 0 ? today : today.AddDays(-1);
        var streak = 0;
        while (days.GetValueOrDefault(Key(cursor)) > 0)
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return new { streakDays = streak, minutesWeek = week, todayIndex = ((int)today.DayOfWeek + 6) % 7, minutesToday = days.GetValueOrDefault(Key(today)) };
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static string Key(DateOnly date) => date.ToString("yyyy-MM-dd");

    // ---------------------------------------------------------------- enrolment

    public async Task<bool> IsEnrolledAsync(string userId, string courseId, CancellationToken ct) =>
        await store.GetAsync(AcademyKinds.Enrolment, Owner(userId), courseId.ToLowerInvariant(), ct) is not null;

    /// <summary>Enrols a person. Payments are not connected, so enrolment is open: nothing is charged.</summary>
    public async Task<bool> EnrolAsync(string userId, string courseId, string enrolledBy, string? personName, CancellationToken ct)
    {
        var course = ReadyCourse(courseId);
        var owner = Owner(userId);
        if (await IsEnrolledAsync(userId, course.Id, ct))
        {
            return false;
        }

        await WriteAsync(AcademyKinds.Enrolment, owner, course.Id.ToLowerInvariant(), new AcademyEnrolment(course.Id, clock.GetUtcNow(), enrolledBy), ct);
        await WriteAsync(AcademyKinds.Progress, owner, course.Id.ToLowerInvariant(), new AcademyProgress { CourseId = course.Id }, ct);
        await LogAsync(owner, $"Enrolled in {course.Title}", ct);
        await NotifyAsync(owner, $"You are enrolled in {course.Title}. Start with the first lesson.", $"/academy/learn/{course.Id}", "book", ct);
        return true;
    }

    private async Task<AcademyProgress> ProgressAsync(string userId, string courseId, CancellationToken ct) =>
        await ReadAsync<AcademyProgress>(AcademyKinds.Progress, Owner(userId), courseId.ToLowerInvariant(), ct)
        ?? new AcademyProgress { CourseId = courseId };

    private Task SaveProgressAsync(string userId, AcademyProgress progress, CancellationToken ct) =>
        WriteAsync(AcademyKinds.Progress, Owner(userId), progress.CourseId.ToLowerInvariant(), progress, ct);

    private async Task<AcademyCourse> EnrolledCourseAsync(string userId, string courseId, CancellationToken ct)
    {
        var course = ReadyCourse(courseId);
        if (!await IsEnrolledAsync(userId, course.Id, ct))
        {
            throw new AcademyRuleException(403, "Enrol in this course to open its lessons.", "not_enrolled");
        }

        return course;
    }

    // ---------------------------------------------------------------- lessons

    public async Task<object> LessonAsync(string userId, string courseId, string lessonId, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(userId, courseId, ct);
        var (module, lesson) = FindLesson(course, lessonId);
        var progress = await ProgressAsync(userId, course.Id, ct);
        var moduleIndex = course.Modules.ToList().IndexOf(module);
        progress = progress with { LastLessonId = lesson.Id };
        await SaveProgressAsync(userId, progress, ct);

        return new
        {
            id = lesson.Id,
            courseId = course.Id,
            moduleId = module.Id,
            moduleTitle = module.Title,
            moduleNumber = moduleIndex + 1,
            indexInModule = module.Lessons.ToList().IndexOf(lesson) + 1,
            lessonsInModule = module.Lessons.Count,
            number = lesson.Number,
            title = lesson.Title,
            minutes = lesson.Minutes,
            outcome = lesson.Outcome,
            blocks = lesson.Blocks.Select(BlockView),
            sources = lesson.Sources,
            tryIt = lesson.TryIt,
            video = lesson.Video,
            note = progress.Notes.GetValueOrDefault(lesson.Id, string.Empty),
            hasQuiz = lesson.Quiz is not null,
            done = progress.DoneLessons.Contains(lesson.Id),
        };
    }

    private static object BlockView(AcademyBlock block) => new
    {
        type = block.Type,
        text = block.Text ?? string.Empty,
        items = block.Items ?? [],
        ordered = block.Ordered ?? false,
        head = block.Head ?? [],
        rows = block.Rows ?? [],
        steps = block.Steps ?? [],
        note = block.Note,
        file = block.File ?? string.Empty,
        caption = block.Caption ?? string.Empty,
        callouts = block.Callouts ?? [],
        title = block.Title ?? string.Empty,
    };

    private static (AcademyModule Module, AcademyLesson Lesson) FindLesson(AcademyCourse course, string lessonId)
    {
        foreach (var module in course.Modules)
        {
            var lesson = module.Lessons.FirstOrDefault(item => string.Equals(item.Id, lessonId, StringComparison.OrdinalIgnoreCase));
            if (lesson is not null)
            {
                return (module, lesson);
            }
        }

        throw new AcademyRuleException(404, "That lesson was not found.");
    }

    public async Task<object> CompleteLessonAsync(AcademyLearner learner, string courseId, string lessonId, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(learner.UserId, courseId, ct);
        var (_, lesson) = FindLesson(course, lessonId);
        var progress = await ProgressAsync(learner.UserId, course.Id, ct);
        if (lesson.Quiz is not null && !progress.PassedQuizzes.Contains(lesson.Id))
        {
            throw new AcademyRuleException(409, "Pass this unit's quiz to complete the lesson.", "quiz_required");
        }

        var (_, newly) = await MarkDoneAsync(learner, course, lesson, progress, ct);
        var certificate = await TryIssueCertificateAsync(learner, course, ct);
        return new { newlyCompleted = newly, minutes = newly ? lesson.Minutes : 0, certificateIssued = certificate is not null };
    }

    private async Task<(AcademyProgress Progress, bool NewlyCompleted)> MarkDoneAsync(
        AcademyLearner learner, AcademyCourse course, AcademyLesson lesson, AcademyProgress progress, CancellationToken ct)
    {
        if (progress.DoneLessons.Contains(lesson.Id))
        {
            return (progress, false);
        }

        progress = progress with { DoneLessons = [.. progress.DoneLessons, lesson.Id], LastLessonId = lesson.Id };
        await SaveProgressAsync(learner.UserId, progress, ct);
        await AddMinutesAsync(learner.UserId, lesson.Minutes, ct);
        await LogAsync(Owner(learner.UserId), $"Completed {lesson.Title}", ct);
        return (progress, true);
    }

    public async Task SaveNoteAsync(string userId, string courseId, string lessonId, string text, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(userId, courseId, ct);
        var (_, lesson) = FindLesson(course, lessonId);
        if (text.Length > MaxNoteLength)
        {
            throw new AcademyRuleException(400, $"A note can be up to {MaxNoteLength} characters.");
        }

        var progress = await ProgressAsync(userId, course.Id, ct);
        var notes = progress.Notes.ToDictionary(pair => pair.Key, pair => pair.Value);
        notes[lesson.Id] = text.Trim();
        await SaveProgressAsync(userId, progress with { Notes = notes }, ct);
    }

    // ---------------------------------------------------------------- assessments

    private sealed record Located(AcademyAssessment Assessment, string Kind, AcademyLesson? Lesson, AcademyModule? Module);

    private static Located FindAssessment(AcademyCourse course, string assessmentId)
    {
        foreach (var module in course.Modules)
        {
            foreach (var lesson in module.Lessons)
            {
                if (lesson.Quiz is not null && string.Equals(lesson.Quiz.Id, assessmentId, StringComparison.OrdinalIgnoreCase))
                {
                    return new Located(lesson.Quiz, "quiz", lesson, module);
                }
            }

            if (module.Assessment is not null && string.Equals(module.Assessment.Id, assessmentId, StringComparison.OrdinalIgnoreCase))
            {
                return new Located(module.Assessment, "module", null, module);
            }
        }

        if (course.Check is not null && string.Equals(course.Check.Id, assessmentId, StringComparison.OrdinalIgnoreCase))
        {
            return new Located(course.Check, "check", null, null);
        }

        throw new AcademyRuleException(404, "That assessment was not found.");
    }

    public async Task<object> StartAssessmentAsync(string userId, string courseId, string assessmentId, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(userId, courseId, ct);
        var found = FindAssessment(course, assessmentId);
        var progress = await ProgressAsync(userId, course.Id, ct);
        var record = progress.Assessments.GetValueOrDefault(found.Assessment.Id);
        var attempts = record?.Attempts ?? 0;
        var passed = Passed(progress, found);
        var left = found.Assessment.MaxAttempts == 0 ? (int?)null : Math.Max(0, found.Assessment.MaxAttempts - attempts);
        if (left == 0 && !passed)
        {
            throw new AcademyRuleException(409, "You have used every attempt. Ask the Academy team to reset this assessment.", "no_attempts");
        }

        return new
        {
            id = found.Assessment.Id,
            courseId = course.Id,
            title = found.Assessment.Title,
            kind = found.Kind,
            lessonId = found.Lesson?.Id,
            moduleId = found.Module?.Id,
            passMark = found.Assessment.PassMark,
            maxAttempts = found.Assessment.MaxAttempts,
            attemptsUsed = attempts,
            attemptsLeft = left,
            best = record?.Best ?? 0,
            passed,
            questions = found.Assessment.Questions.Select(question => new
            {
                id = question.Id,
                q = question.Q,
                options = Shuffled(question.Options),
            }),
        };
    }

    private static bool Passed(AcademyProgress progress, Located found) => found.Kind switch
    {
        "quiz" => progress.PassedQuizzes.Contains(found.Lesson!.Id),
        "check" => progress.CheckPassed,
        _ => progress.Assessments.TryGetValue(found.Assessment.Id, out var record) && record.Passed,
    };

    /// <summary>Options in a random order, each carrying the position it has in the question's own list.</summary>
    private static IEnumerable<object> Shuffled(IReadOnlyList<string> options) =>
        options.Select((text, index) => (text, index)).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Select(item => (object)new { k = item.index, text = item.text });

    public async Task<object> SubmitAssessmentAsync(AcademyLearner learner, string courseId, string assessmentId, IReadOnlyDictionary<string, int>? answers, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(learner.UserId, courseId, ct);
        var found = FindAssessment(course, assessmentId);
        var assessment = found.Assessment;
        var progress = await ProgressAsync(learner.UserId, course.Id, ct);
        var record = progress.Assessments.GetValueOrDefault(assessment.Id);
        var attemptsBefore = record?.Attempts ?? 0;
        var wasPassed = Passed(progress, found);
        if (assessment.MaxAttempts > 0 && attemptsBefore >= assessment.MaxAttempts && !wasPassed)
        {
            throw new AcademyRuleException(409, "You have used every attempt. Ask the Academy team to reset this assessment.", "no_attempts");
        }

        answers ??= new Dictionary<string, int>();
        if (assessment.Questions.Any(question => !answers.ContainsKey(question.Id)))
        {
            throw new AcademyRuleException(400, "Answer every question before you submit.");
        }

        var results = assessment.Questions.Select(question =>
        {
            var chosen = answers[question.Id];
            if (chosen < 0 || chosen >= question.Options.Count)
            {
                throw new AcademyRuleException(400, "One of the answers is not a valid choice.");
            }

            return (Question: question, Chosen: chosen, Right: chosen == question.Answer);
        }).ToList();

        var correct = results.Count(item => item.Right);
        var percent = results.Count == 0 ? 0 : (int)Math.Round(100.0 * correct / results.Count);
        var passed = percent >= assessment.PassMark;
        var attempts = attemptsBefore + 1;
        var left = assessment.MaxAttempts == 0 ? (int?)null : Math.Max(0, assessment.MaxAttempts - attempts);

        var assessments = progress.Assessments.ToDictionary(pair => pair.Key, pair => pair.Value);
        assessments[assessment.Id] = new AcademyAssessmentRecord(attempts, Math.Max(record?.Best ?? 0, percent), wasPassed || passed, clock.GetUtcNow());
        progress = progress with { Assessments = assessments };
        var lessonCompleted = false;
        if (passed)
        {
            switch (found.Kind)
            {
                case "quiz" when !progress.PassedQuizzes.Contains(found.Lesson!.Id):
                    progress = progress with { PassedQuizzes = [.. progress.PassedQuizzes, found.Lesson.Id] };
                    break;
                case "check":
                    progress = progress with { CheckPassed = true };
                    break;
            }
        }

        await SaveProgressAsync(learner.UserId, progress, ct);
        if (passed && found.Kind == "quiz")
        {
            (progress, lessonCompleted) = await MarkDoneAsync(learner, course, found.Lesson!, progress, ct);
        }

        await LogAsync(Owner(learner.UserId), $"{(passed ? "Passed" : "Took")} {assessment.Title}: {percent}%", ct);
        if (passed && !wasPassed && found.Kind != "quiz")
        {
            await NotifyAsync(Owner(learner.UserId), $"You passed {assessment.Title} with {percent}%.", $"/academy/courses/{course.Id}", "check", ct);
        }

        var certificate = passed ? await TryIssueCertificateAsync(learner, course, ct) : null;

        // Showing the right answers after a failed module assessment would hand them to the next attempt.
        var reveal = found.Kind != "module" || passed || left == 0;
        return new
        {
            score = percent,
            correct,
            total = results.Count,
            passed,
            passMark = assessment.PassMark,
            attempts,
            attemptsLeft = left,
            best = assessments[assessment.Id].Best,
            lessonCompleted,
            certificateIssued = certificate is not null,
            review = results.Select(item => new
            {
                id = item.Question.Id,
                q = item.Question.Q,
                options = item.Question.Options,
                chosen = item.Chosen,
                right = item.Right,
                correctIndex = reveal ? (int?)item.Question.Answer : null,
                why = reveal ? item.Question.Why : null,
            }),
        };
    }

    // ---------------------------------------------------------------- final task

    public async Task<object> FinalTaskAsync(string userId, string courseId, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(userId, courseId, ct);
        var task = course.FinalTask ?? throw new AcademyRuleException(404, "This course has no final task.");
        var progress = await ProgressAsync(userId, course.Id, ct);
        var submission = progress.FinalTask;
        var blocker = FinalBlocker(course, progress);
        return new
        {
            id = task.Id,
            courseId = course.Id,
            courseTitle = course.Title,
            title = task.Title,
            brief = task.Brief,
            deliverables = task.Deliverables,
            integrity = task.Integrity,
            requirement = task.Requirement,
            canvasBoxes = task.CanvasBoxes,
            minWords = task.MinWords,
            maxWords = task.MaxWords,
            passMark = task.PassMark,
            outOf = task.OutOf,
            rubric = task.Rubric.Select(row => new { criterion = row.Criterion, marks = row.Marks, distinction = row.Distinction }),
            locked = blocker is not null,
            lockedReason = blocker,
            submission = new
            {
                status = submission.Status,
                canvas = submission.Canvas,
                proposal = submission.Proposal,
                submittedAt = submission.SubmittedAt,
                score = submission.Score,
                scores = submission.Scores.Select(score => new { criterion = score.Criterion, marks = score.Marks, outOf = score.OutOf }),
                feedback = submission.Feedback,
                markedAt = submission.MarkedAt,
            },
        };
    }

    private static string? FinalBlocker(AcademyCourse course, AcademyProgress progress)
    {
        var lessonsLeft = course.AllLessons.Count(lesson => !progress.DoneLessons.Contains(lesson.Id));
        if (lessonsLeft > 0)
        {
            return $"Finish the {lessonsLeft} remaining lesson{(lessonsLeft == 1 ? string.Empty : "s")} first.";
        }

        var assessmentsLeft = course.Modules.Count(module => module.Assessment is not null
            && !(progress.Assessments.TryGetValue(module.Assessment.Id, out var record) && record.Passed));
        return assessmentsLeft > 0 ? $"Pass the {assessmentsLeft} remaining module assessment{(assessmentsLeft == 1 ? string.Empty : "s")} first." : null;
    }

    public async Task SaveFinalDraftAsync(string userId, string courseId, FinalDraft draft, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(userId, courseId, ct);
        var task = course.FinalTask ?? throw new AcademyRuleException(404, "This course has no final task.");
        var progress = await ProgressAsync(userId, course.Id, ct);
        if (progress.FinalTask.Status is "submitted" or "passed")
        {
            throw new AcademyRuleException(409, "This task has been submitted and can no longer be changed.");
        }

        var canvas = (draft.Canvas ?? []).Take(task.CanvasBoxes.Count).Select(value => Clip(value ?? string.Empty, 1500)).ToList();
        while (canvas.Count < task.CanvasBoxes.Count)
        {
            canvas.Add(string.Empty);
        }

        var next = progress.FinalTask with { Status = "draft", Canvas = canvas, Proposal = Clip(draft.Proposal ?? string.Empty, 30000) };
        await SaveProgressAsync(userId, progress with { FinalTask = next }, ct);
    }

    public async Task SubmitFinalAsync(AcademyLearner learner, string courseId, FinalDraft draft, CancellationToken ct)
    {
        var course = await EnrolledCourseAsync(learner.UserId, courseId, ct);
        var task = course.FinalTask ?? throw new AcademyRuleException(404, "This course has no final task.");
        var progress = await ProgressAsync(learner.UserId, course.Id, ct);
        if (progress.FinalTask.Status is "submitted" or "passed")
        {
            throw new AcademyRuleException(409, "This task has already been submitted.");
        }

        if (FinalBlocker(course, progress) is { } blocker)
        {
            throw new AcademyRuleException(409, blocker, "locked");
        }

        var canvas = (draft.Canvas ?? []).Select(value => (value ?? string.Empty).Trim()).ToList();
        if (canvas.Count != task.CanvasBoxes.Count || canvas.Any(value => value.Length == 0))
        {
            throw new AcademyRuleException(400, $"Fill every box of the canvas ({task.CanvasBoxes.Count} boxes).");
        }

        var proposal = (draft.Proposal ?? string.Empty).Trim();
        var words = WordCount(proposal);
        if (task.MinWords > 0 && words < task.MinWords)
        {
            throw new AcademyRuleException(400, $"The proposal is {words} words. It needs at least {task.MinWords}.");
        }

        if (task.MaxWords > 0 && words > task.MaxWords)
        {
            throw new AcademyRuleException(400, $"The proposal is {words} words. It can be at most {task.MaxWords}.");
        }

        var next = new AcademyFinalSubmission { Status = "submitted", Canvas = canvas, Proposal = proposal, SubmittedAt = clock.GetUtcNow() };
        await SaveProgressAsync(learner.UserId, progress with { FinalTask = next }, ct);
        await LogAsync(Owner(learner.UserId), $"Submitted the final task for {course.Title}", ct);
        await NotifyAsync(Owner(learner.UserId), $"Your final task for {course.Title} was submitted. You will be told when it has been marked.", $"/academy/final/{course.Id}", "doc", ct);
    }

    public static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    // ---------------------------------------------------------------- operator

    public async Task<object> PendingFinalsAsync(CancellationToken ct)
    {
        var records = await store.ListByKindAsync(AcademyKinds.Progress, 2000, ct);
        var rows = new List<object>();
        foreach (var record in records)
        {
            var progress = JsonSerializer.Deserialize<AcademyProgress>(record.Json, Json);
            if (progress is null || progress.FinalTask.Status != "submitted")
            {
                continue;
            }

            var profile = await ReadAsync<AcademyProfile>(AcademyKinds.Profile, record.Owner, "profile", ct);
            var course = content.Course(progress.CourseId);
            rows.Add(new
            {
                userId = record.Owner,
                name = profile?.Name ?? record.Owner,
                email = profile?.Email,
                courseId = progress.CourseId,
                courseTitle = course?.Title ?? progress.CourseId,
                submittedAt = progress.FinalTask.SubmittedAt,
                canvas = progress.FinalTask.Canvas,
                canvasBoxes = course?.FinalTask?.CanvasBoxes ?? [],
                proposal = progress.FinalTask.Proposal,
                rubric = (course?.FinalTask?.Rubric ?? []).Select(row => new { criterion = row.Criterion, marks = row.Marks, distinction = row.Distinction }),
                passMark = course?.FinalTask?.PassMark ?? 60,
                outOf = course?.FinalTask?.OutOf ?? 100,
            });
        }

        return new { submissions = rows };
    }

    public async Task<object> MarkFinalAsync(string markerName, string learnerOwner, string courseId, IReadOnlyDictionary<string, int>? marks, string? feedback, CancellationToken ct)
    {
        var course = ReadyCourse(courseId);
        var task = course.FinalTask ?? throw new AcademyRuleException(404, "This course has no final task.");
        var owner = Owner(learnerOwner);
        var progress = await ProgressAsync(owner, course.Id, ct);
        if (progress.FinalTask.Status != "submitted")
        {
            throw new AcademyRuleException(409, "There is no submitted final task to mark.");
        }

        marks ??= new Dictionary<string, int>();
        var scores = new List<AcademyRubricScore>();
        foreach (var row in task.Rubric)
        {
            if (!marks.TryGetValue(row.Criterion, out var given) || given < 0 || given > row.Marks)
            {
                throw new AcademyRuleException(400, $"Give \"{row.Criterion}\" a mark from 0 to {row.Marks}.");
            }

            scores.Add(new AcademyRubricScore(row.Criterion, given, row.Marks));
        }

        var total = scores.Sum(score => score.Marks);
        var passed = total >= task.PassMark;
        var marked = progress.FinalTask with
        {
            Status = passed ? "passed" : "not_passed",
            Score = total,
            Scores = scores,
            Feedback = string.IsNullOrWhiteSpace(feedback) ? null : Clip(feedback, 4000),
            MarkedBy = markerName,
            MarkedAt = clock.GetUtcNow(),
        };
        await SaveProgressAsync(owner, progress with { FinalTask = marked }, ct);
        await NotifyAsync(owner, passed
            ? $"Your final task for {course.Title} passed with {total} of {task.OutOf}."
            : $"Your final task for {course.Title} scored {total} of {task.OutOf}. Read the feedback and resubmit.", $"/academy/final/{course.Id}", passed ? "check" : "doc", ct);
        await LogAsync(owner, $"Final task for {course.Title} marked: {total} of {task.OutOf}", ct);

        var profile = await ReadAsync<AcademyProfile>(AcademyKinds.Profile, owner, "profile", ct);
        var learner = new AcademyLearner(owner, profile?.Name ?? owner, profile?.Email, false, false);
        var certificate = passed ? await TryIssueCertificateAsync(learner, course, ct) : null;
        return new { score = total, passed, certificateIssued = certificate is not null };
    }

    /// <summary>People who have opened the Academy while signed in, so an operator can pick one to enrol.</summary>
    public async Task<IReadOnlyList<object>> LearnersAsync(CancellationToken ct)
    {
        var profiles = await store.ListByKindAsync(AcademyKinds.Profile, 2000, ct);
        return profiles
            .Select(record => (record.Owner, Profile: JsonSerializer.Deserialize<AcademyProfile>(record.Json, Json)))
            .Where(item => item.Profile is not null)
            .Select(item => (object)new { userId = item.Owner, name = item.Profile!.Name, email = item.Profile.Email })
            .ToList();
    }

    public async Task ResetAssessmentAsync(string learnerOwner, string courseId, string assessmentId, CancellationToken ct)
    {
        var course = ReadyCourse(courseId);
        var found = FindAssessment(course, assessmentId);
        var progress = await ProgressAsync(learnerOwner, course.Id, ct);
        var assessments = progress.Assessments.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (assessments.TryGetValue(found.Assessment.Id, out var record))
        {
            assessments[found.Assessment.Id] = record with { Attempts = 0 };
        }

        await SaveProgressAsync(learnerOwner, progress with { Assessments = assessments }, ct);
    }

    // ---------------------------------------------------------------- certificates

    /// <summary>
    /// Issues the course certificate when, and only when, every lesson is done, every module assessment and any course check
    /// is passed, and any final task is marked as passed. Safe to call after any step: it does nothing until then, and it
    /// never issues a second certificate.
    /// </summary>
    public async Task<AcademyCertificate?> TryIssueCertificateAsync(AcademyLearner learner, AcademyCourse course, CancellationToken ct)
    {
        var owner = Owner(learner.UserId);
        if (await store.GetAsync(AcademyKinds.Certificate, owner, course.Id.ToLowerInvariant(), ct) is not null)
        {
            return null;
        }

        var progress = await ProgressAsync(learner.UserId, course.Id, ct);
        var complete = course.AllLessons.All(lesson => progress.DoneLessons.Contains(lesson.Id))
            && course.Modules.All(module => module.Assessment is null || (progress.Assessments.TryGetValue(module.Assessment.Id, out var record) && record.Passed))
            && (course.Check is null || progress.CheckPassed)
            && (course.FinalTask is null || progress.FinalTask.Status == "passed");
        if (!complete)
        {
            return null;
        }

        var profile = await ReadAsync<AcademyProfile>(AcademyKinds.Profile, owner, "profile", ct);
        var certificate = new AcademyCertificate(
            Guid.NewGuid().ToString("N")[..12], NewCode(), owner, course.Id, course.Title,
            string.IsNullOrWhiteSpace(profile?.Name) ? learner.Name : profile!.Name, clock.GetUtcNow());
        await WriteAsync(AcademyKinds.Certificate, owner, course.Id.ToLowerInvariant(), certificate, ct);
        await WriteAsync(AcademyKinds.CertificateCode, "-", certificate.Code, certificate, ct);
        await NotifyAsync(owner, $"Your certificate for {course.Title} is ready.", $"/academy/certificates/{certificate.Id}", "award", ct);
        await LogAsync(owner, $"Earned the certificate for {course.Title}", ct);
        return certificate;
    }

    private static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[12];
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        var raw = new string(chars);
        return $"IIPA-{raw[..4]}-{raw[4..8]}-{raw[8..]}";
    }

    public async Task<AcademyCertificate?> CertificateAsync(string userId, string certificateId, CancellationToken ct)
    {
        var records = await store.ListByOwnerAsync(AcademyKinds.Certificate, Owner(userId), ct);
        return records.Select(record => JsonSerializer.Deserialize<AcademyCertificate>(record.Json, Json)!)
            .FirstOrDefault(item => string.Equals(item.Id, certificateId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>For the public verification page: only what a certificate itself says, never anything about the person's progress.</summary>
    public async Task<AcademyCertificate?> VerifyAsync(string code, CancellationToken ct)
    {
        var normalised = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalised.Length is < 10 or > 30)
        {
            return null;
        }

        return await ReadAsync<AcademyCertificate>(AcademyKinds.CertificateCode, "-", normalised, ct);
    }

    // ---------------------------------------------------------------- notifications, minutes and activity

    private async Task NotifyAsync(string owner, string text, string href, string icon, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        await WriteAsync(AcademyKinds.Notification, owner, id, new AcademyNotification(id, owner, text, href, icon, false, clock.GetUtcNow()), ct);
    }

    public async Task MarkNotificationsReadAsync(string userId, string? id, CancellationToken ct)
    {
        var owner = Owner(userId);
        foreach (var record in await store.ListByOwnerAsync(AcademyKinds.Notification, owner, ct))
        {
            var item = JsonSerializer.Deserialize<AcademyNotification>(record.Json, Json)!;
            if (!item.Read && (id is null || string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                await WriteAsync(AcademyKinds.Notification, owner, record.Key, item with { Read = true }, ct);
            }
        }
    }

    private async Task AddMinutesAsync(string userId, int minutes, CancellationToken ct)
    {
        var owner = Owner(userId);
        var current = await ReadAsync<AcademyMinutes>(AcademyKinds.Minutes, owner, "minutes", ct);
        var days = (current?.Days ?? new Dictionary<string, int>()).ToDictionary(pair => pair.Key, pair => pair.Value);
        var today = Key(Today());
        days[today] = days.GetValueOrDefault(today) + minutes;
        var cutoff = Key(Today().AddDays(-KeepDays));
        foreach (var stale in days.Keys.Where(key => string.CompareOrdinal(key, cutoff) < 0).ToList())
        {
            days.Remove(stale);
        }

        await WriteAsync(AcademyKinds.Minutes, owner, "minutes", new AcademyMinutes(days), ct);
    }

    private async Task LogAsync(string owner, string text, CancellationToken ct)
    {
        var current = await ReadAsync<AcademyActivity>(AcademyKinds.Activity, owner, "activity", ct);
        var entries = new List<AcademyActivityEntry> { new(text, clock.GetUtcNow()) };
        entries.AddRange(current?.Entries ?? []);
        await WriteAsync(AcademyKinds.Activity, owner, "activity", new AcademyActivity(entries.Take(MaxActivity).ToList()), ct);
    }
}

public sealed record ProfileUpdate(
    string? Name, string? JobTitle, string? Organisation, string? About, string? Language, string? Goal,
    bool? EmailNotifications, bool? WeeklySummary, bool? Replies, bool? AiSuggestions);

public sealed record FinalDraft(IReadOnlyList<string?>? Canvas, string? Proposal);

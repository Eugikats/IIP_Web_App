using System.Collections.Concurrent;
using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The Academy's rules, exercised against an in-memory store: who may read what, how quizzes are graded, when a certificate exists.</summary>
public sealed class AcademyLearnerServiceTests
{
    private static readonly AcademyContentProvider Content = new();

    private sealed class MemoryStore : IAcademyRecordStore
    {
        private readonly ConcurrentDictionary<(string, string, string), AcademyRecord> rows = new();

        public Task<AcademyRecord?> GetAsync(string kind, string owner, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.TryGetValue((kind, owner, key), out var row) ? row : null);

        public Task SaveAsync(string kind, string owner, string key, string json, CancellationToken cancellationToken = default)
        {
            rows[(kind, owner, key)] = new AcademyRecord(kind, owner, key, json, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AcademyRecord>> ListByOwnerAsync(string kind, string owner, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind && row.Owner == owner).ToList());

        public Task<IReadOnlyList<AcademyRecord>> ListByKindAsync(string kind, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind).OrderByDescending(row => row.UpdatedAt).Take(take).ToList());

        public Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
        {
            rows.TryRemove((kind, owner, key), out _);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Values.Count(row => row.Kind == kind));
    }

    private static AcademyLearnerService NewService() => new(new MemoryStore(), Content, TimeProvider.System);

    private static AcademyLearner Person(string id, string name = "Aisha Namutebi") => new(id, name, $"{id}@example.test", false, false);

    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static Dictionary<string, int> Right(AcademyAssessment assessment) => assessment.Questions.ToDictionary(question => question.Id, question => question.Answer);

    private static Dictionary<string, int> Wrong(AcademyAssessment assessment) =>
        assessment.Questions.ToDictionary(question => question.Id, question => (question.Answer + 1) % question.Options.Count);

    private static async Task<AcademyRuleException> Refused(Func<Task> action) => await Assert.ThrowsAsync<AcademyRuleException>(action);

    [Fact]
    public async Task Lessons_AreClosedUntilTheLearnerEnrols_AndComingSoonCoursesCannotBeEntered()
    {
        var service = NewService();
        var learner = Person("u1");

        var closed = await Refused(() => service.LessonAsync("u1", "ai-agentic-insurance", "unit-1-1", default));
        Assert.Equal(403, closed.Status);

        var soon = await Refused(() => service.EnrolAsync("u1", "claims-communication", "u1", null, default));
        Assert.Equal(409, soon.Status);

        Assert.True(await service.EnrolAsync(learner.UserId, "ai-agentic-insurance", "u1", null, default));
        Assert.False(await service.EnrolAsync(learner.UserId, "ai-agentic-insurance", "u1", null, default));
        var lesson = J(await service.LessonAsync("u1", "ai-agentic-insurance", "unit-1-1", default));
        Assert.Equal("unit-1-1", lesson.GetProperty("id").GetString());
        Assert.True(lesson.GetProperty("hasQuiz").GetBoolean());
    }

    [Fact]
    public async Task OneLearner_CannotSeeAnotherLearnersProgressOrNotes()
    {
        var service = NewService();
        await service.EnrolAsync("alice", "ai-agentic-insurance", "alice", null, default);
        await service.SaveNoteAsync("alice", "ai-agentic-insurance", "unit-1-1", "my private note", default);

        var bob = J(await service.StateAsync(Person("bob"), default));
        Assert.Empty(bob.GetProperty("enrolled").EnumerateArray());
        Assert.Empty(bob.GetProperty("progress").EnumerateObject());

        var denied = await Refused(() => service.LessonAsync("bob", "ai-agentic-insurance", "unit-1-1", default));
        Assert.Equal(403, denied.Status);

        await service.EnrolAsync("bob", "ai-agentic-insurance", "bob", null, default);
        var bobLesson = J(await service.LessonAsync("bob", "ai-agentic-insurance", "unit-1-1", default));
        Assert.Equal(string.Empty, bobLesson.GetProperty("note").GetString());
        var aliceLesson = J(await service.LessonAsync("alice", "ai-agentic-insurance", "unit-1-1", default));
        Assert.Equal("my private note", aliceLesson.GetProperty("note").GetString());
    }

    [Fact]
    public async Task StartingAnAssessment_NeverSendsAnswersOrExplanations_AndKeepsEachOptionsOwnPosition()
    {
        var service = NewService();
        await service.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;

        var started = J(await service.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default));
        var raw = started.GetRawText();
        Assert.DoesNotContain("\"answer\"", raw);
        Assert.DoesNotContain("\"why\"", raw);
        var questions = started.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(10, questions.Count);
        foreach (var question in questions)
        {
            var original = quiz.Questions.Single(item => item.Id == question.GetProperty("id").GetString());
            var options = question.GetProperty("options").EnumerateArray().ToList();
            Assert.Equal(original.Options.Count, options.Count);
            Assert.All(options, option => Assert.Equal(original.Options[option.GetProperty("k").GetInt32()], option.GetProperty("text").GetString()));
        }
    }

    [Fact]
    public async Task UnitQuiz_GatesTheLesson_AFailLeavesItOpen_APassCompletesItAndCountsMinutes()
    {
        var service = NewService();
        var learner = Person("u1");
        await service.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var lesson = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0];

        var gated = await Refused(() => service.CompleteLessonAsync(learner, "ai-agentic-insurance", lesson.Id, default));
        Assert.Equal("quiz_required", gated.Code);

        var failed = J(await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", lesson.Quiz!.Id, Wrong(lesson.Quiz), default));
        Assert.False(failed.GetProperty("passed").GetBoolean());
        Assert.Equal(0, failed.GetProperty("score").GetInt32());
        Assert.False(failed.GetProperty("lessonCompleted").GetBoolean());

        var passed = J(await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", lesson.Quiz.Id, Right(lesson.Quiz), default));
        Assert.True(passed.GetProperty("passed").GetBoolean());
        Assert.Equal(100, passed.GetProperty("score").GetInt32());
        Assert.True(passed.GetProperty("lessonCompleted").GetBoolean());

        var state = J(await service.StateAsync(learner, default));
        var progress = state.GetProperty("progress").GetProperty("ai-agentic-insurance");
        Assert.Contains(lesson.Id, progress.GetProperty("doneLessons").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(lesson.Minutes, state.GetProperty("stats").GetProperty("minutesToday").GetInt32());
        Assert.Equal(1, state.GetProperty("stats").GetProperty("streakDays").GetInt32());
    }

    [Fact]
    public async Task EveryAnswerIsRequired_AndAnOutOfRangeChoiceIsRefused()
    {
        var service = NewService();
        var learner = Person("u1");
        await service.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;

        var partial = Right(quiz);
        partial.Remove(quiz.Questions[0].Id);
        Assert.Equal(400, (await Refused(() => service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", quiz.Id, partial, default))).Status);

        var outOfRange = Right(quiz);
        outOfRange[quiz.Questions[0].Id] = 99;
        Assert.Equal(400, (await Refused(() => service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", quiz.Id, outOfRange, default))).Status);
    }

    [Fact]
    public async Task ModuleAssessment_AllowsThreeAttempts_HidesAnswersWhileAttemptsRemain_ThenStops()
    {
        var service = NewService();
        var learner = Person("u1");
        await service.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var assessment = Content.Course("ai-agentic-insurance")!.Modules[0].Assessment!;

        var first = J(await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", assessment.Id, Wrong(assessment), default));
        Assert.False(first.GetProperty("passed").GetBoolean());
        Assert.Equal(2, first.GetProperty("attemptsLeft").GetInt32());
        Assert.All(first.GetProperty("review").EnumerateArray(), item =>
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("correctIndex").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("why").ValueKind);
        });

        await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", assessment.Id, Wrong(assessment), default);
        var third = J(await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", assessment.Id, Wrong(assessment), default));
        Assert.Equal(0, third.GetProperty("attemptsLeft").GetInt32());
        Assert.All(third.GetProperty("review").EnumerateArray(), item => Assert.NotEqual(JsonValueKind.Null, item.GetProperty("correctIndex").ValueKind));

        var stopped = await Refused(() => service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", assessment.Id, Right(assessment), default));
        Assert.Equal("no_attempts", stopped.Code);
        var cannotStart = await Refused(() => service.StartAssessmentAsync("u1", "ai-agentic-insurance", assessment.Id, default));
        Assert.Equal("no_attempts", cannotStart.Code);

        await service.ResetAssessmentAsync("u1", "ai-agentic-insurance", assessment.Id, default);
        var afterReset = J(await service.SubmitAssessmentAsync(learner, "ai-agentic-insurance", assessment.Id, Right(assessment), default));
        Assert.True(afterReset.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task ACourseWithACheck_IssuesOneCertificateOnlyAfterEveryLessonAndTheCheck_AndItCanBeVerifiedByCode()
    {
        var service = NewService();
        var learner = Person("u1", "Aisha Namutebi");
        var course = Content.Course("ai-claims-fraud")!;
        await service.EnrolAsync("u1", course.Id, "u1", null, default);

        var lessons = course.AllLessons.ToList();
        foreach (var lesson in lessons.Take(lessons.Count - 1))
        {
            await service.CompleteLessonAsync(learner, course.Id, lesson.Id, default);
        }

        var early = J(await service.SubmitAssessmentAsync(learner, course.Id, course.Check!.Id, Right(course.Check), default));
        Assert.True(early.GetProperty("passed").GetBoolean());
        Assert.False(early.GetProperty("certificateIssued").GetBoolean());
        Assert.Empty(J(await service.StateAsync(learner, default)).GetProperty("certificates").EnumerateArray());

        var last = J(await service.CompleteLessonAsync(learner, course.Id, lessons[^1].Id, default));
        Assert.True(last.GetProperty("certificateIssued").GetBoolean());

        var again = J(await service.SubmitAssessmentAsync(learner, course.Id, course.Check.Id, Right(course.Check), default));
        Assert.False(again.GetProperty("certificateIssued").GetBoolean());

        var certificates = J(await service.StateAsync(learner, default)).GetProperty("certificates").EnumerateArray().ToList();
        var certificate = Assert.Single(certificates);
        Assert.Equal("Aisha Namutebi", certificate.GetProperty("name").GetString());

        var code = certificate.GetProperty("code").GetString()!;
        var verified = await service.VerifyAsync(code, default);
        Assert.Equal(course.Title, verified!.CourseTitle);
        Assert.Null(await service.VerifyAsync("IIPA-AAAA-BBBB-CCCC", default));
        Assert.Null(await service.VerifyAsync("", default));
    }

    [Fact]
    public async Task CourseOne_NeedsTheFinalTaskMarkedAndPassed_BeforeACertificateExists()
    {
        var service = NewService();
        var learner = Person("u1", "Aisha Namutebi");
        var course = Content.Course("ai-agentic-insurance")!;
        await service.EnrolAsync("u1", course.Id, "u1", null, default);

        var locked = J(await service.FinalTaskAsync("u1", course.Id, default));
        Assert.True(locked.GetProperty("locked").GetBoolean());
        var early = await Refused(() => service.SubmitFinalAsync(learner, course.Id, new FinalDraft(Enumerable.Repeat("x", 10).ToList(), "word"), default));
        Assert.Equal("locked", early.Code);

        foreach (var module in course.Modules)
        {
            foreach (var lesson in module.Lessons)
            {
                await service.SubmitAssessmentAsync(learner, course.Id, lesson.Quiz!.Id, Right(lesson.Quiz), default);
            }

            await service.SubmitAssessmentAsync(learner, course.Id, module.Assessment!.Id, Right(module.Assessment), default);
        }

        Assert.False(J(await service.FinalTaskAsync("u1", course.Id, default)).GetProperty("locked").GetBoolean());
        Assert.Empty(J(await service.StateAsync(learner, default)).GetProperty("certificates").EnumerateArray());

        var task = course.FinalTask!;
        var canvas = Enumerable.Repeat("A plain, specific answer for this box.", task.CanvasBoxes.Count).ToList();

        var tooShort = await Refused(() => service.SubmitFinalAsync(learner, course.Id, new FinalDraft(canvas, "Too short."), default));
        Assert.Equal(400, tooShort.Status);
        var emptyBox = canvas.ToList();
        emptyBox[3] = " ";
        Assert.Equal(400, (await Refused(() => service.SubmitFinalAsync(learner, course.Id, new FinalDraft(emptyBox, Words(task.MinWords)), default))).Status);

        await service.SaveFinalDraftAsync("u1", course.Id, new FinalDraft(canvas, "draft text"), default);
        await service.SubmitFinalAsync(learner, course.Id, new FinalDraft(canvas, Words(task.MinWords)), default);
        var status = J(await service.StateAsync(learner, default)).GetProperty("progress").GetProperty(course.Id).GetProperty("finalTask").GetProperty("status").GetString();
        Assert.Equal("submitted", status);
        Assert.Equal(409, (await Refused(() => service.SubmitFinalAsync(learner, course.Id, new FinalDraft(canvas, Words(task.MinWords)), default))).Status);

        var pending = J(await service.PendingFinalsAsync(default)).GetProperty("submissions").EnumerateArray().ToList();
        Assert.Single(pending);

        var overMark = task.Rubric.ToDictionary(row => row.Criterion, row => row.Marks);
        overMark[task.Rubric[0].Criterion] = task.Rubric[0].Marks + 1;
        Assert.Equal(400, (await Refused(() => service.MarkFinalAsync("Marker", "u1", course.Id, overMark, null, default))).Status);

        var low = task.Rubric.ToDictionary(row => row.Criterion, _ => 0);
        var notPassed = J(await service.MarkFinalAsync("Marker", "u1", course.Id, low, "Needs more evidence.", default));
        Assert.False(notPassed.GetProperty("passed").GetBoolean());
        Assert.Empty(J(await service.StateAsync(learner, default)).GetProperty("certificates").EnumerateArray());

        // A task that was not passed can be corrected and sent again.
        await service.SubmitFinalAsync(learner, course.Id, new FinalDraft(canvas, Words(task.MinWords)), default);
        var full = task.Rubric.ToDictionary(row => row.Criterion, row => row.Marks);
        var passed = J(await service.MarkFinalAsync("Marker", "u1", course.Id, full, "Strong work.", default));
        Assert.True(passed.GetProperty("passed").GetBoolean());
        Assert.True(passed.GetProperty("certificateIssued").GetBoolean());
        Assert.Single(J(await service.StateAsync(learner, default)).GetProperty("certificates").EnumerateArray());
    }

    [Fact]
    public async Task ProfileEdits_AreValidated_AndSaved_AndNotificationsCanBeMarkedRead()
    {
        var service = NewService();
        var learner = Person("u1", "Aisha Namutebi");

        var bad = await Refused(() => service.UpdateProfileAsync(learner, new ProfileUpdate("A", null, null, null, null, null, null, null, null, null), default));
        Assert.Equal(400, bad.Status);
        var badGoal = await Refused(() => service.UpdateProfileAsync(learner, new ProfileUpdate(null, null, null, null, null, "7", null, null, null, null), default));
        Assert.Equal(400, badGoal.Status);

        await service.UpdateProfileAsync(learner, new ProfileUpdate("Aisha N.", "Claims Lead", "Equator General", "Learning.", "English", "60", false, null, null, null), default);
        var profile = J(await service.StateAsync(learner, default)).GetProperty("profile");
        Assert.Equal("Aisha N.", profile.GetProperty("name").GetString());
        Assert.Equal("Claims Lead", profile.GetProperty("jobTitle").GetString());
        Assert.Equal("60", profile.GetProperty("goal").GetString());
        Assert.False(profile.GetProperty("emailNotifications").GetBoolean());
        Assert.True(profile.GetProperty("weeklySummary").GetBoolean());

        await service.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var unread = J(await service.StateAsync(learner, default)).GetProperty("notifications").EnumerateArray().ToList();
        Assert.Contains(unread, item => !item.GetProperty("read").GetBoolean());
        await service.MarkNotificationsReadAsync("u1", null, default);
        Assert.All(J(await service.StateAsync(learner, default)).GetProperty("notifications").EnumerateArray(), item => Assert.True(item.GetProperty("read").GetBoolean()));
    }

    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", Math.Max(count, 1)));
}

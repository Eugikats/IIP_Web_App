using System.Collections.Concurrent;
using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Domain.Academy;
using Microsoft.Extensions.Configuration;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Zenith is switched off while a person is sitting an assessment or working on the final task, so it cannot be asked for answers.</summary>
public sealed class AcademySittingTests
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
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind).Take(take).ToList());

        public Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
        {
            rows.TryRemove((kind, owner, key), out _);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(string kind, CancellationToken cancellationToken = default) => Task.FromResult(rows.Values.Count(row => row.Kind == kind));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Model : IZenithModel
    {
        public bool Configured => true;

        public Task<ZenithModelAnswer> AnswerAsync(string prompt, CancellationToken cancellationToken) => Task.FromResult(new ZenithModelAnswer("It is a thing [1].", 100, 100));
    }

    private static (AcademyZenithService Zenith, AcademyLearnerService Learners, Clock Clock, AcademyLearner Learner) Build()
    {
        var store = new MemoryStore();
        var clock = new Clock();
        var learners = new AcademyLearnerService(store, Content, clock);
        var settings = new AcademySettingsService(store, new ConfigurationBuilder().Build());
        return (new AcademyZenithService(store, Content, learners, new Model(), settings, clock), learners, clock, new AcademyLearner("u1", "Aisha", "a@example.test", false, false));
    }

    private static async Task<AcademyRuleException> Refused(Func<Task> action) => await Assert.ThrowsAsync<AcademyRuleException>(action);

    [Fact]
    public async Task StartingAnAssessment_SwitchesZenithOff_UntilItIsSubmitted()
    {
        var (zenith, learners, _, learner) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;

        await zenith.AskAsync(learner, "What should I study next?", null, default);
        await learners.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default);

        Assert.Equal("assessment_lock", (await Refused(() => zenith.AskAsync(learner, "What is agentic AI?", null, default))).Code);
        Assert.Equal("assessment_lock", (await Refused(() => zenith.CheckQuizAsync("u1", "ai-agentic-insurance", quiz.Questions[0].Id, 0, default))).Code);
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);

        var answers = quiz.Questions.ToDictionary(question => question.Id, question => question.Answer);
        await learners.SubmitAssessmentAsync(learner, "ai-agentic-insurance", quiz.Id, answers, default);

        Assert.False((await zenith.StatusAsync("u1", default)).Locked);
        await zenith.AskAsync(learner, "What should I study next?", null, default);
    }

    [Fact]
    public async Task AnIncompleteSubmission_DoesNotReleaseTheLock_AndLeavingDoes()
    {
        var (zenith, learners, _, learner) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;
        await learners.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default);

        await Refused(() => learners.SubmitAssessmentAsync(learner, "ai-agentic-insurance", quiz.Id, new Dictionary<string, int>(), default));
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);

        await learners.EndSittingAsync("u1", "assessment-" + quiz.Id, default);
        Assert.False((await zenith.StatusAsync("u1", default)).Locked);
    }

    [Fact]
    public async Task AStaleBrowserCleanupCannotUnlockAnotherAssessmentSitting()
    {
        var (zenith, learners, _, _) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "operator", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;
        var first = JsonSerializer.SerializeToElement(await learners.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default)).GetProperty("sittingId").GetString();
        var second = JsonSerializer.SerializeToElement(await learners.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default)).GetProperty("sittingId").GetString();
        await learners.LeaveAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, first, default);
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);
        await learners.LeaveAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, null, default);
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);
        await learners.LeaveAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, second, default);
        Assert.False((await zenith.StatusAsync("u1", default)).Locked);
    }

    [Fact]
    public async Task TheLockEndsOnItsOwn_NinetyMinutesAfterAnAssessmentStarted()
    {
        var (zenith, learners, clock, _) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        var quiz = Content.Course("ai-agentic-insurance")!.Modules[0].Lessons[0].Quiz!;
        await learners.StartAssessmentAsync("u1", "ai-agentic-insurance", quiz.Id, default);

        clock.Now = clock.Now.AddMinutes(60);
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);
        clock.Now = clock.Now.AddMinutes(31);
        Assert.False((await zenith.StatusAsync("u1", default)).Locked);
    }

    [Fact]
    public async Task TheFinalTaskPage_KeepsZenithOffWhileOpen_AndFiveMinutesAfter()
    {
        var (zenith, learners, clock, learner) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);

        await learners.FinalTaskOpenAsync("u1", "ai-agentic-insurance", default);
        Assert.Equal("assessment_lock", (await Refused(() => zenith.AskAsync(learner, "What is agentic AI?", null, default))).Code);

        clock.Now = clock.Now.AddMinutes(6);
        await zenith.AskAsync(learner, "What should I study next?", null, default);
    }

    [Fact]
    public async Task OnePersonSittingDoesNotSwitchZenithOffForAnother()
    {
        var (zenith, learners, _, _) = Build();
        await learners.EnrolAsync("u1", "ai-agentic-insurance", "u1", null, default);
        await learners.FinalTaskOpenAsync("u1", "ai-agentic-insurance", default);

        Assert.False((await zenith.StatusAsync("u2", default)).Locked);
        Assert.True((await zenith.StatusAsync("u1", default)).Locked);
    }
}

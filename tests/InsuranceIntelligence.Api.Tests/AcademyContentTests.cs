using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The Academy's course content as embedded in the API: complete, consistent, and never leaking answers publicly.</summary>
public sealed class AcademyContentTests
{
    private static readonly AcademyContentProvider Content = new();

    [Fact]
    public void EveryCatalogueCourseHasAnEditablePriceOfOneHundredAndTwentyDollarsByDefault()
    {
        foreach (var course in Content.Catalogue.Courses)
        {
            Assert.Contains(course.Id, AcademyPricing.CourseIds);
            Assert.Equal(120m, AcademyPricing.DefaultPrice(course.Id));
        }
    }

    [Fact]
    public void CourseOne_HasTheSuppliedShape_TwelveUnitsTenQuestionsEach_FifteenPerModuleAndTheFinalTask()
    {
        var course = Content.Course("ai-agentic-insurance")!;

        Assert.Equal(4, course.Modules.Count);
        Assert.Equal(12, course.AllLessons.Count());
        Assert.All(course.AllLessons, lesson =>
        {
            Assert.NotNull(lesson.Quiz);
            Assert.Equal(10, lesson.Quiz!.Questions.Count);
            Assert.Equal(70, lesson.Quiz.PassMark);
            Assert.NotEmpty(lesson.Blocks);
        });
        Assert.All(course.Modules, module =>
        {
            Assert.Equal(15, module.Assessment!.Questions.Count);
            Assert.Equal(3, module.Assessment.MaxAttempts);
            Assert.Equal(70, module.Assessment.PassMark);
        });
        Assert.Equal(60, course.FinalTask!.PassMark);
        Assert.Equal(100, course.FinalTask.Rubric.Sum(row => row.Marks));
        Assert.Equal(10, course.FinalTask.CanvasBoxes.Count);
        Assert.Null(course.Check);
    }

    [Fact]
    public void EveryQuestionHasAtLeastTwoOptions_AndARealAnswer()
    {
        foreach (var course in Content.Catalogue.Courses)
        {
            var assessments = course.Modules.SelectMany(m => m.Lessons.Select(l => l.Quiz).Append(m.Assessment)).Append(course.Check).Where(a => a is not null);
            foreach (var assessment in assessments)
            {
                foreach (var question in assessment!.Questions)
                {
                    Assert.True(question.Options.Count >= 2, $"{question.Id} has fewer than two options");
                    Assert.InRange(question.Answer, 0, question.Options.Count - 1);
                    // Some supplied questions have no explanation; none is invented. The page then shows the right answer only.
                    Assert.NotNull(question.Why);
                    Assert.All(question.Options, option => Assert.False(string.IsNullOrWhiteSpace(option)));
                }
            }
        }
    }

    [Fact]
    public void IdsAreUnique_AcrossCoursesLessonsAndQuestions()
    {
        var courseIds = Content.Catalogue.Courses.Select(c => c.Id).ToArray();
        Assert.Equal(courseIds.Length, courseIds.Distinct().Count());
        foreach (var course in Content.Catalogue.Courses)
        {
            var lessonIds = course.AllLessons.Select(l => l.Id).ToArray();
            Assert.Equal(lessonIds.Length, lessonIds.Distinct().Count());
            var questionIds = course.Modules.SelectMany(m => m.Lessons.Select(l => l.Quiz).Append(m.Assessment)).Where(a => a is not null).SelectMany(a => a!.Questions).Select(q => q.Id).ToArray();
            Assert.Equal(questionIds.Length, questionIds.Distinct().Count());
        }
    }

    [Fact]
    public void NothingShownToAPersonContainsAnEmDashOrAnEmoji()
    {
        var json = JsonSerializer.Serialize(Content.Catalogue);
        Assert.DoesNotContain('—', json);
        Assert.DoesNotMatch("[\\u2600-\\u27BF]", json);
    }

    [Fact]
    public void CourseTwo_IsComingSoonWithItsOutline_AndCannotBeStarted()
    {
        var course = Content.Course("claims-communication")!;

        Assert.False(course.IsReady);
        Assert.Empty(course.Modules);
        Assert.True(course.Outline.Count >= 10);
        Assert.Equal("coming_soon", AcademyStatus(course));
    }

    [Fact]
    public void TheNineOtherCoursesCanBeTaken_AndTheirCheckIsServerSide()
    {
        var ready = Content.Catalogue.Courses.Where(c => c.IsReady).ToArray();
        Assert.Equal(9, ready.Length);
        foreach (var course in ready.Where(c => c.Id != "ai-agentic-insurance" && c.Id != "ai-sales-distribution"))
        {
            Assert.NotNull(course.Check);
            Assert.True(course.Check!.Questions.Count >= 3);
            Assert.All(course.Modules, module => Assert.Equal(2, module.Lessons.Count));
        }
    }

    [Fact]
    public void ThePublicCourseViews_NeverContainAnAnswerOrLessonText()
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var course in Content.Catalogue.Courses)
        {
            var summary = JsonSerializer.Serialize(AcademyPublicEndpoints.Summary(course, prices));
            var detail = JsonSerializer.Serialize(AcademyPublicEndpoints.Detail(course, prices));
            foreach (var text in new[] { summary, detail })
            {
                Assert.DoesNotContain("\"answer\"", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"why\"", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"blocks\"", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void CourseOne_NamesItsSources_AndSaysItsContentIsPilotContent()
    {
        var course = Content.Course("ai-agentic-insurance")!;
        Assert.NotEmpty(course.Sources);
        Assert.All(course.Sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Title)));
        Assert.Contains("Pilot", course.ContentStatus);
        Assert.All(course.AllLessons, lesson => Assert.All(lesson.Sources, id => Assert.Contains(course.Sources, s => s.Id == id)));
    }

    private static string AcademyStatus(AcademyCourse course) => course.IsReady ? "ready" : "coming_soon";

    [Theory]
    [InlineData("ai-agentic-insurance")]
    [InlineData("ai-sales-distribution")]
    public void FlagshipsHaveCompleteMediaMappingsAndPrivateAssessments(string courseId)
    {
        var course = Content.Course(courseId)!;
        Assert.False(course.PaidReleaseApproved);
        Assert.Equal(4, course.Modules.Count);
        Assert.Equal(12, course.AllLessons.Count());
        Assert.All(course.AllLessons, lesson =>
        {
            Assert.Matches("^C0[12]_U[1-4]-[1-3]$", lesson.MediaUnitId!);
            Assert.Equal(10, lesson.Quiz!.Questions.Count);
            Assert.Equal(10, lesson.Quiz.Questions.Select(question => question.Q).Distinct().Count());
        });
        Assert.All(course.Modules, module => Assert.Equal(15, module.Assessment!.Questions.Count));
        Assert.Equal(100, course.FinalTask!.Rubric.Sum(row => row.Marks));
    }
}

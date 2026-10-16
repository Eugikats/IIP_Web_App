using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

public static class AcademyZenithEndpoints
{
    public static Task<IResult> Status(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyZenithService service, CancellationToken ct) =>
        AcademyLearnerEndpoints.Run(http, o, t, op, async learner => Results.Ok(await service.StatusAsync(learner.UserId, ct)));

    public static Task<IResult> Ask(ZenithAskRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyZenithService service, CancellationToken ct) =>
        AcademyLearnerEndpoints.Run(http, o, t, op, async learner =>
        {
            var reply = await service.AskAsync(learner, request.Message, request.History, ct);
            return Results.Ok(new
            {
                mode = reply.Mode,
                steps = reply.Steps,
                text = reply.Text,
                sources = reply.Sources.Select(source => new { label = source.Label, href = source.Href }),
                actions = reply.Actions.Select(action => new { label = action.Label, href = action.Href, q = action.Q }),
                quiz = reply.Quiz is null ? null : new { courseId = reply.Quiz.CourseId, questionId = reply.Quiz.QuestionId, lessonTitle = reply.Quiz.LessonTitle, q = reply.Quiz.Q, options = reply.Quiz.Options },
                seconds = reply.Seconds,
            });
        });

    public static Task<IResult> CheckQuiz(ZenithQuizAnswer request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyZenithService service, CancellationToken ct) =>
        AcademyLearnerEndpoints.Run(http, o, t, op, async learner => Results.Ok(await service.CheckQuizAsync(learner.UserId, request.CourseId ?? string.Empty, request.QuestionId ?? string.Empty, request.Choice ?? -1, ct)));
}

public sealed record ZenithAskRequest(string? Message, IReadOnlyList<ZenithTurn>? History);

public sealed record ZenithQuizAnswer(string? CourseId, string? QuestionId, int? Choice);

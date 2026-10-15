using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>
/// The Academy's learner routes. Every one acts on the signed-in person's own records and nobody else's: the person comes
/// from the sign-in, never from the request. Operator routes additionally require the Academy operator role.
/// </summary>
public static class AcademyLearnerEndpoints
{
    private static readonly IResult SignInRequired = Results.Json(new { message = "Sign in to use the Academy.", code = "sign_in" }, statusCode: StatusCodes.Status401Unauthorized);

    private static readonly IResult OperatorOnly = Results.Json(new { message = "Only the Academy team can do this.", code = "operator" }, statusCode: StatusCodes.Status403Forbidden);

    private static AcademyLearner? Who(HttpContext http, InfrastructureOptions options, ITenantContextAccessor tenant, AcademyOperator academyOperator) =>
        AcademyIdentity.Resolve(http, options, tenant, academyOperator);

    /// <summary>Runs a learner action: no sign-in is a 401, a broken rule is the status it names, and nothing is cached.</summary>
    public static async Task<IResult> Run(
        HttpContext http, InfrastructureOptions options, ITenantContextAccessor tenant, AcademyOperator academyOperator,
        Func<AcademyLearner, Task<IResult>> action, bool operatorOnly = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        var learner = Who(http, options, tenant, academyOperator);
        if (learner is null)
        {
            return SignInRequired;
        }

        if (operatorOnly && !learner.IsOperator)
        {
            return OperatorOnly;
        }

        try
        {
            return await action(learner);
        }
        catch (AcademyRuleException rule)
        {
            return Results.Json(new { message = rule.Message, code = rule.Code }, statusCode: rule.Status);
        }
    }

    public static Task<IResult> State(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.StateAsync(learner, ct)));

    public static Task<IResult> UpdateProfile(ProfileUpdate update, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.UpdateProfileAsync(learner, update, ct);
            return Results.Ok(await service.StateAsync(learner, ct));
        });

    public static Task<IResult> Enrol(string courseId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.ProfileAsync(learner, ct);
            var created = await service.EnrolAsync(learner.UserId, courseId, learner.UserId, learner.Name, ct);
            return Results.Ok(new { enrolled = true, alreadyEnrolled = !created });
        });

    public static Task<IResult> Lesson(string courseId, string lessonId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.LessonAsync(learner.UserId, courseId, lessonId, ct)));

    public static Task<IResult> Complete(string courseId, string lessonId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.CompleteLessonAsync(learner, courseId, lessonId, ct)));

    public static Task<IResult> Note(string courseId, string lessonId, NoteRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.SaveNoteAsync(learner.UserId, courseId, lessonId, request.Text ?? string.Empty, ct);
            return Results.Ok(new { saved = true });
        });

    public static Task<IResult> StartAssessment(string courseId, string assessmentId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.StartAssessmentAsync(learner.UserId, courseId, assessmentId, ct)));

    public static Task<IResult> SubmitAssessment(string courseId, string assessmentId, AssessmentSubmission request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.SubmitAssessmentAsync(learner, courseId, assessmentId, request.Answers, ct)));

    public static Task<IResult> FinalTask(string courseId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.FinalTaskAsync(learner.UserId, courseId, ct)));

    public static Task<IResult> SaveFinalDraft(string courseId, FinalDraft draft, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.SaveFinalDraftAsync(learner.UserId, courseId, draft, ct);
            return Results.Ok(new { saved = true });
        });

    public static Task<IResult> SubmitFinal(string courseId, FinalDraft draft, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.SubmitFinalAsync(learner, courseId, draft, ct);
            return Results.Ok(new { submitted = true });
        });

    public static Task<IResult> Certificate(string certificateId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            var certificate = await service.CertificateAsync(learner.UserId, certificateId, ct);
            return certificate is null
                ? Results.NotFound(new { message = "That certificate was not found." })
                : Results.Ok(new { id = certificate.Id, code = certificate.Code, courseId = certificate.CourseId, courseTitle = certificate.CourseTitle, name = certificate.Name, issuedAt = certificate.IssuedAt });
        });

    public static Task<IResult> ReadNotifications(NotificationRead request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            await service.MarkNotificationsReadAsync(learner.UserId, request.Id, ct);
            return Results.Ok(new { read = true });
        });

    /// <summary>Public: anyone holding a certificate code can check it. Nothing but the certificate's own text is returned.</summary>
    public static async Task<IResult> Verify(string code, AcademyLearnerService service, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var certificate = await service.VerifyAsync(code, ct);
        return certificate is null
            ? Results.NotFound(new { valid = false, message = "No certificate has this code." })
            : Results.Ok(new { valid = true, code = certificate.Code, name = certificate.Name, courseTitle = certificate.CourseTitle, issuedAt = certificate.IssuedAt });
    }

    // ---------------------------------------------------------------- operators

    public static Task<IResult> PendingFinals(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async _ => Results.Ok(await service.PendingFinalsAsync(ct)), operatorOnly: true);

    public static Task<IResult> MarkFinal(MarkRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner => Results.Ok(await service.MarkFinalAsync(learner.Name, request.UserId ?? string.Empty, request.CourseId ?? string.Empty, request.Marks, request.Feedback, ct)), operatorOnly: true);

    public static Task<IResult> Learners(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async _ => Results.Ok(new { learners = await service.LearnersAsync(ct) }), operatorOnly: true);

    public static Task<IResult> OperatorEnrol(OperatorEnrolRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async learner =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.CourseId))
            {
                return Results.BadRequest(new { message = "Choose a person and a course." });
            }

            var created = await service.EnrolAsync(request.UserId, request.CourseId, learner.Name, null, ct);
            return Results.Ok(new { enrolled = true, alreadyEnrolled = !created });
        }, operatorOnly: true);

    public static Task<IResult> ResetAssessment(ResetRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService service, CancellationToken ct) =>
        Run(http, o, t, op, async _ =>
        {
            await service.ResetAssessmentAsync(request.UserId ?? string.Empty, request.CourseId ?? string.Empty, request.AssessmentId ?? string.Empty, ct);
            return Results.Ok(new { reset = true });
        }, operatorOnly: true);
}

public sealed record NoteRequest(string? Text);

public sealed record AssessmentSubmission(Dictionary<string, int>? Answers);

public sealed record NotificationRead(string? Id);

public sealed record MarkRequest(string? UserId, string? CourseId, Dictionary<string, int>? Marks, string? Feedback);

public sealed record OperatorEnrolRequest(string? UserId, string? CourseId);

public sealed record ResetRequest(string? UserId, string? CourseId, string? AssessmentId);

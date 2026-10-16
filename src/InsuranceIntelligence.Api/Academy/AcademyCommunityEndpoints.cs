using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>The community's routes. Signed-in people only: names and job titles are shown to other organisations.</summary>
public static class AcademyCommunityEndpoints
{
    private static Task<IResult> Go(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, Func<AcademyLearner, Task<IResult>> action, bool operatorOnly = false) =>
        AcademyLearnerEndpoints.Run(http, o, t, op, action, operatorOnly);

    public static Task<IResult> State(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.StateAsync(learner, ct)));

    public static Task<IResult> Consent(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.GiveConsentAsync(learner, ct); return Results.Ok(new { consent = true }); });

    public static Task<IResult> Join(string groupId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.JoinAsync(learner, groupId, true, ct); return Results.Ok(new { joined = true }); });

    public static Task<IResult> Leave(string groupId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.JoinAsync(learner, groupId, false, ct); return Results.Ok(new { joined = false }); });

    public static Task<IResult> Post(PostRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.PostAsync(learner, request.Text, request.Tag, ct)));

    public static Task<IResult> Like(string postId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.ToggleLikeAsync(learner, postId, ct)));

    public static Task<IResult> Comment(string postId, CommentRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.CommentAsync(learner, postId, request.Text, ct); return Results.Ok(new { commented = true }); });

    public static Task<IResult> Report(string postId, ReportPostRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.ReportAsync(learner, postId, request.Reason, ct); return Results.Ok(new { reported = true }); });

    public static Task<IResult> Delete(string postId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await service.DeletePostAsync(learner, postId, ct); return Results.Ok(new { deleted = true }); });

    public static Task<IResult> Rsvp(string eventId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.ToggleRsvpAsync(learner, eventId, ct)));

    public static Task<IResult> CreateEvent(EventRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await service.CreateEventAsync(request.Title, request.StartsAt, request.Description, request.Link, ct)), operatorOnly: true);

    public static Task<IResult> DeleteEvent(string eventId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => { await service.DeleteEventAsync(eventId, ct); return Results.Ok(new { deleted = true }); }, operatorOnly: true);

    public static Task<IResult> CreateGroup(GroupRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await service.CreateGroupAsync(request.Name, request.Description, ct)), operatorOnly: true);

    public static Task<IResult> SetDiscord(DiscordRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => { await service.SetDiscordAsync(request.Url, ct); return Results.Ok(new { saved = true }); }, operatorOnly: true);

    public static Task<IResult> Reports(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await service.ReportsAsync(ct)), operatorOnly: true);

    public static Task<IResult> Hide(string postId, HideRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyCommunityService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => { await service.SetHiddenAsync(postId, request.Hidden, ct); return Results.Ok(new { hidden = request.Hidden }); }, operatorOnly: true);
}

public sealed record PostRequest(string? Text, string? Tag);

public sealed record CommentRequest(string? Text);

public sealed record ReportPostRequest(string? Reason);

public sealed record EventRequest(string? Title, DateTimeOffset? StartsAt, string? Description, string? Link);

public sealed record GroupRequest(string? Name, string? Description);

public sealed record DiscordRequest(string? Url);

public sealed record HideRequest(bool Hidden);

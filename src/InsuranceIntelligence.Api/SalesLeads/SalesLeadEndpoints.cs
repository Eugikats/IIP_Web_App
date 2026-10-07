using InsuranceIntelligence.Application.SalesLeads;

namespace InsuranceIntelligence.Api.SalesLeads;

public static class SalesLeadEndpoints
{
    public static async Task<IResult> GetLeadDetail(
        string leadId,
        ISalesLeadQueryService queryService,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await queryService.GetLeadDetailAsync(leadId, cancellationToken);
            return detail is null
                ? Results.NotFound(new { message = "Sales lead was not found for the current tenant workspace." })
                : Results.Ok(detail);
        }
        catch (SalesLeadQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
    }

    public static async Task<IResult> ListLeads(
        ISalesLeadQueryService queryService,
        CancellationToken cancellationToken)
    {
        var leads = await queryService.ListLeadsAsync(cancellationToken);
        return Results.Ok(leads);
    }

    public static async Task<IResult> CreateLead(
        CreateSalesLeadRequest request,
        ISalesLeadCommandService commandService,
        CancellationToken cancellationToken)
    {
        try
        {
            var lead = await commandService.CreateLeadAsync(request, cancellationToken);
            return Results.Created($"/api/v1/sales-leads/{lead.LeadId}", lead);
        }
        catch (SalesLeadQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
    }

    public static Task<IResult> AddNote(
        string leadId,
        AddLeadNoteRequest request,
        ISalesLeadCommandService commandService,
        ISalesLeadQueryService queryService,
        CancellationToken cancellationToken) =>
        AfterChange(() => commandService.AddNoteAsync(leadId, request, cancellationToken), leadId, queryService, cancellationToken);

    public static Task<IResult> Assign(
        string leadId,
        AssignLeadRequest request,
        ISalesLeadCommandService commandService,
        ISalesLeadQueryService queryService,
        CancellationToken cancellationToken) =>
        AfterChange(() => commandService.AssignAsync(leadId, request, cancellationToken), leadId, queryService, cancellationToken);

    private static async Task<IResult> AfterChange(
        Func<Task<bool>> change, string leadId, ISalesLeadQueryService queryService, CancellationToken cancellationToken)
    {
        try
        {
            if (!await change())
            {
                return Results.NotFound(new { message = "Sales lead was not found for the current tenant workspace." });
            }

            var detail = await queryService.GetLeadDetailAsync(leadId, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }
        catch (SalesLeadQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    /// <summary>Reads the lead's conversation again now. 503 when no model is configured, so the
    /// screen can say so rather than show a stale summary as current.</summary>
    public static async Task<IResult> RefreshInsights(
        string leadId,
        IServiceProvider services,
        InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContextAccessor,
        InsuranceIntelligence.Application.Agents.IAgentGate agentGate,
        ISalesLeadQueryService queryService,
        CancellationToken cancellationToken)
    {
        var insights = services.GetService<IConversationInsightsService>();
        if (insights is null)
        {
            return Results.Json(new { message = "Conversation insights are not configured: no language model is connected." }, statusCode: 503);
        }

        var context = tenantContextAccessor.Current;
        var gate = await agentGate.CheckAsync(context.Scope, ConversationInsightsService.AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return Results.Json(new { message = "The Conversation Insights Agent is switched off for this workspace." }, statusCode: 409);
        }

        try
        {
            await insights.RefreshLeadAsync(context.Scope, leadId, context.UserId, Guid.NewGuid().ToString("N"), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Results.Json(new { message = "The insights could not be prepared just now. Try again in a moment." }, statusCode: 502);
        }

        var detail = await queryService.GetLeadDetailAsync(leadId, cancellationToken);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    /// <summary>Scores every prospect, or the one named, and stores the two best-fitting covers.</summary>
    public static async Task<IResult> RunTriage(
        string? leadId,
        IProspectTriageService triage,
        InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContextAccessor,
        InsuranceIntelligence.Application.Agents.IAgentGate agentGate,
        CancellationToken cancellationToken)
    {
        var gate = await agentGate.CheckAsync(tenantContextAccessor.Current.Scope, ProspectTriageService.AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return Results.Json(new { message = "The Prospect Triage Agent is switched off for this workspace." }, statusCode: 409);
        }

        return Results.Ok(await triage.RunAsync(leadId, cancellationToken));
    }

    public static Task<IResult> RunTriageForLead(
        string leadId,
        IProspectTriageService triage,
        InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContextAccessor,
        InsuranceIntelligence.Application.Agents.IAgentGate agentGate,
        CancellationToken cancellationToken) =>
        RunTriage(leadId, triage, tenantContextAccessor, agentGate, cancellationToken);

    public static Task<IResult> RunTriageForAll(
        IProspectTriageService triage,
        InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContextAccessor,
        InsuranceIntelligence.Application.Agents.IAgentGate agentGate,
        CancellationToken cancellationToken) =>
        RunTriage(null, triage, tenantContextAccessor, agentGate, cancellationToken);

    public sealed record DraftOutreachRequest(IReadOnlyList<string>? LeadIds);

    public sealed record EditOutreachRequest(string? Text);

    public sealed record SendOutreachRequest(IReadOnlyList<OutreachRef>? Drafts);

    public static Task<IResult> DraftOutreach(DraftOutreachRequest? request, IOutreachService outreach, CancellationToken cancellationToken) =>
        OutreachCall(async () => Results.Ok(await outreach.DraftAsync(request?.LeadIds, cancellationToken)));

    public static Task<IResult> SendOutreachMany(SendOutreachRequest request, IOutreachService outreach, CancellationToken cancellationToken) =>
        OutreachCall(async () => Results.Ok(await outreach.SendManyAsync(request.Drafts ?? [], cancellationToken)));

    public static Task<IResult> SendOutreach(string leadId, Guid draftId, IOutreachService outreach, CancellationToken cancellationToken) =>
        OutreachCall(async () => Results.Ok(await outreach.SendAsync(leadId, draftId, cancellationToken)));

    public static Task<IResult> EditOutreach(string leadId, Guid draftId, EditOutreachRequest request, IOutreachService outreach, CancellationToken cancellationToken) =>
        OutreachCall(async () => await outreach.EditAsync(leadId, draftId, request.Text ?? string.Empty, cancellationToken) is { } draft
            ? Results.Ok(InsuranceIntelligence.Application.SalesLeads.SalesLeadQueryService.OutreachDtoFor(draft))
            : Results.NotFound(new { message = "This message cannot be edited: it is not in this workspace or has already gone out." }));

    public static Task<IResult> DismissOutreach(string leadId, Guid draftId, IOutreachService outreach, CancellationToken cancellationToken) =>
        OutreachCall(async () => await outreach.DismissAsync(leadId, draftId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { message = "This message cannot be dismissed: it is not in this workspace or has already gone out." }));

    private static async Task<IResult> OutreachCall(Func<Task<IResult>> call)
    {
        try
        {
            return await call();
        }
        catch (SalesLeadQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    public static async Task<IResult> GetByThread(string threadId, ISalesLeadQueryService queryService, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await queryService.GetLeadByThreadAsync(threadId, cancellationToken);
            return detail is null ? Results.NotFound(new { message = "This conversation is not a prospect yet." }) : Results.Ok(detail);
        }
        catch (SalesLeadQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    /// <summary>Merges leads that are the same person (the same phone number) into one record, keeping
    /// everything recorded on each. Safe to run again.</summary>
    public static async Task<IResult> MergeDuplicates(
        InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContextAccessor,
        ISalesLeadReadRepository readRepository,
        ISalesLeadWriteRepository writeRepository,
        CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        var all = await readRepository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var merged = 0;
        var removed = 0;
        foreach (var group in all
                     .Where(lead => !string.IsNullOrWhiteSpace(lead.Phone ?? lead.ContactInfo))
                     .GroupBy(lead => InsuranceIntelligence.Domain.Customers.PhoneNumbers.Digits(lead.Phone ?? lead.ContactInfo))
                     .Where(group => group.Key.Length >= 9 && group.Count() > 1))
        {
            var ordered = group.OrderBy(lead => lead.CreatedAt ?? DateTimeOffset.MaxValue).ToArray();
            var keep = LeadMerge.Merge(ordered);
            await writeRepository.CreateAsync(context.TenantId, context.WorkspaceId, keep, cancellationToken);
            foreach (var duplicate in ordered.Where(lead => lead.LeadId != keep.LeadId))
            {
                await writeRepository.DeleteAsync(context.TenantId, context.WorkspaceId, duplicate.LeadId, cancellationToken);
                removed++;
            }

            merged++;
        }

        return Results.Ok(new { peopleMerged = merged, recordsRemoved = removed });
    }
}

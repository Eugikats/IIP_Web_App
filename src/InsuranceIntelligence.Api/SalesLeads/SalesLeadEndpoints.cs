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

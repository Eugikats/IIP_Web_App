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
}

using InsuranceIntelligence.Application.Customers;

namespace InsuranceIntelligence.Api.Customer;

public static class CustomerProfileEndpoints
{
    public static async Task<IResult> GetCustomerProfile(
        string customerId,
        ICustomerQueryService queryService,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await queryService.GetCustomerProfileAsync(customerId, cancellationToken);
            return profile is null
                ? Results.NotFound(new { message = "Customer profile was not found for the current tenant workspace." })
                : Results.Ok(profile);
        }
        catch (CustomerQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
    }
}

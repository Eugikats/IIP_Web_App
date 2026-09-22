using InsuranceIntelligence.Application.Customers;

namespace InsuranceIntelligence.Api.Customers;

/// <summary>Outcome M: consent as a first class, staff-visible record.</summary>
public static class ConsentEndpoints
{
    public static async Task<IResult> ListForCustomer(
        string customerId,
        ICustomerConsentService consentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await consentService.ListForCustomerAsync(customerId, cancellationToken);
            return Results.Ok(records);
        }
        catch (CustomerConsentValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    public static async Task<IResult> RecordExplicitOptIn(
        string customerId,
        ConsentPurposeRequest request,
        ICustomerConsentService consentService,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["purpose"] = ["A consent purpose is required."] });
        }

        try
        {
            var record = await consentService.RecordExplicitOptInAsync(customerId, request.Purpose.Trim(), cancellationToken);
            return Results.Ok(record);
        }
        catch (CustomerConsentValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    public static async Task<IResult> Withdraw(
        string customerId,
        ConsentPurposeRequest request,
        ICustomerConsentService consentService,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["purpose"] = ["A consent purpose is required."] });
        }

        try
        {
            var record = await consentService.WithdrawAsync(customerId, request.Purpose.Trim(), cancellationToken);
            return Results.Ok(record);
        }
        catch (CustomerConsentValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}

public sealed record ConsentPurposeRequest(string Purpose);

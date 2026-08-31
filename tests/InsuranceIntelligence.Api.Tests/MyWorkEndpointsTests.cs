using System.Text.Json;
using InsuranceIntelligence.Api.MyWork;
using InsuranceIntelligence.Application.MyWork;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class MyWorkEndpointsTests
{
    [Fact]
    public async Task List_ReturnsValidationProblemForInvalidLimitWithoutCallingService()
    {
        var service = new StubMyWorkService();
        var context = Context();

        var result = await MyWorkEndpoints.List("mine", null, null, 0, service, context, CancellationToken.None);
        var (status, body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("limit", out _));
        Assert.False(service.WasCalled);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task List_DefaultsToMineAndFiftyItemsWhenFiltersAreOmitted()
    {
        var service = new StubMyWorkService();
        var context = Context();

        var result = await MyWorkEndpoints.List(null, null, null, null, service, context, CancellationToken.None);
        await ExecuteAsync(result, context);

        Assert.Equal(MyWorkOwnership.Mine, service.LastQuery?.Ownership);
        Assert.Equal(50, service.LastQuery?.Limit);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, JsonElement Body)> ExecuteAsync(IResult result, DefaultHttpContext context)
    {
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context.Response.StatusCode, body);
    }

    private sealed class StubMyWorkService : IMyWorkService
    {
        public bool WasCalled { get; private set; }
        public MyWorkQuery? LastQuery { get; private set; }
        public Task<MyWorkResponse> ListAsync(MyWorkQuery query, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastQuery = query;
            return Task.FromResult(new MyWorkResponse(query.Ownership, 0, false, [], new MyWorkSummary(0, 0, 0, 0)));
        }
    }
}

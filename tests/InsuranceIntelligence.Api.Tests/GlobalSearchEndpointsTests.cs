using System.Text.Json;
using InsuranceIntelligence.Api.Search;
using InsuranceIntelligence.Application.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class GlobalSearchEndpointsTests
{
    [Fact]
    public async Task Search_ReturnsValidationProblemForSingleCharacterQuery()
    {
        var service = new StubGlobalSearchService();
        var context = Context();

        var result = await SearchEndpoints.Search("a", 20, service, context, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("q", out _));
        Assert.False(service.WasCalled);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
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

    private sealed class StubGlobalSearchService : IGlobalSearchService
    {
        public bool WasCalled { get; private set; }

        public Task<GlobalSearchResponse> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new GlobalSearchResponse(query, 0, false, []));
        }
    }
}

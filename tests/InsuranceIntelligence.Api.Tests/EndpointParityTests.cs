using System.Reflection;
using InsuranceIntelligence.Api.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The consolidation of November 2026 lost about 150 routes and their wiring from Program.cs, and nothing failed, because no
/// test asked the running application which routes it serves. These do. One checks that every route the live build served on
/// 2026-11-02 is still registered, the other that every service an endpoint asks for can actually be built.
/// </summary>
public sealed class EndpointParityTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string[] LiveRouteFile = ReadLiveRoutes();

    private static string[] ReadLiveRoutes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Support", "live-routes-2026-11-02.txt");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Support", "live-routes-2026-11-02.txt"));
        }

        return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    }

    private IReadOnlyList<RouteEndpoint> Endpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();

    private static string Normalise(string? pattern) => (pattern ?? string.Empty).TrimEnd('/').ToLowerInvariant();

    [Fact]
    public void TheLiveRouteListIsNotEmpty()
    {
        Assert.True(LiveRouteFile.Length > 400, "The list of routes the live build served has gone missing from the test project.");
    }

    [Fact]
    public void EveryRouteTheLiveBuildServed_IsStillRegistered()
    {
        var registered = Endpoints()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method.ToUpperInvariant()} {Normalise(endpoint.RoutePattern.RawText)}"))
            .ToHashSet(StringComparer.Ordinal);

        var missing = LiveRouteFile
            .Select(line => line.Split(' ', 2))
            .Where(parts => !registered.Contains($"{parts[0]} {Normalise(parts[1])}"))
            .Select(parts => $"{parts[0]} {parts[1]}")
            .ToArray();

        Assert.True(missing.Length == 0, "Routes the live build served that this build does not register:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void NoRouteIsRegisteredTwiceForTheSameMethod()
    {
        var twice = Endpoints()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method.ToUpperInvariant()} {Normalise(endpoint.RoutePattern.RawText)}"))
            .GroupBy(key => key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(twice.Length == 0, "Routes registered more than once:\n" + string.Join('\n', twice));
    }

    [Fact]
    public void EveryServiceAnEndpointAsksFor_CanBeBuilt()
    {
        var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in Endpoints())
        {
            var method = endpoint.Metadata.GetMetadata<MethodInfo>();
            if (method is null)
            {
                continue;
            }

            foreach (var parameter in method.GetParameters())
            {
                var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                if (!IsInjectable(parameter, type) || isService.IsService(type))
                {
                    continue;
                }

                unresolved.Add($"{type.FullName} (needed by {endpoint.RoutePattern.RawText})");
            }
        }

        Assert.True(unresolved.Count == 0, "Services that endpoints ask for but nothing registers:\n" + string.Join('\n', unresolved));
    }

    /// <summary>A parameter that must come from the service container: an interface, or a class from this product that
    /// is not a request body (bodies are records or carry a binding attribute).</summary>
    private static bool IsInjectable(ParameterInfo parameter, Type type)
    {
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(decimal) || type == typeof(CancellationToken) || type == typeof(HttpContext) || type == typeof(HttpRequest) || type == typeof(HttpResponse)
            || type == typeof(Stream) || type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() != typeof(Microsoft.Extensions.Logging.ILogger<>))
        {
            return false;
        }

        if (parameter.GetCustomAttributes().Any(attribute => attribute.GetType().Name.StartsWith("From", StringComparison.Ordinal)))
        {
            return false;
        }

        if (type.Namespace is null || type.Assembly == typeof(string).Assembly)
        {
            return false;
        }

        var isRecord = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;
        return type.IsInterface || (!isRecord && type.IsClass && type.Namespace.StartsWith("InsuranceIntelligence", StringComparison.Ordinal));
    }
}

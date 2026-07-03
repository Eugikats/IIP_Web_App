using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Infrastructure.Persistence;

internal static class JsonSerializerSettings
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}

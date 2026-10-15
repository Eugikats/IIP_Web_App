using System.Reflection;
using System.Text.Json;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

public interface IAcademyContentProvider
{
    AcademyCatalogue Catalogue { get; }

    AcademyCourse? Course(string id);
}

/// <summary>Reads the course content embedded in the API once, and keeps it for the life of the process.</summary>
public sealed class AcademyContentProvider : IAcademyContentProvider
{
    public const string ResourceName = "InsuranceIntelligence.Api.Academy.Content.academy-content.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly Lazy<AcademyCatalogue> catalogue = new(Load);

    public AcademyCatalogue Catalogue => catalogue.Value;

    public AcademyCourse? Course(string id) =>
        Catalogue.Courses.FirstOrDefault(course => string.Equals(course.Id, id, StringComparison.OrdinalIgnoreCase));

    private static AcademyCatalogue Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The Academy content is not embedded in this build.");
        return JsonSerializer.Deserialize<AcademyCatalogue>(stream, Options)
            ?? throw new InvalidOperationException("The Academy content could not be read.");
    }
}

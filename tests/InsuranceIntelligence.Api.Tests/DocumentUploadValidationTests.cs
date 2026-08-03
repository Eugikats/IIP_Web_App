using InsuranceIntelligence.Api.Documents;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentUploadValidationTests
{
    [Fact]
    public void Validate_EmptyFile_Rejected() =>
        Assert.Equal("Uploaded documents cannot be empty.", DocumentUploadValidation.Validate(0, "photo.jpg"));

    [Fact]
    public void Validate_Over25Mb_Rejected() =>
        Assert.Equal(
            "Uploaded documents cannot exceed 25 MB.",
            DocumentUploadValidation.Validate(DocumentUploadValidation.MaxUploadFileSizeBytes + 1, "photo.jpg"));

    [Theory]
    [InlineData("report.pdf")]
    [InlineData("scene.JPG")]
    [InlineData("scan.tiff")]
    [InlineData("scan.tif")]
    [InlineData("notes.txt")]
    public void Validate_SupportedExtensions_Accepted(string fileName) =>
        Assert.Null(DocumentUploadValidation.Validate(1024, fileName));

    [Fact]
    public void Validate_UnsupportedExtension_Rejected() =>
        Assert.Contains("Unsupported file type", DocumentUploadValidation.Validate(1024, "clip.mp4"));
}

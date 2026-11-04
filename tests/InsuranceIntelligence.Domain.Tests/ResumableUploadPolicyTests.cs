using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Uploads;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 23: how a file is cut into pieces.</summary>
public sealed class ResumableUploadPolicyTests
{
    private const int Chunk = ResumableUploadPolicy.ChunkSize;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(Chunk, 1)]
    [InlineData(Chunk + 1, 2)]
    [InlineData(Chunk * 10, 10)]
    [InlineData(Chunk * 10 + 1, 11)]
    public void AFileIsCutIntoTheFewestWholePieces(long size, int pieces) =>
        Assert.Equal(pieces, ResumableUploadPolicy.TotalChunks(size));

    [Fact]
    public void EveryPieceIsFullSizeExceptTheLastWhichHoldsTheRest()
    {
        const long size = Chunk * 3L + 1234;

        Assert.Equal(Chunk, ResumableUploadPolicy.ExpectedLength(size, 0));
        Assert.Equal(Chunk, ResumableUploadPolicy.ExpectedLength(size, 2));
        Assert.Equal(1234, ResumableUploadPolicy.ExpectedLength(size, 3));
        Assert.Equal(-1, ResumableUploadPolicy.ExpectedLength(size, 4));
        Assert.Equal(-1, ResumableUploadPolicy.ExpectedLength(size, -1));
        Assert.Equal(Chunk, ResumableUploadPolicy.ExpectedLength(Chunk, 0));
    }

    [Fact]
    public void ThePiecesAlwaysAddUpToTheFile()
    {
        foreach (var size in new long[] { 1, 17, Chunk - 1, Chunk, Chunk + 1, Chunk * 7 + 3, 25L * 1024 * 1024 })
        {
            var total = Enumerable.Range(0, ResumableUploadPolicy.TotalChunks(size)).Sum(index => (long)ResumableUploadPolicy.ExpectedLength(size, index));
            Assert.Equal(size, total);
        }
    }

    [Fact]
    public void AFileAtTheSingleRequestLimitIsNotCutUp() =>
        Assert.Equal(1, ResumableUploadPolicy.TotalChunks(ResumableUploadPolicy.SingleRequestLimit));

    [Fact]
    public void ASessionExpiresAfterADay()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new UploadSession
        {
            UploadId = Guid.NewGuid(),
            Scope = TenantScope.Create("t", "w"),
            CaseId = Guid.NewGuid(),
            ActorId = "a",
            FileName = "f.jpg",
            ContentType = "image/jpeg",
            Size = 10,
            ChunkSize = Chunk,
            TotalChunks = 1,
            Fingerprint = "f",
            CreatedAt = now,
            ExpiresAt = now.Add(ResumableUploadPolicy.SessionLifetime),
        };

        Assert.False(ResumableUploadPolicy.IsExpired(session, now));
        Assert.True(ResumableUploadPolicy.IsExpired(session, now.AddHours(25)));
        Assert.Equal(TimeSpan.FromHours(24), ResumableUploadPolicy.SessionLifetime);
    }
}

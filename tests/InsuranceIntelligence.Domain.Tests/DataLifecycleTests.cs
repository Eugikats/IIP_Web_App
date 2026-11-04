using InsuranceIntelligence.Domain.DataLifecycle;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class DataLifecycleTests
{
    [Fact]
    public void OnlyKnownKindsOfRecordCanHaveARetentionPeriodAndItMustBeSensible()
    {
        Assert.Empty(RetentionClasses.Validate(null));
        Assert.Empty(RetentionClasses.Validate(new Dictionary<string, int> { [RetentionClasses.Documents] = 36, [RetentionClasses.ClosedClaims] = 0, [RetentionClasses.Conversations] = 240 }));
        Assert.Single(RetentionClasses.Validate(new Dictionary<string, int> { ["audit-trail"] = 12 }));
        Assert.Single(RetentionClasses.Validate(new Dictionary<string, int> { [RetentionClasses.Documents] = -1 }));
        Assert.Single(RetentionClasses.Validate(new Dictionary<string, int> { [RetentionClasses.Documents] = 241 }));
    }

    [Fact]
    public void ZeroMeansKeepAndAMissingEntryMeansKeep()
    {
        var months = new Dictionary<string, int> { [RetentionClasses.Documents] = 24, [RetentionClasses.Communications] = 0 };

        Assert.Equal(24, RetentionClasses.MonthsFor(months, RetentionClasses.Documents));
        Assert.Equal(0, RetentionClasses.MonthsFor(months, RetentionClasses.Communications));
        Assert.Equal(0, RetentionClasses.MonthsFor(months, RetentionClasses.ClosedClaims));
        Assert.Equal(0, RetentionClasses.MonthsFor(null, RetentionClasses.Documents));
    }

    [Fact]
    public void SettingsCompareAsEqualWhenOnlyTheKeptPeriodsMatch()
    {
        Assert.True(RetentionClasses.SameAs(null, new Dictionary<string, int> { [RetentionClasses.Documents] = 0 }));
        Assert.True(RetentionClasses.SameAs(new Dictionary<string, int> { [RetentionClasses.Documents] = 12 }, new Dictionary<string, int> { [RetentionClasses.Documents] = 12, [RetentionClasses.ClosedClaims] = 0 }));
        Assert.False(RetentionClasses.SameAs(new Dictionary<string, int> { [RetentionClasses.Documents] = 12 }, new Dictionary<string, int> { [RetentionClasses.Documents] = 13 }));
    }

    [Theory]
    [InlineData("+256 700 123 456", "700123456")]
    [InlineData("0700123456", "700123456")]
    [InlineData("256700123456", "700123456")]
    public void APhoneNumberIsRecognisedHoweverItIsWritten(string written, string tail) =>
        Assert.Equal(tail, new ErasureSubject(written, null, null).PhoneTail);

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", " ", "")]
    [InlineData("12345", null, null)]
    public void AnEmptyOrTooShortSubjectIsEmpty(string? phone, string? email, string? policy) =>
        Assert.True(new ErasureSubject(phone, email, policy).IsEmpty);

    [Fact]
    public void TheFingerprintIsStableIgnoresFormattingAndNeverContainsWhatWasAskedAbout()
    {
        var a = new ErasureSubject("+256 700 123 456", "Pat@Example.test", "pol-1");
        var b = new ErasureSubject("0700123456", " pat@example.test ", "POL-1");
        var c = new ErasureSubject("0700123457", "pat@example.test", "POL-1");

        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        Assert.NotEqual(a.Fingerprint(), c.Fingerprint());
        Assert.Equal(16, a.Fingerprint().Length);
        Assert.DoesNotContain("700123456", a.Fingerprint());
        Assert.DoesNotContain("example", a.Fingerprint());
    }

    [Fact]
    public void ASubjectMatchesOnAnyOfItsThreeIdentifiers()
    {
        var subject = new ErasureSubject("0700123456", "pat@example.test", "POL-9");

        Assert.True(subject.Matches("+256700123456", null, "other"));
        Assert.True(subject.Matches(null, "PAT@example.test", "other"));
        Assert.True(subject.Matches(null, null, "pol-9"));
        Assert.False(subject.Matches("+256700999999", "someone@else.test", "POL-1"));
    }

    [Fact]
    public void ContactsFoundOnTheirClaimsWidenTheSubjectWithoutOverridingWhatWasGiven()
    {
        var widened = new ErasureSubject(null, null, "POL-9").WithContactsFrom([("+256700123456", "pat@example.test"), ("+256711111111", null)]);
        var given = new ErasureSubject("0788888888", null, null).WithContactsFrom([("+256700123456", "pat@example.test")]);

        Assert.Equal("700123456", widened.PhoneTail);
        Assert.Equal("pat@example.test", widened.NormalisedEmail);
        Assert.Equal("788888888", given.PhoneTail);
    }

    [Fact]
    public void ADeletionRecordTotalsItsCounts()
    {
        var record = new DeletionRecord
        {
            DeletionId = Guid.NewGuid(),
            Scope = InsuranceIntelligence.Domain.Common.TenantScope.Create("t", "w"),
            Kind = DeletionKinds.Erasure,
            Subject = "abc",
            Reason = "request",
            Actor = "admin",
            At = DateTimeOffset.UtcNow,
            Counts = new Dictionary<string, int> { [DataCategories.Documents] = 3, [DataCategories.Communications] = 4 },
        };

        Assert.Equal(7, record.Total);
    }
}

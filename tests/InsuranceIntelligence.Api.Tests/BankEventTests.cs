using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class BankEventTests
{
    private static readonly TenantScope Bank = new("org-bank", "main");
    private static readonly TenantScope Other = new("org-other", "main");
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private EventService Service()
    {
        var db = new InsuranceIntelligenceDbContext(
            new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return new EventService(new EfWorkspaceRecordStore(db), new FixedClock(Now));
    }

    private static EventInput Loan(string phone = "+256 790 000 001", int daysAgo = 2, string? externalId = null) =>
        new("loan_disbursed", phone, Now.AddDays(-daysAgo), externalId, Amount: 8_000_000m, TermMonths: 36, Branch: "Ntinda");

    [Fact]
    public async Task ARecordedEventKeepsOnlyTheSafeDetails_AndTheNumberIsNormalised()
    {
        var result = await Service().RecordAsync(Bank, Loan("0790000001"), EventSource.Recorded, null, "staff-1");

        Assert.True(result.Created);
        Assert.Equal("256790000001", result.Event.Phone);
        Assert.Equal(8_000_000m, result.Event.Amount);
        Assert.Equal(36, result.Event.TermMonths);
        Assert.Equal("Ntinda", result.Event.Details["branch"]);
        Assert.Equal(EventProcessingState.Pending, result.Event.State);
        Assert.True(result.Event.Details.Keys.All(WorkspaceEvent.AllowedDetails.Contains));
    }

    [Fact]
    public async Task TheSameEventArrivingTwiceIsStoredOnce()
    {
        var service = Service();

        var first = await service.RecordAsync(Bank, Loan(), EventSource.Recorded, null, "staff-1");
        var second = await service.RecordAsync(Bank, Loan(), EventSource.FileImport, "a.csv row 2", "staff-1");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Event.EventId, second.Event.EventId);
        Assert.Single(await service.ListAsync(Bank));
    }

    [Fact]
    public async Task TwoLoansToTheSamePersonOnTheSameDayAreTwoEvents_WhenTheBankGivesThemDifferentReferences()
    {
        var service = Service();

        await service.RecordAsync(Bank, Loan(externalId: "L-1"), EventSource.FileImport, null, "staff-1");
        await service.RecordAsync(Bank, Loan(externalId: "L-2"), EventSource.FileImport, null, "staff-1");

        Assert.Equal(2, (await service.ListAsync(Bank)).Count);
    }

    [Theory]
    [InlineData("nonsense", "+256790000001", "Unknown event type")]
    [InlineData("loan_disbursed", "12345", "phone number is not usable")]
    [InlineData("loan_disbursed", "", "phone number is not usable")]
    public async Task ABadTypeOrNumberIsRefusedWithAReasonAPersonCanAct(string type, string phone, string expected)
    {
        var error = await Assert.ThrowsAsync<EventException>(() =>
            Service().RecordAsync(Bank, new EventInput(type, phone, Now.AddDays(-1)), EventSource.Recorded, null, "staff-1"));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task ADateInTheFutureOrTooOldIsRefused()
    {
        var service = Service();

        await Assert.ThrowsAsync<EventException>(() => service.RecordAsync(Bank, Loan(daysAgo: -3), EventSource.Recorded, null, "s"));
        await Assert.ThrowsAsync<EventException>(() => service.RecordAsync(Bank, Loan(daysAgo: 900), EventSource.Recorded, null, "s"));
        await Assert.ThrowsAsync<EventException>(() => service.RecordAsync(Bank, new EventInput("loan_disbursed", "+256790000001", null), EventSource.Recorded, null, "s"));
    }

    [Fact]
    public async Task EventsBelongToTheirWorkspaceOnly()
    {
        var service = Service();
        var made = await service.RecordAsync(Bank, Loan(), EventSource.Recorded, null, "staff-1");

        Assert.Empty(await service.ListAsync(Other));
        Assert.Null(await service.GetAsync(Other, made.Event.EventId));
        Assert.NotNull(await service.GetAsync(Bank, made.Event.EventId));
    }

    [Fact]
    public async Task ProcessedEventsLeaveThePendingList_AndAreNotProcessedTwice()
    {
        var service = Service();
        var made = await service.RecordAsync(Bank, Loan(), EventSource.Recorded, null, "staff-1");

        await service.MarkProcessedAsync(Bank, made.Event.EventId);
        await service.MarkProcessedAsync(Bank, made.Event.EventId);

        Assert.Empty(await service.ListAsync(Bank, EventProcessingState.Pending));
        var done = Assert.Single(await service.ListAsync(Bank, EventProcessingState.Processed));
        Assert.Equal(Now, done.ProcessedAt);
    }

    [Fact]
    public async Task TheListIsNewestFirst_AndCanBeFilteredByType()
    {
        var service = Service();
        await service.RecordAsync(Bank, Loan(daysAgo: 5), EventSource.Recorded, null, "s");
        await service.RecordAsync(Bank, new EventInput("salary_account_opened", "+256790000002", Now.AddDays(-1)), EventSource.Recorded, null, "s");

        var all = await service.ListAsync(Bank);
        Assert.Equal(["salary_account_opened", "loan_disbursed"], all.Select(item => item.EventType).ToArray());
        Assert.Single(await service.ListAsync(Bank, eventType: "loan_disbursed"));
    }

    // ---- File import

    private const string Header = "external_id,phone,event_type,occurred_at,amount,term_months,branch";

    [Fact]
    public async Task AFileLoadsRowByRow_ReportsWhatItRefused_AndLoadsTheRest()
    {
        var csv = string.Join("\n",
            Header,
            "L-1,0790000001,loan_disbursed,2026-10-25,8000000,36,Ntinda",
            "L-2,0790000002,Salary account opened,26/10/2026,,,Kawempe",
            "L-3,not-a-number,loan_disbursed,2026-10-25,,,",
            "L-4,0790000004,mystery_event,2026-10-25,,,",
            "L-5,0790000005,loan_disbursed,2099-01-01,,,",
            "L-6,0790000006,loan_disbursed,2026-10-25,lots,,");

        var result = await Service().ImportCsvAsync(Bank, "daily.csv", csv, "staff-1");

        Assert.Equal(6, result.RowsRead);
        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal([4, 5, 6, 7], result.Rejected.Select(problem => problem.Row).ToArray());
        Assert.Contains("phone number", result.Rejected[0].Reason);
        Assert.Contains("future", result.Rejected[2].Reason);
    }

    [Fact]
    public async Task SendingTheSameFileAgainChangesNothing()
    {
        var service = Service();
        var csv = Header + "\nL-1,0790000001,loan_disbursed,2026-10-25,8000000,36,Ntinda";

        var first = await service.ImportCsvAsync(Bank, "daily.csv", csv, "staff-1");
        var second = await service.ImportCsvAsync(Bank, "daily.csv", csv, "staff-1");

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Duplicates);
        Assert.Single(await service.ListAsync(Bank));
    }

    [Fact]
    public async Task ColumnsTheServiceDoesNotAccept_AreNeverReadAndAreNamed()
    {
        var csv = "phone,event_type,occurred_at,balance,account_number\n0790000001,loan_disbursed,2026-10-25,12000000,0123456789";
        var service = Service();

        var result = await service.ImportCsvAsync(Bank, "daily.csv", csv, "staff-1");

        Assert.Equal(["balance", "account_number"], result.IgnoredColumns);
        var stored = Assert.Single(await service.ListAsync(Bank));
        Assert.DoesNotContain("balance", stored.Details.Keys);
        Assert.DoesNotContain("12000000", stored.Details.Values);
        Assert.DoesNotContain("0123456789", stored.Details.Values);
    }

    [Fact]
    public async Task AFileWithoutTheRequiredColumnsIsRefusedWhole_WithTheAcceptedNames()
    {
        var error = await Assert.ThrowsAsync<EventException>(() => Service().ImportCsvAsync(Bank, "x.csv", "name,amount\nA,5", "staff-1"));

        Assert.Contains("phone", error.Message);
    }

    [Fact]
    public async Task QuotedCellsAndAlternativeColumnNamesAreUnderstood()
    {
        var csv = "﻿msisdn,event,date,loan_amount,branch\n\"0790000001\",\"Loan disbursed\",2026-10-25,\"8,000,000\",\"Ntinda, Kampala\"";
        var service = Service();

        await service.ImportCsvAsync(Bank, "x.csv", csv, "staff-1");

        var stored = Assert.Single(await service.ListAsync(Bank));
        Assert.Equal(8_000_000m, stored.Amount);
        Assert.Equal("Ntinda, Kampala", stored.Details["branch"]);
    }

    [Fact]
    public async Task AnEmptyFileOrOneWithTooManyRowsIsRefused()
    {
        var service = Service();
        await Assert.ThrowsAsync<EventException>(() => service.ImportCsvAsync(Bank, "x.csv", "", "s"));
        var big = Header + string.Concat(Enumerable.Repeat("\nL,0790000001,loan_disbursed,2026-10-25,,,", EventService.MaxImportRows + 1));
        await Assert.ThrowsAsync<EventException>(() => service.ImportCsvAsync(Bank, "x.csv", big, "s"));
    }

    [Fact]
    public void EveryEventTypeHasAReasonAnOfferAndATiming_AndTypesAreFoundByCodeOrLabel()
    {
        Assert.All(BankEventCatalog.All, type =>
        {
            Assert.False(string.IsNullOrWhiteSpace(type.Reason));
            Assert.False(string.IsNullOrWhiteSpace(type.SuggestedOffer));
            Assert.False(string.IsNullOrWhiteSpace(type.SuggestedTiming));
        });
        Assert.Equal("school_fees_paid", BankEventCatalog.Find("School fees paid")!.Code);
        Assert.Equal("school_fees_paid", BankEventCatalog.Find("school-fees-paid")!.Code);
        Assert.Null(BankEventCatalog.Find("unknown"));
    }
}

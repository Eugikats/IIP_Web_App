using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProspectListImportServiceTests
{
    [Theory]
    [InlineData("0776 123 456", "256776123456")]
    [InlineData("+256 776 123 456", "256776123456")]
    [InlineData("256776123456", "256776123456")]
    [InlineData("776123456", "256776123456")]
    [InlineData("00256776123456", "256776123456")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData("not a number", null)]
    public void Phone_numbers_become_international_digits_or_are_rejected(string raw, string? expected)
    {
        Assert.Equal(expected, ProspectListImportService.NormalisePhone(raw));
    }

    [Theory]
    [InlineData("Yes", ContactConsentStatus.Agreed)]
    [InlineData("opted in", ContactConsentStatus.Agreed)]
    [InlineData("N", ContactConsentStatus.Declined)]
    [InlineData("unsubscribed", ContactConsentStatus.Withdrawn)]
    [InlineData("", ContactConsentStatus.NotRecorded)]
    [InlineData("maybe", ContactConsentStatus.NotRecorded)]
    [InlineData(null, ContactConsentStatus.NotRecorded)]
    public void Consent_is_only_agreed_when_the_list_clearly_says_so(string? raw, ContactConsentStatus expected)
    {
        Assert.Equal(expected, ProspectListImportService.ParseConsent(raw));
    }

    [Fact]
    public async Task Creates_prospects_marked_as_from_the_list_and_keeps_consent_as_the_list_states_it()
    {
        var store = new Store();
        var service = Service(store);

        var result = await service.ImportAsync(new ImportListRequest("Branch customers", [
            new ImportListRow("Aisha Namutebi", "0776123456", "Salon owner", "Ntinda", "Ntinda branch", "Current account", "Yes"),
            new ImportListRow("Brian Okello", "0700111222", Consent: "no"),
            new ImportListRow("Carol Akullo", "0700333444"),
        ]));

        Assert.Equal(3, result.Created);
        Assert.Equal(1, result.WithConsent);
        Assert.Equal(3, store.Leads.Count);
        var aisha = store.Leads.Single(lead => lead.CustomerName == "Aisha Namutebi");
        Assert.Equal("Imported list", aisha.LeadSource);
        Assert.Equal(ContactConsentStatus.Agreed, aisha.Consent!.Status);
        Assert.Equal("List upload", aisha.Consent.Channel);
        Assert.Contains(aisha.Facts, fact => fact.Key == "occupation" && fact.Value == "Salon owner" && fact.Source == ProspectFactSource.ImportedList);
        Assert.Equal(ContactConsentStatus.Declined, store.Leads.Single(lead => lead.CustomerName == "Brian Okello").Consent!.Status);
        Assert.Null(store.Leads.Single(lead => lead.CustomerName == "Carol Akullo").Consent);
    }

    [Fact]
    public async Task Rejects_rows_without_a_name_or_a_usable_number_and_says_which_row()
    {
        var store = new Store();
        var result = await Service(store).ImportAsync(new ImportListRequest("List", [
            new ImportListRow("", "0776123456"),
            new ImportListRow("No Number", "12"),
            new ImportListRow("Good Person", "0776123457"),
        ]));

        Assert.Equal(1, result.Created);
        Assert.Equal(2, result.Invalid);
        Assert.Contains(result.Problems, problem => problem.Row == 2 && problem.Reason == "No name.");
        Assert.Contains(result.Problems, problem => problem.Row == 3 && problem.Reason == "No usable phone number.");
    }

    [Fact]
    public async Task A_person_already_on_record_or_repeated_in_the_file_is_left_alone()
    {
        var store = new Store();
        var service = Service(store);
        await service.ImportAsync(new ImportListRequest("First", [new ImportListRow("Aisha", "0776123456")]));

        var second = await service.ImportAsync(new ImportListRequest("Second", [
            new ImportListRow("Aisha Again", "+256776123456"),
            new ImportListRow("New Person", "0700111222"),
            new ImportListRow("New Person Twice", "0700111222"),
        ]));

        Assert.Equal(1, second.Created);
        Assert.Equal(2, second.AlreadyKnown);
        Assert.Equal(2, store.Leads.Count);
        Assert.Equal("Aisha", store.Leads.Single(lead => lead.Phone == "256776123456").CustomerName);
    }

    [Fact]
    public async Task Spreadsheet_formulas_in_a_cell_are_defused()
    {
        var store = new Store();
        await Service(store).ImportAsync(new ImportListRequest("List", [
            new ImportListRow("=HYPERLINK(\"http://example.test\")", "0776123456", Occupation: "@SUM(A1)"),
        ]));

        var lead = Assert.Single(store.Leads);
        Assert.DoesNotMatch(@"^[=+@]", lead.CustomerName);
        Assert.DoesNotMatch(@"^[=+@]", lead.Facts.Single(fact => fact.Key == "occupation").Value);
    }

    [Fact]
    public async Task An_empty_or_oversized_list_is_refused_and_the_audit_trail_holds_counts_only()
    {
        var store = new Store();
        var service = Service(store);

        await Assert.ThrowsAsync<SalesLeadQueryValidationException>(() => service.ImportAsync(new ImportListRequest("List", [])));
        var tooMany = Enumerable.Range(0, ProspectListImportService.MaxRows + 1).Select(i => new ImportListRow($"P{i}", "0776123456")).ToList();
        await Assert.ThrowsAsync<SalesLeadQueryValidationException>(() => service.ImportAsync(new ImportListRequest("List", tooMany)));

        await service.ImportAsync(new ImportListRequest("Secret list", [new ImportListRow("Aisha Namutebi", "0776123456")]));
        var audit = Assert.Single(store.Audit);
        Assert.Equal("sales_lead.list_imported", audit.ActionName);
        Assert.DoesNotContain("Aisha", audit.AuditReason);
        Assert.DoesNotContain("0776", audit.AuditReason);
    }

    [Fact]
    public async Task Importing_needs_the_permission_to_manage_leads()
    {
        var service = new ProspectListImportService(new Ctx(), new Deny(), new Store(), new Store());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ImportAsync(new ImportListRequest("List", [new ImportListRow("A B", "0776123456")])));
    }

    private static ProspectListImportService Service(Store store) => new(new Ctx(), new Allow(), store, store);

    private sealed class Ctx : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class Allow : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Deny : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => throw new PermissionDeniedException(permission);
    }

    private sealed class Store : ISalesLeadWriteRepository, IAuditWriter
    {
        public List<SalesLeadDetailReadModel> Leads { get; } = [];
        public List<AuditEvent> Audit { get; } = [];

        public Task CreateAsync(string tenantId, string workspaceId, SalesLeadDetailReadModel lead, CancellationToken cancellationToken = default)
        {
            Leads.RemoveAll(existing => existing.LeadId == lead.LeadId);
            Leads.Add(lead);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default)
        {
            Leads.RemoveAll(existing => existing.LeadId == leadId);
            return Task.CompletedTask;
        }

        public Task<SalesLeadDetailReadModel?> FindByPhoneAsync(string tenantId, string workspaceId, string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Leads.FirstOrDefault(lead => lead.Phone == phone));

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Audit.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}

using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Saving an exported evidence pack to the person's own storage: only for people who may export, only a pack that is
/// really an export of this case, only into the person's own connection, and audited on the case without the contents.
/// </summary>
public sealed class EvidencePackSaveTests
{
    private static readonly Guid CaseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task AnExportedPackIsSavedToTheChosenProvider_AndTheCaseTrailRecordsWhoWhereAndTheFileName()
    {
        var rig = new Rig();

        var result = await rig.Save("microsoft", "packs/case-1.zip");

        Assert.Equal(StatusCodes.Status200OK, Status(result));
        var body = Body(result).GetProperty("saved");
        Assert.Equal("case-1-evidence.zip", body.GetProperty("name").GetString());
        var upload = Assert.Single(rig.Files.Uploads);
        Assert.Equal((FileProvider.Microsoft, "case-1-evidence.zip", "application/zip"), (upload.Provider, upload.Name, upload.ContentType));
        Assert.Equal(Rig.PackBytes, upload.Content);
        var audit = Assert.Single(rig.Audit.Events);
        Assert.Equal("evidence_pack.saved_to_storage", audit.ActionName);
        Assert.Equal(CaseId, audit.CaseId);
        Assert.Equal("user-officer", audit.UserId);
        Assert.Contains("OneDrive", audit.AuditReason);
        Assert.Contains("case-1-evidence.zip", audit.AuditReason);
    }

    [Fact]
    public async Task AnObjectKeyThatIsNotAnExportOfThisCase_IsNotFound_AndNothingIsUploaded()
    {
        var rig = new Rig { KnownKey = "packs/case-1.zip" };

        var result = await rig.Save("google", "packs/someone-elses.zip");

        Assert.Equal(StatusCodes.Status404NotFound, Status(result));
        Assert.Empty(rig.Files.Uploads);
        Assert.Empty(rig.Audit.Events);
    }

    [Fact]
    public async Task WithoutTheirOwnConnectedAccount_ItIsRefusedWithAReason_AndNothingIsAudited()
    {
        var rig = new Rig { Files = { Failure = new FileConnectionException("not_connected", "Connect your OneDrive account first.") } };

        var result = await rig.Save("microsoft", "packs/case-1.zip");

        Assert.Equal(StatusCodes.Status409Conflict, Status(result));
        Assert.Contains("Connect your OneDrive", Body(result).GetProperty("message").GetString());
        Assert.Empty(rig.Audit.Events);
    }

    [Fact]
    public async Task APackTooLargeToSaveFromHere_SaysSo()
    {
        var rig = new Rig { Files = { Failure = new FileConnectionException("too_large", "That file is larger than 25 MB.") } };
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Status(await rig.Save("google", "packs/case-1.zip")));
    }

    [Theory]
    [InlineData("dropbox", "packs/case-1.zip")]
    [InlineData("microsoft", "")]
    public async Task AnUnknownProviderOrAMissingKeyIsInvalid(string provider, string key)
    {
        var rig = new Rig();
        Assert.Equal(StatusCodes.Status400BadRequest, Status(await rig.Save(provider, key)));
        Assert.Empty(rig.Files.Uploads);
    }

    [Fact]
    public async Task APersonWhoMayNotExportEvidenceCannotSave()
    {
        var rig = new Rig { Role = "ClaimsOfficer" };

        var result = await rig.Save("microsoft", "packs/case-1.zip");

        Assert.Equal(StatusCodes.Status403Forbidden, Status(result));
        Assert.Empty(rig.Files.Uploads);
    }

    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 200;

    private static JsonElement Body(IResult result) => JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value);

    private sealed class Rig
    {
        public static readonly byte[] PackBytes = Encoding.UTF8.GetBytes("pack contents that must never reach the audit trail");

        public string Role { get; init; } = "Admin";
        public string KnownKey { get; init; } = "packs/case-1.zip";
        public FakeFiles Files { get; } = new();
        public FakeAudit Audit { get; } = new();
        private readonly TenantContextAccessor tenant = new();

        public Task<IResult> Save(string provider, string key)
        {
            var http = new DefaultHttpContext { TraceIdentifier = "trace-1" };
            http.Request.Headers["X-Tenant-Id"] = "tenant-demo";
            http.Request.Headers["X-Workspace-Id"] = "workspace-operations";
            http.Request.Headers["X-User-Id"] = "user-officer";
            http.Request.Headers["x-demo-user-id"] = "user-officer";
            http.Request.Headers["x-demo-role"] = Role;
            tenant.SetCurrent(new TenantContext("tenant-demo", "workspace-operations", "user-officer", [Role]));
            return EvidencePackSaveEndpoints.Save(
                CaseId, new EvidencePackSaveEndpoints.SaveRequest(key, provider), new FakeCompliance(KnownKey), Files, Audit, tenant, tenant,
                TimeProvider.System, new InfrastructureOptions { AuthMode = "Development" }, http, default);
        }
    }

    private sealed class FakeCompliance(string knownKey) : IComplianceQueryService
    {
        public Task<EvidencePackDownloadDto?> DownloadAuditPackAsync(Guid caseId, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<EvidencePackDownloadDto?>(caseId == CaseId && objectKey == knownKey
                ? new EvidencePackDownloadDto(new MemoryStream(Rig.PackBytes), "application/zip", "case-1-evidence.zip")
                : null);

        public Task<CompliancePostureDto> GetPostureAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(ComplianceAuditEventQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditPack?> GetAuditPackAsync(Guid caseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidencePackExportDto?> ExportAuditPackAsync(EvidencePackExportCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeFiles : IUserFileService
    {
        public List<(FileProvider Provider, string Name, string ContentType, byte[] Content)> Uploads { get; } = [];
        public FileConnectionException? Failure { get; set; }

        public Task<SavedFile> UploadAsync(FileProvider provider, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) return Task.FromException<SavedFile>(Failure);
            Uploads.Add((provider, fileName, contentType, content));
            return Task.FromResult(new SavedFile(fileName, "https://storage.example.test/file", provider == FileProvider.Google ? "google" : "microsoft"));
        }

        public Task<IReadOnlyList<FileConnectionStatus>> StatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileConnectStart> StartAsync(FileProvider provider, string? returnTo, bool includeSharePoint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileConnectResult> ExchangeAsync(FileProvider provider, string code, string state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisconnectAsync(FileProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileBrowseResult> BrowseAsync(FileProvider provider, string? folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DownloadedFile> DownloadAsync(FileProvider provider, string fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GooglePickerSession> PickerSessionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetAccessTokenAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeAudit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}

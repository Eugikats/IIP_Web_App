$ErrorActionPreference = "Stop"

$BaseUrl = "http://localhost:5167"
$Results = [System.Collections.Generic.List[object]]::new()

function New-AuthHeaders($tenantId, $workspaceId, $userId, $role) {
    @{
        "X-Tenant-Id" = $tenantId
        "X-Workspace-Id" = $workspaceId
        "X-User-Id" = $userId
        "x-demo-user-id" = $userId
        "X-Demo-Role" = $role
    }
}

function Test-ValidGuid([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $parsed = [guid]::Empty
    return [guid]::TryParse($Value, [ref]$parsed) -and $parsed -ne [guid]::Empty
}

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers,
        [object]$Body = $null
    )

    if ($Path -match "/cases//" -or $Path -match "/documents/$" -or $Path -match "/approvals/$") {
        throw "Refusing malformed path: $Path"
    }

    $curlArgs = @("-sS", "-D", "-", "-w", "`n__STATUS__=%{http_code}`n", "-X", $Method, "$BaseUrl$Path")
    foreach ($h in $Headers.GetEnumerator()) {
        $curlArgs += @("-H", "$($h.Key): $($h.Value)")
    }

    if ($null -ne $Body) {
        $curlArgs += @("-H", "Content-Type: application/json", "--data", ($Body | ConvertTo-Json -Compress -Depth 20))
    }

    $raw = & curl.exe @curlArgs 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "curl.exe failed while calling $Method $Path"
    }

    $lines = $raw -split "`r?`n"
    $status = $null
    $contentLines = @()
    $inBody = $false

    foreach ($line in $lines) {
        if ($line -match "^__STATUS__=(\d{3})$") {
            $status = [int]$Matches[1]
            continue
        }

        if (-not $inBody) {
            if ($line -match "^HTTP/") { continue }
            if ($line.Trim() -eq "") { $inBody = $true; continue }
            continue
        }

        $contentLines += $line
    }

    [pscustomobject]@{
        StatusCode = $status
        Body = ($contentLines -join "`n").Trim()
    }
}

function Invoke-MultipartUpload {
    param(
        [string]$Path,
        [hashtable]$Headers,
        [string]$FilePath,
        [string]$UploadedByUserId
    )

    if (-not (Test-ValidGuid (($Path -split "/")[4]))) {
        throw "Refusing document upload with invalid case id in path: $Path"
    }

    $curlArgs = @("-sS", "-D", "-", "-w", "`n__STATUS__=%{http_code}`n", "-X", "POST", "$BaseUrl$Path")
    foreach ($h in $Headers.GetEnumerator()) {
        $curlArgs += @("-H", "$($h.Key): $($h.Value)")
    }
    $curlArgs += @("-F", "files=@$FilePath", "-F", "uploadedByUserId=$UploadedByUserId")

    $raw = & curl.exe @curlArgs 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "curl.exe failed while uploading file for $Path"
    }

    $lines = $raw -split "`r?`n"
    $status = $null
    $contentLines = @()
    $inBody = $false
    foreach ($line in $lines) {
        if ($line -match "^__STATUS__=(\d{3})$") { $status = [int]$Matches[1]; continue }
        if (-not $inBody) {
            if ($line -match "^HTTP/") { continue }
            if ($line.Trim() -eq "") { $inBody = $true; continue }
            continue
        }
        $contentLines += $line
    }

    [pscustomobject]@{
        StatusCode = $status
        Body = ($contentLines -join "`n").Trim()
    }
}

function Get-JsonValue {
    param([object]$Object, [string[]]$CandidateNames)
    foreach ($name in $CandidateNames) {
        if ($null -ne $Object -and $Object.PSObject.Properties.Match($name).Count -gt 0) {
            return $Object.$name
        }
    }
    return $null
}

function Write-SetupResult {
    param([string]$Label, [object]$Response)
    Write-Host "$Label"
    Write-Host "  HTTP status: $($Response.StatusCode)"
    if ($Response.StatusCode -lt 200 -or $Response.StatusCode -ge 300) {
        Write-Host "  Response body:"
        Write-Host "  $($Response.Body)"
    }
}

function Require-SetupResource {
    param(
        [string]$Label,
        [object]$Response,
        [string]$Id,
        [int[]]$AllowedStatus = @(200, 201)
    )

    Write-SetupResult -Label $Label -Response $Response

    if ($AllowedStatus -notcontains $Response.StatusCode) {
        throw "$Label failed with HTTP $($Response.StatusCode). Cannot continue tenant isolation verification."
    }

    if (-not (Test-ValidGuid $Id)) {
        throw "$Label returned invalid resource id '$Id'. Cannot continue tenant isolation verification."
    }

    return $Id
}

function Add-TestResult {
    param(
        [string]$Test,
        [string]$Direction,
        [int]$ExpectedDeniedStatus,
        [object]$Response,
        [string]$Notes = ""
    )

    $denied = @(
        $Response.StatusCode -eq 404,
        $Response.StatusCode -eq 403,
        $Response.StatusCode -eq 401,
        $Response.StatusCode -eq 409
    ) -contains $true

    $passed = $denied
    $Results.Add([pscustomobject]@{
        Test = $Test
        Direction = $Direction
        Expected = "Denied (404/403/401/409)"
        Actual = "HTTP $($Response.StatusCode)"
        Result = $(if ($passed) { "PASS" } else { "FAIL" })
        Notes = $Notes
    }) | Out-Null

    return $passed
}

function Log-CrossTenantAttempt {
    param(
        [string]$Label,
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers,
        [object]$Body = $null
    )

    Write-Host ""
    Write-Host "======================================="
    Write-Host $Label
    Write-Host "Method: $Method  Path: $Path"
    Write-Host "Actor tenant=$($Headers['X-Tenant-Id']) workspace=$($Headers['X-Workspace-Id'])"
    $resp = if ($null -ne $Body) {
        Invoke-JsonApi -Method $Method -Path $Path -Headers $Headers -Body $Body
    } else {
        Invoke-JsonApi -Method $Method -Path $Path -Headers $Headers
    }
    Write-Host "HTTP status: $($resp.StatusCode)"
    if ($resp.Body) { Write-Host "Response body: $($resp.Body)" }
    Write-Host "======================================="
    return $resp
}

# --- Setup ---
$TenantA = New-AuthHeaders -tenantId "tenant-a" -workspaceId "workspace-a" -userId "user-a" -role "Admin"
$TenantB = New-AuthHeaders -tenantId "tenant-b" -workspaceId "workspace-b" -userId "user-b" -role "Admin"

Write-Host "Tenant isolation verification against $BaseUrl"
Write-Host ""

$health = Invoke-JsonApi -Method "GET" -Path "/health" -Headers $TenantA
Write-Host "Health check: HTTP $($health.StatusCode)"
if ($health.StatusCode -ne 200) {
    throw "API is not healthy at $BaseUrl. Start the API before running this script."
}

$casePayload = {
    param($tenantLabel)
    @{
        customerName = "$tenantLabel Customer"
        customerEmail = "$($tenantLabel.ToLower())@example.com"
        customerPhone = "+256700000001"
        policyNumber = "POL-$tenantLabel-001"
        productLine = "Motor"
        claimType = "Accident"
        channel = "WebPortal"
        incidentDate = (Get-Date).AddDays(-2).ToString("yyyy-MM-dd")
        incidentDescription = "$tenantLabel isolation verification case"
    }
}

Write-Host "--- SETUP: Tenant A case ---"
$caseAResp = Invoke-JsonApi -Method "POST" -Path "/api/v1/cases" -Headers $TenantA -Body (& $casePayload "TenantA")
$caseAJson = if ($caseAResp.Body) { $caseAResp.Body | ConvertFrom-Json } else { $null }
$caseAId = Require-SetupResource -Label "Create case (Tenant A)" -Response $caseAResp -Id (Get-JsonValue -Object $caseAJson -CandidateNames @("caseId"))

Write-Host "--- SETUP: Tenant B case ---"
$caseBResp = Invoke-JsonApi -Method "POST" -Path "/api/v1/cases" -Headers $TenantB -Body (& $casePayload "TenantB")
$caseBJson = if ($caseBResp.Body) { $caseBResp.Body | ConvertFrom-Json } else { $null }
$caseBId = Require-SetupResource -Label "Create case (Tenant B)" -Response $caseBResp -Id (Get-JsonValue -Object $caseBJson -CandidateNames @("caseId"))

Write-Host ""
Write-Host "Case A ID: $caseAId"
Write-Host "Case B ID: $caseBId"

# --- List isolation ---
Write-Host ""
Write-Host "===== LIST ISOLATION ====="
$listA = Invoke-JsonApi -Method "GET" -Path "/api/v1/cases" -Headers $TenantA
$listB = Invoke-JsonApi -Method "GET" -Path "/api/v1/cases" -Headers $TenantB
$listAJson = if ($listA.Body) { $listA.Body | ConvertFrom-Json } else { @() }
$listBJson = if ($listB.Body) { $listB.Body | ConvertFrom-Json } else { @() }
$listAIds = @($listAJson | ForEach-Object { $_.caseId })
$listBIds = @($listBJson | ForEach-Object { $_.caseId })

Write-Host "Tenant A list (HTTP $($listA.StatusCode)): $($listAIds -join ', ')"
Write-Host "Tenant B list (HTTP $($listB.StatusCode)): $($listBIds -join ', ')"

$listAOk = ($listA.StatusCode -eq 200) -and ($listAIds -contains $caseAId) -and ($listAIds -notcontains $caseBId)
$listBOk = ($listB.StatusCode -eq 200) -and ($listBIds -contains $caseBId) -and ($listBIds -notcontains $caseAId)
Write-Host "List isolation Tenant A: $(if ($listAOk) { 'PASS' } else { 'FAIL' })"
Write-Host "List isolation Tenant B: $(if ($listBOk) { 'PASS' } else { 'FAIL' })"

# --- Cross-tenant case access ---
Write-Host ""
Write-Host "===== CROSS-TENANT CASE ACCESS ====="

$r = Log-CrossTenantAttempt -Label "Tenant B must NOT read Tenant A case" -Method "GET" -Path "/api/v1/cases/$caseAId" -Headers $TenantB
Add-TestResult -Test "Get case" -Direction "A -> B (B reads A)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

$r = Log-CrossTenantAttempt -Label "Tenant A must NOT read Tenant B case" -Method "GET" -Path "/api/v1/cases/$caseBId" -Headers $TenantA
Add-TestResult -Test "Get case" -Direction "B -> A (A reads B)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

$r = Log-CrossTenantAttempt -Label "Tenant B must NOT assign Tenant A case" -Method "POST" -Path "/api/v1/cases/$caseAId/assign" -Headers $TenantB -Body @{
    assignedUserId = "user-b"
    assignedTeam = "TenantB-Claims"
    auditReason = "cross-tenant assign attempt"
}
Add-TestResult -Test "Assign case" -Direction "A -> B (B assigns A)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

$r = Log-CrossTenantAttempt -Label "Tenant A must NOT assign Tenant B case" -Method "POST" -Path "/api/v1/cases/$caseBId/assign" -Headers $TenantA -Body @{
    assignedUserId = "user-a"
    assignedTeam = "TenantA-Claims"
    auditReason = "cross-tenant assign attempt"
}
Add-TestResult -Test "Assign case" -Direction "B -> A (A assigns B)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

$r = Log-CrossTenantAttempt -Label "Tenant B must NOT escalate Tenant A case" -Method "POST" -Path "/api/v1/cases/$caseAId/escalate" -Headers $TenantB -Body @{
    auditReason = "cross-tenant escalate attempt"
}
Add-TestResult -Test "Escalate case" -Direction "A -> B (B escalates A)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

$r = Log-CrossTenantAttempt -Label "Tenant A must NOT escalate Tenant B case" -Method "POST" -Path "/api/v1/cases/$caseBId/escalate" -Headers $TenantA -Body @{
    auditReason = "cross-tenant escalate attempt"
}
Add-TestResult -Test "Escalate case" -Direction "B -> A (A escalates B)" -ExpectedDeniedStatus 404 -Response $r | Out-Null

# --- Optional: documents ---
$docAId = $null
$docBId = $null
$docStatusA = "BLOCKED"
$docStatusB = "BLOCKED"

try {
    $docFileA = Join-Path $env:TEMP "tenant-a-evidence.txt"
    "Tenant A evidence." | Set-Content -Path $docFileA -Encoding utf8
    $docAUpload = Invoke-MultipartUpload -Path "/api/v1/cases/$caseAId/documents" -Headers $TenantA -FilePath $docFileA -UploadedByUserId "user-a"
    Write-SetupResult -Label "Create document (Tenant A)" -Response $docAUpload
    if ($docAUpload.StatusCode -ge 200 -and $docAUpload.StatusCode -lt 300) {
        $docAJson = $docAUpload.Body | ConvertFrom-Json
        $docAId = Get-JsonValue -Object $docAJson -CandidateNames @("documentId")
        if ($null -eq $docAId -and $docAJson.documents) {
            $docAId = Get-JsonValue -Object $docAJson.documents[0] -CandidateNames @("documentId")
        }
        if (Test-ValidGuid $docAId) { $docStatusA = "Created $docAId" }
    }

    $docFileB = Join-Path $env:TEMP "tenant-b-evidence.txt"
    "Tenant B evidence." | Set-Content -Path $docFileB -Encoding utf8
    $docBUpload = Invoke-MultipartUpload -Path "/api/v1/cases/$caseBId/documents" -Headers $TenantB -FilePath $docFileB -UploadedByUserId "user-b"
    Write-SetupResult -Label "Create document (Tenant B)" -Response $docBUpload
    if ($docBUpload.StatusCode -ge 200 -and $docBUpload.StatusCode -lt 300) {
        $docBJson = $docBUpload.Body | ConvertFrom-Json
        $docBId = Get-JsonValue -Object $docBJson -CandidateNames @("documentId")
        if ($null -eq $docBId -and $docBJson.documents) {
            $docBId = Get-JsonValue -Object $docBJson.documents[0] -CandidateNames @("documentId")
        }
        if (Test-ValidGuid $docBId) { $docStatusB = "Created $docBId" }
    }

    if ((Test-ValidGuid $docAId) -and (Test-ValidGuid $docBId)) {
        $r = Log-CrossTenantAttempt -Label "Tenant B must NOT list Tenant A documents" -Method "GET" -Path "/api/v1/cases/$caseAId/documents" -Headers $TenantB
        Add-TestResult -Test "List documents" -Direction "A -> B" -Response $r | Out-Null
        $r = Log-CrossTenantAttempt -Label "Tenant A must NOT list Tenant B documents" -Method "GET" -Path "/api/v1/cases/$caseBId/documents" -Headers $TenantA
        Add-TestResult -Test "List documents" -Direction "B -> A" -Response $r | Out-Null
    }
}
catch {
    Write-Host "Document setup/tests skipped: $($_.Exception.Message)"
}

# --- Summary ---
Write-Host ""
Write-Host "===== SUMMARY ====="
Write-Host "Setup:"
Write-Host "  Case A: $caseAId (HTTP $($caseAResp.StatusCode))"
Write-Host "  Case B: $caseBId (HTTP $($caseBResp.StatusCode))"
Write-Host "  Document A: $docStatusA"
Write-Host "  Document B: $docStatusB"
Write-Host ""
Write-Host "Cross-tenant tests:"
$Results | Format-Table -AutoSize

$failures = @($Results | Where-Object { $_.Result -eq "FAIL" })
$hasCrossTenantFailure = $failures.Count -gt 0
$hasListIsolationFailure = (-not $listAOk) -or (-not $listBOk)
if ($hasCrossTenantFailure -or $hasListIsolationFailure) {
    Write-Host "VERDICT: FAIL - cross-tenant access or list leakage detected"
    exit 1
}

Write-Host "VERDICT: PASS - tenant isolation verified for case access and list filtering"
exit 0


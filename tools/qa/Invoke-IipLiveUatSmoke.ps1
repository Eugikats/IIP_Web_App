param(
    [string]$ApiBaseUrl = "https://app-api-dev-h5girbnzrayqe.azurewebsites.net",
    [string]$PortalBaseUrl = "https://app-portal-dev-h5girbnzrayqe.azurewebsites.net",
    [string]$Scope = "api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user",
    [string]$AccessToken = "",
    [string]$ExpectedRole = "",
    [string]$ExpectedTenantId = "",
    [string]$ExpectedWorkspaceId = "",
    [string]$ExpectedMeSource = "",
    [string]$DeniedApiPath = "",
    [switch]$IncludeClaimsList,
    [switch]$IncludeProviderStatus,
    [switch]$IncludeCompliancePosture,
    [string]$OutputPath = "",
    [int]$TimeoutSeconds = 30,
    [switch]$SkipAzToken,
    [switch]$RequireAuthenticated
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function New-Result {
    param(
        [string]$Name,
        [string]$Result,
        [string]$Detail,
        [int]$StatusCode = 0
    )

    [pscustomobject]@{
        Check = $Name
        Result = $Result
        Detail = $Detail
        StatusCode = $StatusCode
    }
}

function Invoke-HttpStatus {
    param(
        [string]$Uri,
        [hashtable]$Headers = @{}
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    try {
        foreach ($key in $Headers.Keys) {
            [void]$client.DefaultRequestHeaders.TryAddWithoutValidation($key, [string]$Headers[$key])
        }

        $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
        return [int]$response.StatusCode
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Invoke-HttpJson {
    param(
        [string]$Uri,
        [hashtable]$Headers = @{}
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    try {
        foreach ($key in $Headers.Keys) {
            [void]$client.DefaultRequestHeaders.TryAddWithoutValidation($key, [string]$Headers[$key])
        }

        $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $body = $null
        if (-not [string]::IsNullOrWhiteSpace($content)) {
            try {
                $body = $content | ConvertFrom-Json
            }
            catch {
                $body = $content
            }
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $body
            RawBody = $content
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

$results = New-Object System.Collections.Generic.List[object]
$failed = $false

function Add-Pass {
    param([string]$Name, [string]$Detail, [int]$StatusCode = 0)
    $script:results.Add((New-Result -Name $Name -Result "PASS" -Detail $Detail -StatusCode $StatusCode))
}

function Add-Fail {
    param([string]$Name, [string]$Detail, [int]$StatusCode = 0)
    $script:failed = $true
    $script:results.Add((New-Result -Name $Name -Result "FAIL" -Detail $Detail -StatusCode $StatusCode))
}

function Add-Blocked {
    param([string]$Name, [string]$Detail, [int]$StatusCode = 0)
    if ($RequireAuthenticated) {
        $script:failed = $true
        $script:results.Add((New-Result -Name $Name -Result "FAIL" -Detail $Detail -StatusCode $StatusCode))
    }
    else {
        $script:results.Add((New-Result -Name $Name -Result "BLOCKED" -Detail $Detail -StatusCode $StatusCode))
    }
}

$api = $ApiBaseUrl.TrimEnd("/")
$portal = $PortalBaseUrl.TrimEnd("/")

try {
    $readyStatus = Invoke-HttpStatus -Uri "$api/health/ready"
    if ($readyStatus -eq 200) {
        Add-Pass "API readiness" "GET $api/health/ready returned 200."
    }
    else {
        Add-Fail "API readiness" "GET $api/health/ready returned $readyStatus; expected 200."
    }
}
catch {
    Add-Fail "API readiness" $_.Exception.Message
}

try {
    $meUnauthStatus = Invoke-HttpStatus -Uri "$api/api/v1/me"
    if ($meUnauthStatus -eq 401) {
        Add-Pass "Unauthenticated /me denial" "GET $api/api/v1/me returned 401."
    }
    else {
        Add-Fail "Unauthenticated /me denial" "GET $api/api/v1/me returned $meUnauthStatus; expected 401."
    }
}
catch {
    Add-Fail "Unauthenticated /me denial" $_.Exception.Message
}

if ($portal) {
    try {
        $portalStatus = Invoke-HttpStatus -Uri "$portal/app?smoke=uat-live"
        if ($portalStatus -eq 200) {
            Add-Pass "Portal app route" "GET $portal/app?smoke=uat-live returned 200."
        }
        else {
            Add-Fail "Portal app route" "GET $portal/app?smoke=uat-live returned $portalStatus; expected 200."
        }
    }
    catch {
        Add-Fail "Portal app route" $_.Exception.Message
    }
}

$token = $AccessToken
if (-not $token -and -not $SkipAzToken) {
    try {
        $azTokenJson = & az account get-access-token --scope $Scope --output json 2>&1
        if ($LASTEXITCODE -eq 0) {
            $token = (($azTokenJson | Out-String) | ConvertFrom-Json).accessToken
        }
        else {
            Add-Blocked "Acquire user access token" (($azTokenJson | Out-String).Trim())
        }
    }
    catch {
        Add-Blocked "Acquire user access token" $_.Exception.Message
    }
}

if ($token) {
    try {
        $meResponse = Invoke-HttpJson -Uri "$api/api/v1/me" -Headers @{ Authorization = "Bearer $token" }
        if ($meResponse.StatusCode -eq 200) {
            $meBody = $meResponse.Body
            $detailParts = @("GET $api/api/v1/me returned 200 with bearer token.")

            if ($ExpectedMeSource -and $meBody.source -ne $ExpectedMeSource) {
                Add-Fail "Authenticated /me" "Expected source '$ExpectedMeSource' but received '$($meBody.source)'." $meResponse.StatusCode
            }
            elseif ($ExpectedRole -and $meBody.role -ne $ExpectedRole) {
                Add-Fail "Authenticated /me" "Expected role '$ExpectedRole' but received '$($meBody.role)'." $meResponse.StatusCode
            }
            elseif ($ExpectedTenantId -and $meBody.tenantId -ne $ExpectedTenantId) {
                Add-Fail "Authenticated /me" "Expected tenant '$ExpectedTenantId' but received '$($meBody.tenantId)'." $meResponse.StatusCode
            }
            elseif ($ExpectedWorkspaceId -and $meBody.workspaceId -ne $ExpectedWorkspaceId) {
                Add-Fail "Authenticated /me" "Expected workspace '$ExpectedWorkspaceId' but received '$($meBody.workspaceId)'." $meResponse.StatusCode
            }
            else {
                $detailParts += "source=$($meBody.source)"
                $detailParts += "role=$($meBody.role)"
                $detailParts += "tenant=$($meBody.tenantId)"
                $detailParts += "workspace=$($meBody.workspaceId)"
                Add-Pass "Authenticated /me" ($detailParts -join "; ") $meResponse.StatusCode
            }

            if ($IncludeClaimsList) {
                $claimsResponse = Invoke-HttpJson -Uri "$api/api/v1/claims" -Headers @{ Authorization = "Bearer $token" }
                if ($claimsResponse.StatusCode -eq 200) {
                    $count = @($claimsResponse.Body).Count
                    Add-Pass "Authenticated claims list" "GET $api/api/v1/claims returned 200 with $count records." $claimsResponse.StatusCode
                }
                else {
                    Add-Fail "Authenticated claims list" "GET $api/api/v1/claims returned $($claimsResponse.StatusCode); expected 200." $claimsResponse.StatusCode
                }
            }

            if ($IncludeProviderStatus) {
                $providerResponse = Invoke-HttpJson -Uri "$api/api/v1/provider-status" -Headers @{ Authorization = "Bearer $token" }
                if ($providerResponse.StatusCode -eq 200) {
                    Add-Pass "Provider status" "GET $api/api/v1/provider-status returned 200." $providerResponse.StatusCode
                }
                else {
                    Add-Fail "Provider status" "GET $api/api/v1/provider-status returned $($providerResponse.StatusCode); expected 200." $providerResponse.StatusCode
                }
            }

            if ($IncludeCompliancePosture) {
                $complianceResponse = Invoke-HttpJson -Uri "$api/api/v1/compliance/posture" -Headers @{ Authorization = "Bearer $token" }
                if ($complianceResponse.StatusCode -eq 200) {
                    Add-Pass "Compliance posture" "GET $api/api/v1/compliance/posture returned 200." $complianceResponse.StatusCode
                }
                else {
                    Add-Fail "Compliance posture" "GET $api/api/v1/compliance/posture returned $($complianceResponse.StatusCode); expected 200 for the selected authenticated role/evidence run." $complianceResponse.StatusCode
                }
            }

            if ($DeniedApiPath) {
                $deniedPath = $DeniedApiPath
                if (-not $deniedPath.StartsWith("/")) {
                    $deniedPath = "/" + $deniedPath
                }

                $deniedResponse = Invoke-HttpJson -Uri "$api$deniedPath" -Headers @{ Authorization = "Bearer $token" }
                if ($deniedResponse.StatusCode -in 401, 403) {
                    Add-Pass "Denied route guard" "GET $api$deniedPath returned $($deniedResponse.StatusCode), which is a safe deny outcome." $deniedResponse.StatusCode
                }
                else {
                    Add-Fail "Denied route guard" "GET $api$deniedPath returned $($deniedResponse.StatusCode); expected 401 or 403." $deniedResponse.StatusCode
                }
            }
        }
        else {
            Add-Fail "Authenticated /me" "GET $api/api/v1/me returned $($meResponse.StatusCode); expected 200." $meResponse.StatusCode
        }
    }
    catch {
        Add-Fail "Authenticated /me" $_.Exception.Message
    }
}
elseif ($SkipAzToken) {
    Add-Blocked "Authenticated /me" "No access token supplied and -SkipAzToken was set."
}

$results | Format-Table -AutoSize

if ($OutputPath) {
    $report = [ordered]@{
        generatedAt = [DateTimeOffset]::UtcNow.ToString("O")
        apiBaseUrl = $api
        portalBaseUrl = $portal
        requireAuthenticated = $RequireAuthenticated.IsPresent
        includeClaimsList = $IncludeClaimsList.IsPresent
        includeProviderStatus = $IncludeProviderStatus.IsPresent
        includeCompliancePosture = $IncludeCompliancePosture.IsPresent
        expectedRole = $ExpectedRole
        expectedMeSource = $ExpectedMeSource
        expectedTenantId = $ExpectedTenantId
        expectedWorkspaceId = $ExpectedWorkspaceId
        deniedApiPath = $DeniedApiPath
        results = $results.ToArray()
        passed = (-not $failed)
    }
    $directory = Split-Path -Parent $OutputPath
    if ($directory) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $report | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputPath -Encoding utf8
}

if ($failed) {
    exit 1
}

exit 0

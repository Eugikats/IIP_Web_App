param(
    [string]$ApiBaseUrl = "https://app-api-dev-h5girbnzrayqe.azurewebsites.net",
    [string]$PortalBaseUrl = "https://app-portal-dev-h5girbnzrayqe.azurewebsites.net",
    [string]$Scope = "api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user",
    [string]$AccessToken = "",
    [switch]$SkipAzToken,
    [switch]$RequireAuthenticated
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function New-Result {
    param(
        [string]$Name,
        [string]$Result,
        [string]$Detail
    )

    [pscustomobject]@{
        Check = $Name
        Result = $Result
        Detail = $Detail
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

$results = New-Object System.Collections.Generic.List[object]
$failed = $false

function Add-Pass {
    param([string]$Name, [string]$Detail)
    $script:results.Add((New-Result -Name $Name -Result "PASS" -Detail $Detail))
}

function Add-Fail {
    param([string]$Name, [string]$Detail)
    $script:failed = $true
    $script:results.Add((New-Result -Name $Name -Result "FAIL" -Detail $Detail))
}

function Add-Blocked {
    param([string]$Name, [string]$Detail)
    if ($RequireAuthenticated) {
        $script:failed = $true
        $script:results.Add((New-Result -Name $Name -Result "FAIL" -Detail $Detail))
    }
    else {
        $script:results.Add((New-Result -Name $Name -Result "BLOCKED" -Detail $Detail))
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
        $authStatus = Invoke-HttpStatus -Uri "$api/api/v1/me" -Headers @{ Authorization = "Bearer $token" }
        if ($authStatus -eq 200) {
            Add-Pass "Authenticated /me" "GET $api/api/v1/me returned 200 with bearer token."
        }
        else {
            Add-Fail "Authenticated /me" "GET $api/api/v1/me returned $authStatus; expected 200."
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

if ($failed) {
    exit 1
}

exit 0

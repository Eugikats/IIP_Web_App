[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ApiDisplayName = "iip-api-platform",
    [string]$PortalDisplayName = "iip-operations-portal",
    [string[]]$PortalRedirectUris = @(
        "http://localhost:3000",
        "https://app-portal-dev-h5girbnzrayqe.azurewebsites.net",
        "https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app"
    ),
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-AzJson {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $raw = & az @Arguments --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI failed: az $($Arguments -join ' ')"
    }

    if ([string]::IsNullOrWhiteSpace(($raw -join "`n"))) {
        return $null
    }

    return ($raw -join "`n") | ConvertFrom-Json
}

function Invoke-GraphPatch {
    param(
        [Parameter(Mandatory)][string]$ApplicationObjectId,
        [Parameter(Mandatory)][hashtable]$Body
    )

    $tempPath = [System.IO.Path]::GetTempFileName()
    try {
        $Body | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $tempPath -Encoding utf8NoBOM
        & az rest `
            --method PATCH `
            --uri "https://graph.microsoft.com/v1.0/applications/$ApplicationObjectId" `
            --headers "Content-Type=application/json" `
            --body "@$tempPath" `
            --only-show-errors `
            --output none
        if ($LASTEXITCODE -ne 0) {
            throw "Microsoft Graph update failed for application object $ApplicationObjectId."
        }
    }
    finally {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-SingleApplication {
    param([Parameter(Mandatory)][string]$DisplayName)

    $matches = @(Invoke-AzJson -Arguments @("ad", "app", "list", "--display-name", $DisplayName))
    if ($matches.Count -gt 1) {
        throw "More than one app registration is named '$DisplayName'. Refusing to guess which object to update."
    }
    return $matches | Select-Object -First 1
}

function Ensure-Application {
    param([Parameter(Mandatory)][string]$DisplayName)

    $existing = Get-SingleApplication -DisplayName $DisplayName
    if ($null -ne $existing) {
        return Invoke-AzJson -Arguments @("ad", "app", "show", "--id", $existing.appId)
    }

    return Invoke-AzJson -Arguments @(
        "ad", "app", "create",
        "--display-name", $DisplayName,
        "--sign-in-audience", "AzureADMultipleOrgs"
    )
}

function Ensure-ServicePrincipal {
    param([Parameter(Mandatory)][string]$ApplicationId)

    $existing = & az ad sp show --id $ApplicationId --only-show-errors --output json 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($existing -join "`n"))) {
        return ($existing -join "`n") | ConvertFrom-Json
    }

    return Invoke-AzJson -Arguments @("ad", "sp", "create", "--id", $ApplicationId)
}

$account = Invoke-AzJson -Arguments @("account", "show")
if ($null -eq $account -or [string]::IsNullOrWhiteSpace($account.tenantId)) {
    throw "Sign in to Azure CLI before running this script."
}

$plan = [ordered]@{
    tenantId = $account.tenantId
    subscriptionId = $account.id
    apiDisplayName = $ApiDisplayName
    portalDisplayName = $PortalDisplayName
    signInAudience = "AzureADMultipleOrgs"
    apiRoles = @("IIP.Access", "IIP.Admin", "IIP.ReadOnly")
    delegatedScope = "access_as_user"
    portalRedirectUris = $PortalRedirectUris
    createsSecrets = $false
    mutatesExistingLegacyRegistrations = $false
}

if (-not $Apply) {
    Write-Host "Preview only. Re-run with -Apply to create or reconcile the canonical IIP registrations."
    $plan | ConvertTo-Json -Depth 10
    return
}

if (-not $PSCmdlet.ShouldProcess($account.tenantId, "Create or reconcile canonical multitenant IIP app registrations")) {
    return
}

$apiApp = Ensure-Application -DisplayName $ApiDisplayName
$existingScope = @($apiApp.api.oauth2PermissionScopes) | Where-Object value -eq "access_as_user" | Select-Object -First 1
$scopeId = if ($null -ne $existingScope) { [Guid]$existingScope.id } else { [Guid]::NewGuid() }

$apiBody = @{
    signInAudience = "AzureADMultipleOrgs"
    identifierUris = @("api://$($apiApp.appId)")
    groupMembershipClaims = $null
    api = @{
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes = @(
            @{
                adminConsentDescription = "Allow IIP Operations to access the IIP API as the signed-in user. Fine-grained insurance permissions remain in IIP."
                adminConsentDisplayName = "Access IIP Operations API"
                id = $scopeId
                isEnabled = $true
                type = "User"
                userConsentDescription = "Allow IIP Operations to access the IIP API on your behalf."
                userConsentDisplayName = "Access IIP Operations API"
                value = "access_as_user"
            }
        )
    }
    appRoles = @(
        @{
            allowedMemberTypes = @("User")
            description = "Coarse entry gate for approved IIP users. Fine-grained authority remains in IIP."
            displayName = "IIP Access"
            id = "d7d73992-191a-4d9a-9a84-267cf40886f1"
            isEnabled = $true
            value = "IIP.Access"
        },
        @{
            allowedMemberTypes = @("User")
            description = "Coarse entry gate for IIP tenant administrators. Fine-grained authority remains in IIP."
            displayName = "IIP Admin"
            id = "ab29e6f4-b42a-4f5a-9bf8-7c9f38e2941d"
            isEnabled = $true
            value = "IIP.Admin"
        },
        @{
            allowedMemberTypes = @("User")
            description = "Coarse read-only entry gate. Fine-grained authority remains in IIP."
            displayName = "IIP Read Only"
            id = "6e27f514-0e53-4e02-9afd-3a289b9c3930"
            isEnabled = $true
            value = "IIP.ReadOnly"
        }
    )
}
Invoke-GraphPatch -ApplicationObjectId $apiApp.id -Body $apiBody
$apiApp = Invoke-AzJson -Arguments @("ad", "app", "show", "--id", $apiApp.appId)

$portalApp = Ensure-Application -DisplayName $PortalDisplayName
$portalBody = @{
    signInAudience = "AzureADMultipleOrgs"
    spa = @{ redirectUris = @($PortalRedirectUris | Sort-Object -Unique) }
    requiredResourceAccess = @(
        @{
            resourceAppId = $apiApp.appId
            resourceAccess = @(@{ id = $scopeId; type = "Scope" })
        }
    )
}
Invoke-GraphPatch -ApplicationObjectId $portalApp.id -Body $portalBody
$portalApp = Invoke-AzJson -Arguments @("ad", "app", "show", "--id", $portalApp.appId)

$apiPreauthorizationBody = @{
    api = @{
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes = @($apiBody.api.oauth2PermissionScopes)
        preAuthorizedApplications = @(
            @{
                appId = $portalApp.appId
                delegatedPermissionIds = @($scopeId)
            }
        )
    }
}
Invoke-GraphPatch -ApplicationObjectId $apiApp.id -Body $apiPreauthorizationBody

$apiSp = Ensure-ServicePrincipal -ApplicationId $apiApp.appId
$portalSp = Ensure-ServicePrincipal -ApplicationId $portalApp.appId

$result = [ordered]@{
    tenantId = $account.tenantId
    subscriptionId = $account.id
    api = [ordered]@{
        displayName = $apiApp.displayName
        applicationId = $apiApp.appId
        objectId = $apiApp.id
        servicePrincipalObjectId = $apiSp.id
        audience = "api://$($apiApp.appId)"
        delegatedScope = "api://$($apiApp.appId)/access_as_user"
        appRoles = @("IIP.Access", "IIP.Admin", "IIP.ReadOnly")
    }
    portal = [ordered]@{
        displayName = $portalApp.displayName
        applicationId = $portalApp.appId
        objectId = $portalApp.id
        servicePrincipalObjectId = $portalSp.id
        redirectUris = $PortalRedirectUris
    }
    createsSecrets = $false
    legacyRegistrationsChanged = $false
}

$result | ConvertTo-Json -Depth 10

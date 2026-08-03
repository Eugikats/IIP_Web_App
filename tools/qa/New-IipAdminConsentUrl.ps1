param(
    [string]$TenantId = "dcb0b132-fb9e-475c-a68e-b0671222e8d8",
    [string]$PortalClientId = "3b3522e4-13fd-4bd9-8f08-a1cedf428d0d",
    [string]$PortalBaseUrl = "https://app-portal-dev-h5girbnzrayqe.azurewebsites.net",
    [string]$RedirectUri = "",
    [string]$Scope = "api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user",
    [string]$State = ""
)

$ErrorActionPreference = "Stop"

function Escape-QueryValue {
    param([string]$Value)
    return [System.Uri]::EscapeDataString($Value)
}

if ([string]::IsNullOrWhiteSpace($TenantId)) {
    throw "TenantId is required. Use the insurer/customer Entra tenant ID or domain for admin consent."
}

if ([string]::IsNullOrWhiteSpace($PortalClientId)) {
    throw "PortalClientId is required. Use the SPA/client app registration id, not the API app id."
}

if ([string]::IsNullOrWhiteSpace($RedirectUri)) {
    if ([string]::IsNullOrWhiteSpace($PortalBaseUrl)) {
        throw "Either RedirectUri or PortalBaseUrl is required."
    }

    $RedirectUri = $PortalBaseUrl.TrimEnd("/") + "/"
}

if ([string]::IsNullOrWhiteSpace($State)) {
    $State = "iip-uat-" + [guid]::NewGuid().ToString("N")
}

$adminConsentUrl = "https://login.microsoftonline.com/$(Escape-QueryValue $TenantId)/adminconsent" `
    + "?client_id=$(Escape-QueryValue $PortalClientId)" `
    + "&redirect_uri=$(Escape-QueryValue $RedirectUri)" `
    + "&state=$(Escape-QueryValue $State)"

Write-Host ""
Write-Host "IIP portal admin-consent URL"
Write-Host "----------------------------"
Write-Host $adminConsentUrl
Write-Host ""
Write-Host "Parameters"
Write-Host "----------"
Write-Host "TenantId:       $TenantId"
Write-Host "PortalClientId: $PortalClientId"
Write-Host "RedirectUri:    $RedirectUri"
Write-Host "Scope:          $Scope"
Write-Host "State:          $State"
Write-Host ""
Write-Host "After a tenant administrator grants consent, verify the live gate with:"
Write-Host "powershell -ExecutionPolicy Bypass -File tools\qa\Invoke-IipLiveUatSmoke.ps1 -RequireAuthenticated"
Write-Host ""
Write-Host "Note: the smoke runner's default token probe uses Azure CLI as its own public client."
Write-Host "Portal consent can unblock browser sign-in while Azure CLI token acquisition may still need separate tenant consent."
Write-Host "If you obtain a valid user token through another approved route, pass it with -AccessToken."


param(
    [string]$InfraRoot = "infra",
    [switch]$BuildBicepToTemp
)

$ErrorActionPreference = "Stop"

function New-CheckResult {
    param(
        [string]$Check,
        [string]$Result,
        [string]$Detail
    )

    [pscustomobject]@{
        Check = $Check
        Result = $Result
        Detail = $Detail
    }
}

function Add-Result {
    param(
        [string]$Check,
        [string]$Result,
        [string]$Detail
    )

    $script:results.Add((New-CheckResult -Check $Check -Result $Result -Detail $Detail))
    if ($Result -eq "FAIL") {
        $script:failed = $true
    }
}

function Get-UiElements {
    param([object]$CreateUi)

    $elements = New-Object System.Collections.Generic.List[object]
    foreach ($basic in @($CreateUi.parameters.basics)) {
        $elements.Add($basic)
    }

    foreach ($step in @($CreateUi.parameters.steps)) {
        foreach ($element in @($step.elements)) {
            $elements.Add($element)
        }
    }

    return $elements
}

function Get-AllowedValues {
    param([object]$Element)

    $values = New-Object System.Collections.Generic.List[string]
    foreach ($allowed in @($Element.constraints.allowedValues)) {
        if ($null -ne $allowed.value) {
            $values.Add([string]$allowed.value)
        }
    }

    return $values
}

$results = New-Object System.Collections.Generic.List[object]
$failed = $false

$root = Resolve-Path -LiteralPath $InfraRoot
$mainTemplatePath = Join-Path $root "mainTemplate.bicep"
$createUiPath = Join-Path $root "createUiDefinition.json"
$viewDefinitionPath = Join-Path $root "viewDefinition.json"

foreach ($requiredPath in @($mainTemplatePath, $createUiPath, $viewDefinitionPath)) {
    if (Test-Path -LiteralPath $requiredPath -PathType Leaf) {
        Add-Result "Required artifact exists" "PASS" $requiredPath
    }
    else {
        Add-Result "Required artifact exists" "FAIL" "$requiredPath is missing."
    }
}

if ($failed) {
    $results | Format-Table -AutoSize
    exit 1
}

$createUi = Get-Content -LiteralPath $createUiPath -Raw | ConvertFrom-Json
$viewDefinition = Get-Content -LiteralPath $viewDefinitionPath -Raw | ConvertFrom-Json
$bicepText = Get-Content -LiteralPath $mainTemplatePath -Raw

if ($createUi.handler -eq "Microsoft.Azure.CreateUIDef") {
    Add-Result "createUiDefinition handler" "PASS" "Handler is Microsoft.Azure.CreateUIDef."
}
else {
    Add-Result "createUiDefinition handler" "FAIL" "Handler was '$($createUi.handler)'."
}

if ($viewDefinition.views.Count -gt 0) {
    Add-Result "viewDefinition views" "PASS" "viewDefinition contains $($viewDefinition.views.Count) view(s)."
}
else {
    Add-Result "viewDefinition views" "FAIL" "viewDefinition contains no views."
}

$uiElements = Get-UiElements -CreateUi $createUi
$uiOutputNames = @($createUi.parameters.outputs.PSObject.Properties.Name)
$bicepParamNames = [regex]::Matches($bicepText, "(?m)^\s*param\s+([A-Za-z_][A-Za-z0-9_]*)\s+") |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

$missingBicepParams = @($uiOutputNames | Where-Object { $_ -notin $bicepParamNames })
if ($missingBicepParams.Count -eq 0) {
    Add-Result "createUi outputs map to Bicep params" "PASS" "$($uiOutputNames.Count) output(s) map to mainTemplate.bicep parameters."
}
else {
    Add-Result "createUi outputs map to Bicep params" "FAIL" "Missing Bicep params: $($missingBicepParams -join ', ')"
}

$forbiddenProviderPattern = "(?i)(^|[_-])(mock|local_mock|in_memory|sqlite|developmentauth|seeded|fallback|placeholder)($|[_-])"
$forbiddenUiValues = New-Object System.Collections.Generic.List[string]
foreach ($element in $uiElements) {
    foreach ($value in (Get-AllowedValues -Element $element)) {
        if ($value -match $forbiddenProviderPattern) {
            $forbiddenUiValues.Add("$($element.name)=$value")
        }
    }
}

if ($forbiddenUiValues.Count -eq 0) {
    Add-Result "No fake provider choices exposed" "PASS" "createUiDefinition allowed values do not expose mock/local/in-memory/placeholder options."
}
else {
    Add-Result "No fake provider choices exposed" "FAIL" "Forbidden values: $($forbiddenUiValues -join ', ')"
}

$runtimeElement = $uiElements | Where-Object { $_.name -eq "runtimeProfile" } | Select-Object -First 1
$runtimeValues = @(Get-AllowedValues -Element $runtimeElement)
$forbiddenRuntimeValues = @("LocalTest", "Development")
$runtimeLeaks = @($runtimeValues | Where-Object { $_ -in $forbiddenRuntimeValues })
if ($runtimeLeaks.Count -eq 0 -and $runtimeValues -contains "ManagedApplicationReviewer") {
    Add-Result "Protected runtime choices" "PASS" "Runtime choices are protected and include ManagedApplicationReviewer."
}
else {
    Add-Result "Protected runtime choices" "FAIL" "Runtime choices were: $($runtimeValues -join ', ')"
}

if ([string]$createUi.parameters.outputs.authMode -eq "entra") {
    Add-Result "Managed app auth output" "PASS" "authMode output is entra."
}
else {
    Add-Result "Managed app auth output" "FAIL" "authMode output was '$($createUi.parameters.outputs.authMode)'."
}

if ([string]$createUi.parameters.outputs.storageProviderMode -eq "SqlServer") {
    Add-Result "Managed app storage output" "PASS" "storageProviderMode output is SqlServer."
}
else {
    Add-Result "Managed app storage output" "FAIL" "storageProviderMode output was '$($createUi.parameters.outputs.storageProviderMode)'."
}

if ($BuildBicepToTemp) {
    $tempOutput = Join-Path ([System.IO.Path]::GetTempPath()) ("iip-mainTemplate-" + [guid]::NewGuid().ToString("N") + ".json")
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $buildOutput = & az bicep build --file $mainTemplatePath --outfile $tempOutput 2>&1
        $buildExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
        if ($buildExitCode -eq 0 -and (Test-Path -LiteralPath $tempOutput -PathType Leaf)) {
            $compiled = Get-Content -LiteralPath $tempOutput -Raw | ConvertFrom-Json
            Add-Result "Bicep compile to temp" "PASS" "Compiled to temp ARM JSON with contentVersion '$($compiled.contentVersion)'."
        }
        else {
            Add-Result "Bicep compile to temp" "FAIL" (($buildOutput | Out-String).Trim())
        }
    }
    finally {
        if ($null -ne $previousErrorActionPreference) {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        if (Test-Path -LiteralPath $tempOutput -PathType Leaf) {
            Remove-Item -LiteralPath $tempOutput -Force
        }
    }
}
else {
    Add-Result "Bicep compile to temp" "SKIP" "Pass -BuildBicepToTemp to compile to a temporary ARM JSON file without creating package artifacts."
}

$results | Format-Table -AutoSize

if ($failed) {
    exit 1
}

exit 0

[CmdletBinding()]
param()

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$docsRoot = Join-Path $repositoryRoot 'docs'
$catalogPath = Join-Path $docsRoot 'CATALOG.md'
$includedExtensions = @('.md', '.json', '.pdf', '.gs')

$trackedPaths = @(& git -C $repositoryRoot ls-files --cached -- docs)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read the tracked documentation inventory from Git.'
}

$documents = $trackedPaths |
    ForEach-Object {
        $fullPath = Join-Path $repositoryRoot $_
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { return }
        if ([System.IO.Path]::GetFullPath($fullPath) -eq $catalogPath) { return }
        if ($includedExtensions -notcontains [System.IO.Path]::GetExtension($fullPath).ToLowerInvariant()) { return }

        $relativePath = [System.IO.Path]::GetRelativePath($docsRoot, $fullPath).Replace('\', '/')
        $segments = $relativePath.Split('/')
        [PSCustomObject]@{
            RelativePath = $relativePath
            Area = if ($segments.Count -gt 1) { $segments[0] } else { '[docs-root]' }
        }
    } |
    Sort-Object Area, RelativePath

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# IIP documentation catalog')
$lines.Add('')
$lines.Add('> Generated from Git-tracked documentation by `scripts/docs/Update-IipDocsCatalog.ps1`. Do not hand-edit.')
$lines.Add('')
$lines.Add("Total: $($documents.Count) files.")
$lines.Add('')

foreach ($group in ($documents | Group-Object Area)) {
    $lines.Add("## $($group.Name) ($($group.Count))")
    $lines.Add('')
    foreach ($document in $group.Group) {
        $label = Split-Path $document.RelativePath -Leaf
        $escapedPath = $document.RelativePath.Replace(' ', '%20')
        $lines.Add("- [$label]($escapedPath)")
    }
    $lines.Add('')
}

$content = ($lines -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
[System.IO.File]::WriteAllText($catalogPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Output "Updated $catalogPath with $($documents.Count) files."

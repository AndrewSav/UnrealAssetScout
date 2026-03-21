param(
    [Parameter(Mandatory = $true)]
    [string] $ChangelogPath,

    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [string] $ServerUrl = "https://github.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    throw "CHANGELOG not found: $ChangelogPath"
}

$normalizedTag = $Tag.Trim()
if ($normalizedTag.StartsWith("refs/tags/")) {
    $normalizedTag = $normalizedTag.Substring("refs/tags/".Length)
}

$lines = Get-Content -LiteralPath $ChangelogPath
$headingPattern = '^##\s+(.+?)\s*$'
$targetHeading = $null

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $headingPattern) {
        $headingText = $Matches[1].Trim()
        if ($headingText -eq $normalizedTag) {
            $targetHeading = $i
            break
        }
    }
}

if ($null -eq $targetHeading) {
    throw "Could not find release notes for tag '$normalizedTag' in $ChangelogPath"
}

$contentLines = [System.Collections.Generic.List[string]]::new()

for ($i = $targetHeading + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^##\s+') {
        break
    }

    $contentLines.Add($lines[$i])
}

while ($contentLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($contentLines[0])) {
    $contentLines.RemoveAt(0)
}

while ($contentLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($contentLines[$contentLines.Count - 1])) {
    $contentLines.RemoveAt($contentLines.Count - 1)
}

if ($contentLines.Count -eq 0) {
    throw "Release notes section for tag '$normalizedTag' is empty in $ChangelogPath"
}

$trimmedServerUrl = $ServerUrl.TrimEnd('/')
$changelogUrl = "$trimmedServerUrl/$Repository/blob/$normalizedTag/CHANGELOG.md"
$releaseNotes = @(
    $contentLines
    ""
    "[Previous changes can be found in full changelog]($changelogUrl)"
)

[System.IO.File]::WriteAllLines($OutputPath, $releaseNotes)

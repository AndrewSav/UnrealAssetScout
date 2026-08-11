#requires -version 5
<#
UnrealAssetScout installer for Windows.

Downloads a release from GitHub and drops uas.exe somewhere already on PATH. Release builds are a
single self-contained executable, so installing is one file copy and uninstalling is one delete.

  irm https://uas.bat.nz | iex                                     # self-contained, no prerequisite
  $env:UAS_FLAVOR='framework-dependent'; irm https://uas.bat.nz | iex   # smaller, needs .NET 10

Env overrides:
  UAS_VERSION   release tag to install       (default: the latest release)
  UAS_BINDIR    install directory            (default: %LOCALAPPDATA%\Microsoft\WindowsApps, on PATH)
  UAS_FLAVOR    self-contained | framework-dependent  (default: self-contained)
#>
$ErrorActionPreference = 'Stop'
# Invoke-WebRequest's progress bar makes downloads crawl on Windows PowerShell 5.1.
$ProgressPreference = 'SilentlyContinue'
# 5.1 defaults to TLS 1.0, which the GitHub API refuses.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'AndrewSav/UnrealAssetScout'
$flavor = if ($env:UAS_FLAVOR) { $env:UAS_FLAVOR } else { 'self-contained' }
$dest = if ($env:UAS_BINDIR) { $env:UAS_BINDIR } else { Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps' }

if ($flavor -notin 'self-contained', 'framework-dependent') {
    throw "UAS_FLAVOR must be self-contained or framework-dependent (got '$flavor')"
}

# The documented endpoint, rather than the undocumented redirect from the web host.
$headers = @{
    'Accept'     = 'application/vnd.github+json'
    'User-Agent' = 'UnrealAssetScout-installer'
}

if ($env:UAS_VERSION) {
    $tag = if ($env:UAS_VERSION.StartsWith('v')) { $env:UAS_VERSION } else { "v$env:UAS_VERSION" }
    $releaseUrl = "https://api.github.com/repos/$repository/releases/tags/$tag"
} else {
    $releaseUrl = "https://api.github.com/repos/$repository/releases/latest"
}

Write-Host "Resolving $repository ..."
try {
    $release = Invoke-RestMethod -Headers $headers -Uri $releaseUrl
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 403 -or $status -eq 429) {
        throw "GitHub's rate limit for anonymous requests is spent. Try again later."
    }
    if ($status -eq 404) {
        throw "No release found at $releaseUrl"
    }
    throw
}

$version = $release.tag_name -replace '^v', ''
$assetName = if ($flavor -eq 'self-contained') {
    "UnrealAssetScout-v$version-self-contained.zip"
} else {
    "UnrealAssetScout-v$version.zip"
}

$asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
if (-not $asset) {
    throw "Release v$version does not publish $assetName"
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("uas-install-" + [Guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Write-Host "Downloading $assetName ..."
    $archive = Join-Path $temp $assetName
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive

    Expand-Archive -Path $archive -DestinationPath $temp -Force
    $source = Join-Path $temp 'uas.exe'
    if (-not (Test-Path $source)) { throw "uas.exe is not in $assetName" }

    if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest -Force | Out-Null }
    $target = Join-Path $dest 'uas.exe'
    Copy-Item -Force $source $target

    $reported = & $target --version
    Write-Host "installed uas $reported to $target"
} finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}

if (($env:Path -split ';') -notcontains $dest.TrimEnd('\')) {
    Write-Warning "$dest is not on your PATH; run uas by full path, or unset UAS_BINDIR to use the default."
}

<#
.SYNOPSIS
    Verifies that an incremental export over a prior dump produces output identical to a
    from-scratch export against a new patch.
.DESCRIPTION
    Implements the end-to-end protocol from the incremental export design:

        export OLD pak and usmap                    -> D_old plus manifest M_old
        incremental: NEW pak and usmap over D_old    -> D_incr
        from scratch: NEW pak and usmap              -> D_full
        assert D_incr == D_full

    D_incr starts as a copy of D_old (files and manifest), then an incremental export run in the
    chosen mode against the new pak and usmap is pointed at that copy. D_full is a separate, empty output
    directory exported from scratch against the same new pak and usmap. The two trees are then
    compared file by file with a content hash, excluding the manifest itself: the manifest's own
    serialized form can legitimately differ between an incremental run and a from-scratch run
    (dictionary and list ordering are not guaranteed to match) even when every exported output is
    identical, which is the property this script actually checks.

    Not exercised by this protocol: IoStore fingerprinting, since a single classic pak has no
    IoStore container to fingerprint; external Wwise media; and FMOD/CriWare provenance. A pak/
    usmap pair that covers those would need a separate run of this script.
.PARAMETER OldPak
    Path to the paks folder for the prior (old) build. Passed straight through to "--paks", so it
    must be a directory, not a single .pak file.
.PARAMETER NewPak
    Path to the paks folder for the new build, same shape as OldPak.
.PARAMETER OldUsmap
    Path to the .usmap mappings file matching OldPak.
.PARAMETER NewUsmap
    Path to the .usmap mappings file matching NewPak.
.PARAMETER Mode
    Export mode to run the protocol with. Defaults to textures; json exercises the usmap far more
    heavily and is the stronger check when the pair includes a usmap change.
.PARAMETER Filter
    Optional path filter regex, passed to every run so all three trees share one scope. Shrinks
    the protocol from a full-dump pass to minutes, at the cost of only proving the scope it
    contains: choose a region with known churn, and treat an unfiltered run as the final gate.
.PARAMETER ExtraArguments
    Additional uas arguments appended to every run, for exercising flags such as
    --layout-dependencies under the same byte-identity verdict.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OldPak,

    [Parameter(Mandatory = $true)]
    [string]$NewPak,

    [Parameter(Mandatory = $true)]
    [string]$OldUsmap,

    [Parameter(Mandatory = $true)]
    [string]$NewUsmap,

    [string]$Mode = "textures",

    [string]$Filter = "",

    [string[]]$ExtraArguments = @()
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# The frozen baseline pair this protocol was designed around mounts a single classic pak built
# with this engine version. A baseline pair built with a different engine would need this changed
# to match.
$engineVersion = "GAME_UE5_1"

$exePath = Join-Path $PSScriptRoot "..\bin\Debug\net10.0\uas.exe"
if (-not (Test-Path $exePath -PathType Leaf)) {
    throw "uas.exe not found at $exePath -- build the solution in Debug configuration first."
}

foreach ($pair in @(
    @{ Name = "OldPak"; Value = $OldPak },
    @{ Name = "NewPak"; Value = $NewPak }
)) {
    if (-not (Test-Path $pair.Value -PathType Container)) {
        throw "$($pair.Name) must be an existing paks folder; got '$($pair.Value)'."
    }
}

foreach ($pair in @(
    @{ Name = "OldUsmap"; Value = $OldUsmap },
    @{ Name = "NewUsmap"; Value = $NewUsmap }
)) {
    if (-not (Test-Path $pair.Value -PathType Leaf)) {
        throw "$($pair.Name) must be an existing .usmap file; got '$($pair.Value)'."
    }
}

$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("uas-incremental-e2e-" + [Guid]::NewGuid().ToString("N"))
$dOld = Join-Path $workRoot "D_old"
$dIncr = Join-Path $workRoot "D_incr"
$dFull = Join-Path $workRoot "D_full"

function Invoke-Export {
    param(
        [string]$Label,
        [string]$Paks,
        [string]$Usmap,
        [string]$OutputDir
    )

    Write-Host ""
    Write-Host $Label

    $arguments = @("export", $Mode, "--paks", $Paks, "--game", $engineVersion, "--usmap", $Usmap, "--output", $OutputDir, "--no-log")
    if ($Filter -ne "") {
        $arguments += @("--filter", $Filter)
    }
    if ($ExtraArguments.Count -gt 0) {
        $arguments += $ExtraArguments
    }

    & $exePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

function Get-FileHashList {
    param([string]$Root)

    $rootFull = (Resolve-Path $Root).Path.TrimEnd("\")
    Get-ChildItem -Path $rootFull -Recurse -File |
        Where-Object { $_.Name -ne ".uas-manifest.json" } |
        ForEach-Object {
            $relative = $_.FullName.Substring($rootFull.Length + 1).Replace("\", "/")
            [PSCustomObject]@{
                Path = $relative
                Hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
            }
        } |
        Sort-Object Path
}

New-Item -ItemType Directory -Path $workRoot -Force | Out-Null

try {
    Invoke-Export -Label "export OLD pak and usmap -> D_old" -Paks $OldPak -Usmap $OldUsmap -OutputDir $dOld

    Copy-Item -Path $dOld -Destination $dIncr -Recurse
    Invoke-Export -Label "incremental: NEW pak and usmap over D_old -> D_incr" -Paks $NewPak -Usmap $NewUsmap -OutputDir $dIncr

    Invoke-Export -Label "from scratch: NEW pak and usmap -> D_full" -Paks $NewPak -Usmap $NewUsmap -OutputDir $dFull

    Write-Host ""
    Write-Host "Hashing D_incr and D_full ..."
    $incrHashes = @(Get-FileHashList -Root $dIncr)
    $fullHashes = @(Get-FileHashList -Root $dFull)

    $diff = Compare-Object -ReferenceObject $incrHashes -DifferenceObject $fullHashes -Property Path, Hash

    if ($diff) {
        Write-Host ""
        Write-Host "MISMATCH: D_incr and D_full differ." -ForegroundColor Red
        $diff | Format-Table -AutoSize | Out-String | Write-Host
        Write-Host "D_incr: $dIncr"
        Write-Host "D_full: $dFull"
        exit 1
    }

    Write-Host ""
    Write-Host "OK: D_incr and D_full are byte-identical ($($incrHashes.Count) file(s) compared)."
    Remove-Item -Path $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    exit 0
}
catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
    Write-Host "Working directories preserved for inspection: $workRoot"
    exit 1
}

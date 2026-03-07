param(
    [Parameter(Mandatory = $true)]
    [string]$Name
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$exePath = Join-Path $PSScriptRoot "..\bin\Debug\net8.0\uas.exe"
$responseFilePath = Join-Path $PSScriptRoot "$Name.config"
$responseFile = "@$Name.config"
$dumpRoot = Join-Path "C:\_pakdumps" $Name
$timingLogPath = Join-Path $PSScriptRoot "$Name-timings.log"

$runStartedAt = Get-Date
@(
    "Run started: $($runStartedAt.ToString('o'))"
    "Game: $Name"
    "Exe: $exePath"
    "Config: $responseFilePath"
    "Timing log: $timingLogPath"
    ""
) | Set-Content -Path $timingLogPath

function Write-TimingLog {
    param(
        [string]$Message
    )

    Add-Content -Path $timingLogPath -Value $Message
}

function Invoke-TimedStep {
    param(
        [string]$Label,
        [string[]]$Arguments
    )

    $commandText = "$exePath $($Arguments -join ' ')"
    $startedAt = Get-Date
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "Succeeded"
    $exitCode = 0

    Write-Host ""
    Write-Host "[$Name] $Label"
    Write-Host $commandText

    try {
        & $exePath @Arguments
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "Command exited with code $exitCode."
        }
    }
    catch {
        $status = "Failed"
        if ($LASTEXITCODE -ne $null) {
            $exitCode = $LASTEXITCODE
        }

        throw
    }
    finally {
        $stopwatch.Stop()
        Write-TimingLog ("[{0}] {1}" -f $status, $Label)
        Write-TimingLog ("  Started: {0}" -f $startedAt.ToString("o"))
        Write-TimingLog ("  Elapsed: {0}" -f $stopwatch.Elapsed)
        Write-TimingLog ("  ExitCode: {0}" -f $exitCode)
        Write-TimingLog ("  Command: {0}" -f $commandText)
        Write-TimingLog ""
    }
}

Invoke-TimedStep "list tree" @("list", $responseFile, "--format", "tree")
Invoke-TimedStep "list default" @("list", $responseFile, "--log-append")
Invoke-TimedStep "list types csv" @("list", $responseFile, "--format", "types", "--file", "$Name.csv", "--log-append")
Invoke-TimedStep "export simple" @("export", "simple", $responseFile, "--output", (Join-Path $dumpRoot "simple"), "--compact", "--log-append")
Invoke-TimedStep "export json" @("export", "json", $responseFile, "--output", (Join-Path $dumpRoot "json"), "--compact", "--log-append")
Invoke-TimedStep "export graphics" @("export", "graphics", $responseFile, "--output", (Join-Path $dumpRoot "graphics"), "--compact", "--log-append")

$runFinishedAt = Get-Date
Write-TimingLog ("Run finished: {0}" -f $runFinishedAt.ToString("o"))
Write-TimingLog ("Total elapsed: {0}" -f ($runFinishedAt - $runStartedAt))

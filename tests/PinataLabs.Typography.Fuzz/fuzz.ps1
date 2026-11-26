<#
.SYNOPSIS
    Fuzzes the font program API (issue #74), the Item Variation Store reader (issue #124) and the HVAR, VVAR and MVAR readers (issue #128) with libFuzzer through SharpFuzz, or in process without it.

.DESCRIPTION
    Publishes the harness to artifacts/fuzz/publish, instruments PinataLabs.Typography.dll, copies the committed
    seeds into a working corpus, and runs libFuzzer. Crash, timeout and out-of-memory inputs go to artifacts/fuzz/findings.

    -InProcess runs the mutation runner of the harness instead. It needs no libFuzzer binary and no instrumentation.
    -Replay runs saved inputs through the same oracle, uninstrumented, and groups the outcomes.

.EXAMPLE
    ./fuzz.ps1 -Target program -MaxTotalTime 600

.EXAMPLE
    ./fuzz.ps1 -Target type1-plain -InProcess -MaxTotalTime 600

.EXAMPLE
    ./fuzz.ps1 -Target program -Replay artifacts/fuzz/findings
#>
[CmdletBinding()]
param(
    [ValidateSet('program', 'type1-plain', 'item-variation-store', 'metrics-variation')]
    [string] $Target = 'program',
    [string] $LibFuzzer = (Join-Path $PSScriptRoot 'tools/libfuzzer-dotnet-windows.exe'),
    [string] $Replay,
    [switch] $InProcess,
    [int] $Timeout = 10,
    [int] $MaxTotalTime = 0,
    [int] $Jobs = 1,
    [Parameter(ValueFromRemainingArguments = $true)] [string[]] $LibFuzzerArgument
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native([string] $File, [string[]] $Arguments) {
    Write-Host "> $File $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $File" }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $PSScriptRoot 'PinataLabs.Typography.Fuzz.csproj'
$workRoot = Join-Path $repoRoot 'artifacts/fuzz'
$publishRoot = Join-Path $workRoot "publish/$Target"
$toolsRoot = Join-Path $workRoot 'tools'
$findings = Join-Path $workRoot 'findings'
$seedRoot = Join-Path $PSScriptRoot "corpus/$Target"
$corpus = Join-Path $workRoot "corpus/$Target"
New-Item -ItemType Directory -Force -Path $toolsRoot, $findings, $corpus | Out-Null

# SharpFuzz rewrites the library in place, so publish into a fresh directory every time.
if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
Invoke-Native 'dotnet' @('publish', $project, '-c', 'Release', '--output', $publishRoot, '--no-self-contained')
$harness = Join-Path $publishRoot ($IsWindows ? 'PinataLabs.Typography.Fuzz.exe' : 'PinataLabs.Typography.Fuzz')

if ($Replay) {
    & $harness replay $Target ([IO.Path]::GetFullPath($Replay, $repoRoot))
    exit $LASTEXITCODE
}

if ($InProcess) {
    $seconds = $MaxTotalTime -gt 0 ? $MaxTotalTime : 600
    & $harness mutate $Target $seconds $seedRoot $findings
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $LibFuzzer -PathType Leaf)) {
    throw @"
libfuzzer-dotnet was not found at $LibFuzzer.
It is a prebuilt binary that is not in the repository. Download it from
https://github.com/Metalnem/libfuzzer-dotnet/releases into $PSScriptRoot/tools, or pass -LibFuzzer.
-InProcess and -Replay need neither libFuzzer nor instrumentation.
"@
}

$tool = Join-Path $toolsRoot ($IsWindows ? 'sharpfuzz.exe' : 'sharpfuzz')
if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
    Invoke-Native 'dotnet' @('tool', 'install', 'SharpFuzz.CommandLine', '--tool-path', $toolsRoot, '--version', '2.3.0')
}

Invoke-Native $tool @((Join-Path $publishRoot 'PinataLabs.Typography.dll'))

if (@(Get-ChildItem -LiteralPath $corpus -File).Count -eq 0) {
    Copy-Item -Path (Join-Path $seedRoot '*') -Destination $corpus
}

$env:TYPOGRAPHY_FUZZ_TARGET = $Target
$arguments = @(
    "--target_path=$harness",
    "-timeout=$Timeout",
    "-rss_limit_mb=4096",
    "-max_len=262144",
    "-artifact_prefix=$findings$([IO.Path]::DirectorySeparatorChar)")
if ($Jobs -gt 1) { $arguments += @("-jobs=$Jobs", "-workers=$Jobs") }
if ($MaxTotalTime -gt 0) { $arguments += "-max_total_time=$MaxTotalTime" }
if ($LibFuzzerArgument) { $arguments += $LibFuzzerArgument }
$arguments += @($corpus, $seedRoot)

if ($MaxTotalTime -le 0) {
    Invoke-Native $LibFuzzer $arguments
    return
}

# A harness that dies at startup leaves libFuzzer waiting for it past -max_total_time, so a watchdog ends the run.
$grace = 120
$process = Start-Process -FilePath $LibFuzzer -ArgumentList $arguments -NoNewWindow -PassThru
if (-not $process.WaitForExit(($MaxTotalTime + $grace) * 1000)) {
    $process.Kill($true)
    throw "libFuzzer overran -max_total_time ($MaxTotalTime s) by $grace s and was stopped. Replay the seeds to see whether the harness starts: ./fuzz.ps1 -Target $Target -Replay $seedRoot"
}

if ($process.ExitCode -ne 0) {
    throw "libFuzzer exited with $($process.ExitCode). Replay the findings: ./fuzz.ps1 -Target $Target -Replay $findings"
}

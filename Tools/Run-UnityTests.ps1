[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [string]$TestFilter,

    [string]$Target,

    [string]$ProjectPath = (Join-Path $PSScriptRoot ".."),

    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe",

    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600,

    [ValidateRange(1, 300)]
    [int]$PollSeconds = 5,

    [switch]$RunSynchronously,

    [switch]$NoGraphics
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function Get-SanitizedTarget([string]$Value) {
    $sanitized = ($Value -replace "[^A-Za-z0-9_.-]+", "_").Trim("_")
    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        return "All"
    }

    return $sanitized
}

function Format-CommandLine([string]$FilePath, [string[]]$Arguments) {
    $parts = @($FilePath)
    $safeArguments = @()
    if ($null -ne $Arguments) {
        $safeArguments = $Arguments
    }

    foreach ($argument in $safeArguments) {
        if ($argument -match '[\s"]') {
            $parts += '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $parts += $argument
        }
    }

    return ($parts -join ' ')
}

function Get-XmlRunInfo([string]$ResultsPath) {
    if (-not (Test-Path -LiteralPath $ResultsPath)) {
        return $null
    }

    [xml]$xml = Get-Content -LiteralPath $ResultsPath -Raw
    $testRun = $xml."test-run"
    $failedNode = $xml.SelectSingleNode("//test-case[@result='Failed']")

    return [pscustomobject]@{
        RawResult = [string]$testRun.result
        TestCaseCount = [int]$testRun.testcasecount
        Total = [int]$testRun.total
        Passed = [int]$testRun.passed
        Failed = [int]$testRun.failed
        FailedTest = if ($null -ne $failedNode) { [string]$failedNode.fullname } else { "" }
        FailureMessage = if ($null -ne $failedNode -and $null -ne $failedNode.failure.message) { [string]$failedNode.failure.message } else { "" }
    }
}

function Get-RunClassification(
    [string]$ResultsPath,
    [int]$ExitCode,
    [switch]$TimedOut
) {
    if ($TimedOut.IsPresent) {
        return [pscustomobject]@{
            Status = "did not complete"
            XmlExists = (Test-Path -LiteralPath $ResultsPath)
            ResultDetail = "timeout"
            TestCaseCount = 0
            Total = 0
            Passed = 0
            Failed = 0
            FailedTest = ""
            FailureMessage = "Unity batchmode timed out."
        }
    }

    $xmlInfo = Get-XmlRunInfo -ResultsPath $ResultsPath
    if ($null -eq $xmlInfo) {
        return [pscustomobject]@{
            Status = "did not start"
            XmlExists = $false
            ResultDetail = "TestResults.xml missing"
            TestCaseCount = 0
            Total = 0
            Passed = 0
            Failed = 0
            FailedTest = ""
            FailureMessage = "TestResults.xml was not generated."
        }
    }

    if ($xmlInfo.TestCaseCount -eq 0) {
        return [pscustomobject]@{
            Status = "failed"
            XmlExists = $true
            ResultDetail = "zero tests collected"
            TestCaseCount = 0
            Total = $xmlInfo.Total
            Passed = $xmlInfo.Passed
            Failed = $xmlInfo.Failed
            FailedTest = ""
            FailureMessage = "Zero tests collected."
        }
    }

    if ($xmlInfo.Failed -gt 0) {
        return [pscustomobject]@{
            Status = "failed"
            XmlExists = $true
            ResultDetail = $xmlInfo.RawResult
            TestCaseCount = $xmlInfo.TestCaseCount
            Total = $xmlInfo.Total
            Passed = $xmlInfo.Passed
            Failed = $xmlInfo.Failed
            FailedTest = $xmlInfo.FailedTest
            FailureMessage = $xmlInfo.FailureMessage
        }
    }

    if ($ExitCode -ne 0) {
        return [pscustomobject]@{
            Status = "failed"
            XmlExists = $true
            ResultDetail = "non-zero exit code"
            TestCaseCount = $xmlInfo.TestCaseCount
            Total = $xmlInfo.Total
            Passed = $xmlInfo.Passed
            Failed = $xmlInfo.Failed
            FailedTest = $xmlInfo.FailedTest
            FailureMessage = "Unity exited with a non-zero exit code after producing XML."
        }
    }

    return [pscustomobject]@{
        Status = "passed"
        XmlExists = $true
        ResultDetail = $xmlInfo.RawResult
        TestCaseCount = $xmlInfo.TestCaseCount
        Total = $xmlInfo.Total
        Passed = $xmlInfo.Passed
        Failed = $xmlInfo.Failed
        FailedTest = ""
        FailureMessage = ""
    }
}

function Write-RunSummary(
    [string]$SummaryPath,
    [string]$Platform,
    [string]$TestFilter,
    [string]$CommandLine,
    [string]$RunDirectory,
    [string]$LogPath,
    [string]$ResultsPath,
    [pscustomobject]$Classification,
    [int]$ExitCode
) {
    $xmlText = "missing"
    if ($Classification.XmlExists) {
        $xmlText = "exists"
    }

    $lines = @(
        "Test target",
        "- Platform: $Platform",
        "- Filter: $TestFilter",
        "Command",
        "- $CommandLine",
        "Output",
        "- RunDir: $RunDirectory",
        "- Log: $LogPath",
        "- Results: $ResultsPath",
        "Result",
        "- Status: $($Classification.Status)",
        "- ExitCode: $ExitCode",
        "- XML: $xmlText",
        "- TestCaseCount: $($Classification.TestCaseCount)",
        "- Total: $($Classification.Total)",
        "- Passed: $($Classification.Passed)",
        "- Failed: $($Classification.Failed)",
        "Notes"
    )

    if (-not [string]::IsNullOrWhiteSpace($Classification.ResultDetail)) {
        $lines += "- Detail: $($Classification.ResultDetail)"
    }

    if (-not [string]::IsNullOrWhiteSpace($Classification.FailedTest)) {
        $lines += "- FailedTest: $($Classification.FailedTest)"
    }

    if (-not [string]::IsNullOrWhiteSpace($Classification.FailureMessage)) {
        $lines += "- Message: $($Classification.FailureMessage.Trim())"
    }

    Set-Content -LiteralPath $SummaryPath -Value $lines -Encoding utf8
}

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor was not found: $UnityPath"
}

if ($Platform -eq "PlayMode" -and $RunSynchronously.IsPresent) {
    throw "PlayMode tests must not use -RunSynchronously."
}

$runningUnity = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($null -ne $runningUnity) {
    throw "Close Unity Editor before running batchmode tests for this project."
}

$targetName = if ([string]::IsNullOrWhiteSpace($Target)) { $TestFilter } else { $Target }
$targetName = Get-SanitizedTarget $targetName
$runId = "{0}_{1}_{2}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $Platform, $targetName
$runDir = Join-Path $resolvedProjectPath "Logs\TestRuns\$runId"
if (Test-Path -LiteralPath $runDir) {
    Remove-Item -LiteralPath $runDir -Recurse -Force
}

New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$logPath = Join-Path $runDir "unity.log"
$resultsPath = Join-Path $runDir "TestResults.xml"
$summaryPath = Join-Path $runDir "summary.md"

$argumentList = @(
    "-batchmode",
    "-accept-apiupdate",
    "-projectPath", $resolvedProjectPath,
    "-runTests"
)

if ($NoGraphics.IsPresent) {
    $argumentList += "-nographics"
}

if ($Platform -eq "EditMode" -and $RunSynchronously.IsPresent) {
    $argumentList += "-runSynchronously"
}

$argumentList += @(
    "-testPlatform", $Platform,
    "-testFilter", $TestFilter,
    "-testResults", $resultsPath,
    "-logFile", $logPath
)

$commandLine = Format-CommandLine -FilePath $UnityPath -Arguments $argumentList

Write-Output ("RunDir=" + $runDir)
Write-Output ("Log=" + $logPath)
Write-Output ("Results=" + $resultsPath)

$process = Start-Process -FilePath $UnityPath -ArgumentList $argumentList -PassThru
$startedAt = Get-Date
$deadline = $startedAt.AddSeconds($TimeoutSeconds)
$lastStatusWrite = $startedAt
$timedOut = $false

while (-not $process.HasExited) {
    Start-Sleep -Seconds $PollSeconds
    $process.Refresh()

    $now = Get-Date
    if (($now - $lastStatusWrite).TotalSeconds -ge 30) {
        if (Test-Path -LiteralPath $logPath) {
            $logSize = (Get-Item -LiteralPath $logPath).Length
            Write-Output ("UnityStillRunning ElapsedSeconds=" + [int](($now - $startedAt).TotalSeconds) + " LogSize=" + $logSize)
        }
        else {
            Write-Output ("UnityStillRunning ElapsedSeconds=" + [int](($now - $startedAt).TotalSeconds) + " unity.log missing")
        }

        $lastStatusWrite = $now
    }

    if ($now -gt $deadline) {
        $timedOut = $true
        break
    }
}

if ($timedOut) {
    Write-Output ("UnityTimeoutAfterSeconds=" + $TimeoutSeconds)
    Write-Output "--- Unity processes ---"
    Get-Process Unity -ErrorAction SilentlyContinue |
        Select-Object Id, ProcessName, Path, StartTime, CPU

    if (Test-Path -LiteralPath $logPath) {
        Write-Output "--- unity.log last 160 lines ---"
        Get-Content -LiteralPath $logPath -Tail 160
    }
    else {
        Write-Output "unity.log missing"
    }

    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    $exitCode = 124
}
else {
    $process.Refresh()
    $exitCode = $process.ExitCode
}

$classification = Get-RunClassification -ResultsPath $resultsPath -ExitCode $exitCode -TimedOut:$timedOut
Write-RunSummary -SummaryPath $summaryPath -Platform $Platform -TestFilter $TestFilter -CommandLine $commandLine -RunDirectory $runDir -LogPath $logPath -ResultsPath $resultsPath -Classification $classification -ExitCode $exitCode

if ($classification.Status -ne "passed") {
    if (Test-Path -LiteralPath $logPath) {
        Write-Output "--- unity.log last 120 lines ---"
        Get-Content -LiteralPath $logPath -Tail 120
    }

    if ($classification.XmlExists) {
        Write-Output "--- Test summary ---"
        Get-Content -LiteralPath $summaryPath
    }
}

[pscustomobject]@{
    Platform = $Platform
    TestFilter = $TestFilter
    RunId = $runId
    RunDirectory = $runDir
    SummaryPath = $summaryPath
    ExitCode = $exitCode
    XmlExists = $classification.XmlExists
    LogPath = $logPath
    ResultsPath = $resultsPath
    Status = $classification.Status
    ResultDetail = $classification.ResultDetail
    TestCaseCount = $classification.TestCaseCount
    Total = $classification.Total
    Passed = $classification.Passed
    Failed = $classification.Failed
    FailedTest = $classification.FailedTest
    FailureMessage = $classification.FailureMessage
}

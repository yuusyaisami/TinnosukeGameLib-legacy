[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot ".."),

    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe",

    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600,

    [switch]$NoGraphics
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function Invoke-AuditStep {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Definition,

        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$ResolvedUnityPath,

        [Parameter(Mandatory = $true)]
        [int]$ResolvedTimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [bool]$UseNoGraphics
    )

    $scriptPath = Join-Path $PSScriptRoot "Run-UnityTests.ps1"
    $arguments = @{
        Platform = $Definition.Platform
        TestFilter = $Definition.TestFilter
        Target = $Definition.Target
        ProjectPath = $ResolvedProjectPath
        UnityPath = $ResolvedUnityPath
        TimeoutSeconds = $ResolvedTimeoutSeconds
        RunSynchronously = $true
    }

    if ($UseNoGraphics) {
        $arguments.NoGraphics = $true
    }

    Write-Host ("[M12.1] Running " + $Definition.Target + " -> " + $Definition.TestFilter)
    $result = & $scriptPath @arguments
    if ($null -eq $result) {
        throw "Run-UnityTests.ps1 did not return a result object for target $($Definition.Target)."
    }

    return $result
}

function Write-AuditSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SummaryDirectory,

        [Parameter(Mandatory = $true)]
        [pscustomobject[]]$Results
    )

    $summaryPath = Join-Path $SummaryDirectory "M12_1AuditSummary.md"
    $jsonPath = Join-Path $SummaryDirectory "M12_1AuditSummary.json"

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("M12.1 Audit Summary")
    $lines.Add("")

    for ($index = 0; $index -lt $Results.Length; $index++) {
        $result = $Results[$index]
        $lines.Add("- Target: $($result.Target)")
        $lines.Add("  Status: $($result.Status)")
        $lines.Add("  TestFilter: $($result.TestFilter)")
        $lines.Add("  RunDirectory: $($result.RunDirectory)")
        if (-not [string]::IsNullOrWhiteSpace($result.ResultDetail)) {
            $lines.Add("  Detail: $($result.ResultDetail)")
        }

        if (-not [string]::IsNullOrWhiteSpace($result.FailedTest)) {
            $lines.Add("  FailedTest: $($result.FailedTest)")
        }

        $lines.Add("")
    }

    Set-Content -LiteralPath $summaryPath -Value $lines -Encoding utf8

    $json = [ordered]@{
        SchemaVersion = "1"
        ReportKind = "M12_1AuditSummary"
        GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        Results = $Results
    } | ConvertTo-Json -Depth 6
    Set-Content -LiteralPath $jsonPath -Value $json -Encoding utf8
}

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$resolvedUnityPath = $UnityPath
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryDirectory = Join-Path $resolvedProjectPath (Join-Path "Logs\M12_1" $runId)
New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null

$steps = @(
    @{
        Platform = "EditMode"
        TestFilter = "TinnosukeGameLib.Tests.Editor.M12_1BridgeAuthorityAuditTests"
        Target = "M12.1-BridgeAuthorityStatic"
    },
    @{
        Platform = "EditMode"
        TestFilter = "TinnosukeGameLib.Tests.Editor.M12_1SceneAssetBridgeLingeringServiceTests"
        Target = "M12.1-AssetBridgeLingering"
    },
    @{
        Platform = "EditMode"
        TestFilter = "TinnosukeGameLib.Tests.Editor.SceneAssetMigrationValidationTests"
        Target = "M12.1-SceneAssetBaseline"
    },
    @{
        Platform = "EditMode"
        TestFilter = "TinnosukeGameLib.Tests.Editor.ShippedGameplayVerificationTests"
        Target = "M12.1-GameplayEntryGate"
    }
)

$results = New-Object System.Collections.Generic.List[pscustomobject]

for ($index = 0; $index -lt $steps.Length; $index++) {
    $stepResult = Invoke-AuditStep -Definition $steps[$index] -ResolvedProjectPath $resolvedProjectPath -ResolvedUnityPath $resolvedUnityPath -ResolvedTimeoutSeconds $TimeoutSeconds -UseNoGraphics:$NoGraphics.IsPresent
    $results.Add([pscustomobject]@{
        Target = $steps[$index].Target
        Platform = $stepResult.Platform
        TestFilter = $stepResult.TestFilter
        Status = $stepResult.Status
        ResultDetail = $stepResult.ResultDetail
        FailedTest = $stepResult.FailedTest
        RunDirectory = $stepResult.RunDirectory
        SummaryPath = $stepResult.SummaryPath
        SummaryJsonPath = $stepResult.SummaryJsonPath
    })

    if ($stepResult.Status -ne "passed") {
        Write-AuditSummary -SummaryDirectory $summaryDirectory -Results $results.ToArray()
        throw "M12.1 audit stopped at $($steps[$index].Target) with status '$($stepResult.Status)'."
    }
}

Write-AuditSummary -SummaryDirectory $summaryDirectory -Results $results.ToArray()
$results.ToArray()
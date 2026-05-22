[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot ".."),
    [string]$UnityTestsScript = (Join-Path $PSScriptRoot "Run-UnityTests.ps1"),
    [string]$DotNetPath
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function Resolve-DotNetPath {
    if (-not [string]::IsNullOrWhiteSpace($DotNetPath)) {
        return $DotNetPath
    }

    $dotNetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotNetCommand) {
        return $dotNetCommand.Source
    }

    $fallbackPath = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $fallbackPath) {
        return $fallbackPath
    }

    throw "dotnet.exe was not found. Set -DotNetPath or install dotnet."
}

function Invoke-CheckedCommand {
    param(
        [string]$DisplayName,
        [scriptblock]$ScriptBlock
    )

    Write-Host "== $DisplayName =="
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) {
        throw "$DisplayName failed with exit code $LASTEXITCODE."
    }
}

function Invoke-UnityLane {
    param(
        [string]$LaneName,
        [string]$Platform,
        [string[]]$TestFilters,
        [string]$Target
    )

    Write-Host "== $LaneName =="
    foreach ($testFilter in $TestFilters) {
        & $UnityTestsScript -Platform $Platform -TestFilter $testFilter -Target $Target -RunSynchronously -NoGraphics
        if ($LASTEXITCODE -ne 0) {
            throw "$LaneName failed for test filter '$testFilter' with exit code $LASTEXITCODE."
        }
    }
}

$dotNetExe = Resolve-DotNetPath
$solutionPath = Join-Path $ProjectPath "TinnosukeGameLib.slnx"

Invoke-CheckedCommand -DisplayName "Build" -ScriptBlock {
    & $dotNetExe build $solutionPath -v minimal
}

# EditMode validation
Invoke-UnityLane -LaneName "EditMode validation" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.DependencyValidatorTests",
    "TinnosukeGameLib.Tests.Editor.BootValidationTests",
    "TinnosukeGameLib.Tests.Editor.KernelBootManifestTests",
    "TinnosukeGameLib.Tests.Editor.KernelRuntimeServiceGraphTests",
    "TinnosukeGameLib.Tests.Editor.KernelRuntimeScopeGraphTests",
    "TinnosukeGameLib.Tests.Editor.KernelBootBoundaryTests",
    "TinnosukeGameLib.Tests.Editor.KernelVerifiedValueRuntimeTests",
    "TinnosukeGameLib.Tests.Editor.KernelVerifiedCompositionRuntimeTests",
    "TinnosukeGameLib.Tests.Editor.KernelVerifiedCommandRuntimeTests"
) -Target "M15.4_EditMode_Validation"

# EditMode generation
Invoke-UnityLane -LaneName "EditMode generation" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.VerifiedPlanTests",
    "TinnosukeGameLib.Tests.Editor.ArtifactSetPromotionTests",
    "TinnosukeGameLib.Tests.Editor.KernelProjectionGeneratorTests",
    "TinnosukeGameLib.Tests.Editor.KernelIRStructureTests",
    "TinnosukeGameLib.Tests.Editor.KernelIRHashingTests",
    "TinnosukeGameLib.Tests.Editor.KernelIRIdentitiesTests",
    "TinnosukeGameLib.Tests.Editor.KernelIRSourceLocationTests"
) -Target "M15.4_EditMode_Generation"

# EditMode live-boot bundle
Invoke-UnityLane -LaneName "EditMode live-boot bundle" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.KernelV22LiveBootBundleTests",
    "TinnosukeGameLib.Tests.Editor.AuthoringBridgeDirectPlayTests"
) -Target "M15.4_Runtime_LiveBoot"

# PlayMode minimal boot
Invoke-UnityLane -LaneName "PlayMode minimal boot" -Platform "PlayMode" -TestFilters @(
    "TinnosukeGameLib.Tests.PlayMode.KernelMinimalBootPlayModeTests"
) -Target "M15.4_PlayMode_MinimalBoot"

# EditMode representative gameplay bundle
Invoke-UnityLane -LaneName "EditMode representative gameplay bundle" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.KernelV22RepresentativeGameSceneBundleTests",
    "TinnosukeGameLib.Tests.Editor.GameStateMachineMigrationTests",
    "TinnosukeGameLib.Tests.Editor.ConversationDialogueMigrationTests",
    "TinnosukeGameLib.Tests.Editor.GridObjectAuthorityMigrationTests",
    "TinnosukeGameLib.Tests.Editor.TraitListAuthorityMigrationTests",
    "TinnosukeGameLib.Tests.Editor.StatusEffectServiceDependencyCaptureTests",
    "TinnosukeGameLib.Tests.Editor.GameplayAuthorityRegressionTests"
) -Target "M15.4_Runtime_RepresentativeGameplay"

# Static forbidden-pattern tests
Invoke-UnityLane -LaneName "Static forbidden-pattern tests" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.KernelForbiddenPatternTests",
    "TinnosukeGameLib.Tests.Editor.KernelDebugGateTests",
    "TinnosukeGameLib.Tests.Editor.KernelForbiddenPatternScannerTests"
) -Target "M15.4_StaticForbiddenPatterns"

# Diagnostics snapshot tests
Invoke-UnityLane -LaneName "Diagnostics snapshot tests" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.KernelTestArtifactWriterTests",
    "TinnosukeGameLib.Tests.Editor.KernelDiagnosticsModelTests",
    "TinnosukeGameLib.Tests.Editor.KernelDiagnosticServiceTests",
    "TinnosukeGameLib.Tests.Editor.DiagnosticCodeTraceabilityTests"
) -Target "M15.4_DiagnosticsSnapshots"

# Performance smoke tests
Invoke-UnityLane -LaneName "Performance smoke tests" -Platform "EditMode" -TestFilters @(
    "Game.Editor.Tests.KernelPerformanceAllocationTests",
    "TinnosukeGameLib.Tests.Editor.KernelPerformanceRegressionGateTests"
) -Target "M15.4_PerformanceSmoke"

# Legacy-boundary tests
Invoke-UnityLane -LaneName "Legacy-boundary tests" -Platform "EditMode" -TestFilters @(
    "TinnosukeGameLib.Tests.Editor.LegacyCompatBoundaryTests",
    "TinnosukeGameLib.Tests.Editor.KernelDiagnosticsAsmdefBoundaryTests"
) -Target "M15.4_LegacyBoundary"

Write-Host "M15.4 CI gate completed successfully."
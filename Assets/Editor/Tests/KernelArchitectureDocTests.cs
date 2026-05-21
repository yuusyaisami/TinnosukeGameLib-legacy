using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelArchitectureDocTests
    {
        private static string ReadDoc(string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(fullPath), Is.True, "Missing doc file: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        [Test]
        public void Readme_ContainsValidationWorkflowAndTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "README.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-README-01"));
            Assert.That(content, Does.Contain("TC-README-02"));
            Assert.That(content, Does.Contain("02_ModuleContributionSpec.md"));
            Assert.That(content, Does.Contain("07_ScopeGraphRuntimeSpec.md"));
            Assert.That(content, Does.Contain("16_ImplementationMilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("17_AssemblyDefinitionAndCompileBoundarySpec.md"));
            Assert.That(content, Does.Contain("Index/HubClassificationInventory.md"));
            Assert.That(content, Does.Contain("DiagnosticCodeTraceabilityCatalog.md"));
            Assert.That(content, Does.Contain("Run-UnityTests.ps1"));
        }

        [Test]
        public void DiagnosticCodeTraceabilityCatalog_ContainsCurrentM15Entries()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "DiagnosticCodeTraceabilityCatalog.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("Identifier Kind"));
            Assert.That(content, Does.Contain("COMMAND_EXECUTOR_MISSING"));
            Assert.That(content, Does.Contain("DIAG_SINK_EMIT_FAILED"));
            Assert.That(content, Does.Contain("ModuleId"));
            Assert.That(content, Does.Contain("SourceLocationId"));
            Assert.That(content, Does.Contain("SourceLocationIR"));
            Assert.That(content, Does.Contain("UnitySourceLocation"));
            Assert.That(content, Does.Contain("LegacySourceLocation"));
            Assert.That(content, Does.Contain("GeneratedSourceLocation"));
            Assert.That(content, Does.Contain("STATIC_RULE_DEBUG_LOG_OUTSIDE_SINK"));
            Assert.That(content, Does.Contain("STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK"));
            Assert.That(content, Does.Contain("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME"));
            Assert.That(content, Does.Contain("STATIC_RULE_FIND_FIRST_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME"));
            Assert.That(content, Does.Contain("STATIC_RULE_FIND_ANY_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME"));
            Assert.That(content, Does.Contain("STATIC_RULE_ACTIVATOR_CREATE_INSTANCE_IN_KERNEL_RUNTIME"));
            Assert.That(content, Does.Contain("STATIC_RULE_RUNTIME_STABLE_KEY_LOOKUP_IN_KERNEL_RUNTIME"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternScannerTests.ScanText_ReportsGetComponentsInChildrenCalls"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternScannerTests.ScanText_ReportsTransformParentScopeInferenceCalls"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternScannerTests.ScanText_ReportsFindFirstObjectByTypeCalls"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternScannerTests.ScanText_ReportsStableKeyLookupOutsideValidationPaths"));
            Assert.That(content, Does.Contain("KernelIRIdentitiesTests.TypedIdentityPrimitives_PreserveValueEqualityAndHashCode"));
            Assert.That(content, Does.Contain("KernelIRSourceLocationTests.SourceLocationIR_PreservesUnityVariantEqualityAndHashCode"));
        }

        [Test]
        public void ConceptMap_ContainsRequiredCrossSpecConcepts()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "KernelV2ConceptMap.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("KernelIR"));
            Assert.That(content, Does.Contain("ModuleContribution"));
            Assert.That(content, Does.Contain("VerifiedKernelPlan"));
            Assert.That(content, Does.Contain("ArtifactSet"));
            Assert.That(content, Does.Contain("DebugMap"));
            Assert.That(content, Does.Contain("KernelDiagnostic"));
            Assert.That(content, Does.Contain("ServiceGraph"));
            Assert.That(content, Does.Contain("ScopeGraph"));
            Assert.That(content, Does.Contain("LifecyclePlan"));
            Assert.That(content, Does.Contain("CommandCatalog"));
            Assert.That(content, Does.Contain("ValueSchema"));
            Assert.That(content, Does.Contain("ValueStore"));
            Assert.That(content, Does.Contain("RuntimeQuery"));
            Assert.That(content, Does.Contain("UnityAuthoringBridge"));
            Assert.That(content, Does.Contain("LegacyCompat"));
            Assert.That(content, Does.Contain("SourceLocation"));
            Assert.That(content, Does.Contain("DiagnosticCode"));
            Assert.That(content, Does.Contain("BootManifest"));
            Assert.That(content, Does.Contain("RuntimePathKind"));
        }

        [Test]
        public void ServiceGraphSpec_ContainsM67HubClassificationTable()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "06_ServiceGraphRuntimeSpec.md"));

            Assert.That(content, Does.Contain("### M6.7 Hub / Channel / Player Classification"));
            Assert.That(content, Does.Contain("HubClassificationInventory.md"));
            Assert.That(content, Does.Contain("| Runtime concept | Default classification | Notes |"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService"));
            Assert.That(content, Does.Contain("TooltipChannelHubService"));
            Assert.That(content, Does.Contain("MeshChannelHubService"));
            Assert.That(content, Does.Contain("AnimationSpriteHubService"));
            Assert.That(content, Does.Contain("Hub-owned runtime object"));
            Assert.That(content, Does.Contain("mixed boundary"));
            Assert.That(content, Does.Contain("Service candidate"));
        }

        [Test]
        public void HubClassificationInventory_ContainsCanonicalRows()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "HubClassificationInventory.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("machine-readable inventory of the current M6.7 hub classification decisions"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService.cs"));
            Assert.That(content, Does.Contain("TooltipChannelHubService.cs"));
            Assert.That(content, Does.Contain("MeshChannelHubService.cs"));
            Assert.That(content, Does.Contain("AnimationSpriteHubService.cs"));
            Assert.That(content, Does.Contain("service candidate"));
            Assert.That(content, Does.Contain("mixed boundary"));
            Assert.That(content, Does.Contain("hub-owned runtime object"));
            Assert.That(content, Does.Contain("Eligible for ServiceGraph"));
        }

        [Test]
        public void ForbiddenPatternRegistry_ContainsInitialSeedPatterns()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "ForbiddenPatternRegistry.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("Direct `Debug.Log` call outside approved sinks"));
            Assert.That(content, Does.Contain("Debug.LogError"));
            Assert.That(content, Does.Contain("Debug.LogWarning"));
            Assert.That(content, Does.Contain("Debug.LogException"));
            Assert.That(content, Does.Contain("GetComponentsInChildren"));
            Assert.That(content, Does.Contain("FindObjectsByType"));
            Assert.That(content, Does.Contain("Transform.parent"));
            Assert.That(content, Does.Contain("Resources.Load"));
            Assert.That(content, Does.Contain("runtime stable-key lookup"));
            Assert.That(content, Does.Contain("FindFirstObjectByType"));
            Assert.That(content, Does.Contain("FindAnyObjectByType"));
            Assert.That(content, Does.Contain("GetComponentsInParent"));
            Assert.That(content, Does.Contain("GameObject.Find"));
            Assert.That(content, Does.Contain("Activator.CreateInstance"));
            Assert.That(content, Does.Contain("Runtime-generated negative IDs"));
            Assert.That(content, Does.Contain("IReadOnlyList<ICommandExecutor>"));
            Assert.That(content, Does.Contain("CommandKeyResolver"));
            Assert.That(content, Does.Contain("IScopeAcquireHandler"));
            Assert.That(content, Does.Contain("IScopeTickHandler"));
            Assert.That(content, Does.Contain("IScopeLateTickHandler"));
            Assert.That(content, Does.Contain("IScopeReleaseHandler"));
            Assert.That(content, Does.Contain("ServiceGraph as runtime object registry"));
            Assert.That(content, Does.Contain("BootManifest as global settings dump"));
            Assert.That(content, Does.Contain("Legacy fallback repair"));
        }

        [Test]
        public void CrossSpecDependencyMatrix_ContainsDirectDependencyRows()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "CrossSpecDependencyMatrix.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("## Dependency Matrix"));
            Assert.That(content, Does.Contain("00_KernelArchitectureOverviewSpec.md"));
            Assert.That(content, Does.Contain("01_KernelIRSpec.md"));
            Assert.That(content, Does.Contain("02_ModuleContributionSpec.md"));
            Assert.That(content, Does.Contain("03_VerifiedPlanGenerationSpec.md"));
            Assert.That(content, Does.Contain("04_DependencyValidationSpec.md"));
            Assert.That(content, Does.Contain("05_BootManifestAndProfileSpec.md"));
            Assert.That(content, Does.Contain("06_ServiceGraphRuntimeSpec.md"));
            Assert.That(content, Does.Contain("07_ScopeGraphRuntimeSpec.md"));
            Assert.That(content, Does.Contain("08_LifecyclePlanSpec.md"));
            Assert.That(content, Does.Contain("09_CommandCatalogRuntimeSpec.md"));
            Assert.That(content, Does.Contain("10_ValueSchemaAndStoreSpec.md"));
            Assert.That(content, Does.Contain("10_1_ScalarRuntimeAndBindingSpec.md"));
            Assert.That(content, Does.Contain("10_2_DynamicValueEvaluationSpec.md"));
            Assert.That(content, Does.Contain("11_DebugMapAndDiagnosticsSpec.md"));
            Assert.That(content, Does.Contain("12_UnityAuthoringBridgeSpec.md"));
            Assert.That(content, Does.Contain("13_LegacyCompatBoundarySpec.md"));
            Assert.That(content, Does.Contain("14_PerformanceBudgetAndRuntimeRulesSpec.md"));
            Assert.That(content, Does.Contain("15_TestAndValidationSpec.md"));
            Assert.That(content, Does.Contain("16_ImplementationMilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("17_AssemblyDefinitionAndCompileBoundarySpec.md"));
        }

        [Test]
        public void ExistingAnchorInventory_ContainsCurrentCodeLocations()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "Index", "ExistingAnchorInventory.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("RuntimeLifetimeScope.cs"));
            Assert.That(content, Does.Contain("ScopeFeatureInstallerUtility.cs"));
            Assert.That(content, Does.Contain("LoadingScreenService.cs"));
            Assert.That(content, Does.Contain("RuntimeResolverHub.cs"));
            Assert.That(content, Does.Contain("CommandRunnerMB.cs"));
            Assert.That(content, Does.Contain("CommandExecutorRegistry.cs"));
            Assert.That(content, Does.Contain("BlackboardService.cs"));
            Assert.That(content, Does.Contain("VarIdResolver.cs"));
            Assert.That(content, Does.Contain("VarKeyRegistryLocator.cs"));
            Assert.That(content, Does.Contain("LTSLog.cs"));
            Assert.That(content, Does.Contain("DynamicRuntimeLogUtility.cs"));
            Assert.That(content, Does.Contain("ExpressionRuntimeLogger.cs"));
            Assert.That(content, Does.Contain("UnitySaveLogger.cs"));
        }

        [Test]
        public void ReviewMemo_ContainsTraceableTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "00_KernelArchitectureOverviewReview.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-RV-01"));
            Assert.That(content, Does.Contain("TC-RV-02"));
            Assert.That(content, Does.Contain("TC-RV-03"));
        }

        [Test]
        public void OverviewSpec_ContainsRootLevelTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "00_KernelArchitectureOverviewSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-00-01"));
            Assert.That(content, Does.Contain("TC-00-02"));
            Assert.That(content, Does.Contain("TC-00-03"));
            Assert.That(content, Does.Contain("TC-00-04"));
        }

        [Test]
        public void KernelIRSpec_ContainsIRLevelTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "01_KernelIRSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-01-01"));
            Assert.That(content, Does.Contain("TC-01-02"));
            Assert.That(content, Does.Contain("TC-01-03"));
            Assert.That(content, Does.Contain("TC-01-04"));
            Assert.That(content, Does.Contain("TC-01-05"));
        }

        [Test]
        public void KernelIRSpec_ContainsCurrentM22SourceLocationAnchors()
        {
            string kernelIrContent = ReadDoc(Path.Combine("Docs", "v2", "01_KernelIRSpec.md"));
            string unityBridgeContent = ReadDoc(Path.Combine("Docs", "v2", "12_UnityAuthoringBridgeSpec.md"));
            string milestoneContent = ReadDoc(Path.Combine("Docs", "v2", "16_ImplementationMilestoneOrderSpec.md"));

            Assert.That(kernelIrContent, Does.Contain("SourceLocationIR"));
            Assert.That(kernelIrContent, Does.Contain("legacy migration origin"));
            Assert.That(kernelIrContent, Does.Contain("generated source reference"));
            Assert.That(unityBridgeContent, Does.Contain("UnitySourceLocation"));
            Assert.That(unityBridgeContent, Does.Contain("GameObjectPath"));
            Assert.That(milestoneContent, Does.Contain("LegacySourceLocation"));
            Assert.That(milestoneContent, Does.Contain("GeneratedSourceLocation"));
        }

        [Test]
        public void ModuleContributionSpec_ContainsContributionLevelTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "02_ModuleContributionSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-02-01"));
            Assert.That(content, Does.Contain("TC-02-02"));
            Assert.That(content, Does.Contain("TC-02-03"));
            Assert.That(content, Does.Contain("TC-02-04"));
            Assert.That(content, Does.Contain("TC-02-05"));
            Assert.That(content, Does.Contain("TC-02-06"));
        }

        [Test]
        public void VerifiedPlanGenerationSpec_ContainsGenerationLevelTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "03_VerifiedPlanGenerationSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-03-01"));
            Assert.That(content, Does.Contain("TC-03-02"));
            Assert.That(content, Does.Contain("TC-03-03"));
            Assert.That(content, Does.Contain("TC-03-04"));
            Assert.That(content, Does.Contain("TC-03-05"));
            Assert.That(content, Does.Contain("TC-03-06"));
        }

        [Test]
        public void SpecMetadata_DoesNotContainDuplicateTopMatterOrMalformedDependencyIndentation()
        {
            string generationSpec = ReadDoc(Path.Combine("Docs", "v2", "03_VerifiedPlanGenerationSpec.md"));
            string diagnosticsSpec = ReadDoc(Path.Combine("Docs", "v2", "11_DebugMapAndDiagnosticsSpec.md"));

            Assert.That(Regex.Matches(generationSpec, "^# Verified Plan Generation Specification$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            Assert.That(Regex.Matches(generationSpec, "^## Document Status$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            Assert.That(diagnosticsSpec, Does.Not.Contain("    - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)"));
            Assert.That(diagnosticsSpec, Does.Contain("  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)"));
        }

        [Test]
        public void SpecMetadata_UsesPlainDocumentIdsInNormalizedStatusBlocks()
        {
            string[] specFiles =
            {
                Path.Combine("Docs", "v2", "10_ValueSchemaAndStoreSpec.md"),
                Path.Combine("Docs", "v2", "10_1_ScalarRuntimeAndBindingSpec.md"),
                Path.Combine("Docs", "v2", "10_2_DynamicValueEvaluationSpec.md"),
                Path.Combine("Docs", "v2", "12_UnityAuthoringBridgeSpec.md"),
                Path.Combine("Docs", "v2", "13_LegacyCompatBoundarySpec.md"),
                Path.Combine("Docs", "v2", "14_PerformanceBudgetAndRuntimeRulesSpec.md"),
                Path.Combine("Docs", "v2", "15_TestAndValidationSpec.md"),
                Path.Combine("Docs", "v2", "17_AssemblyDefinitionAndCompileBoundarySpec.md"),
            };

            foreach (string specFile in specFiles)
            {
                string content = ReadDoc(specFile);
                StringReader reader = new StringReader(content);
                string? line;
                string? documentIdLine = null;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("- Document ID:"))
                    {
                        documentIdLine = line;
                        break;
                    }
                }

                Assert.That(documentIdLine, Is.Not.Null, "Missing Document ID line: " + specFile);
                Assert.That(documentIdLine, Does.Not.Contain("`"), "Document ID must be plain text: " + specFile);
            }
        }

        [Test]
        public void ScopeGraphRuntimeSpec_ContainsScopeLevelTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "07_ScopeGraphRuntimeSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-07-01"));
            Assert.That(content, Does.Contain("TC-07-02"));
            Assert.That(content, Does.Contain("TC-07-03"));
            Assert.That(content, Does.Contain("TC-07-04"));
            Assert.That(content, Does.Contain("TC-07-05"));
            Assert.That(content, Does.Contain("TC-07-06"));
        }

        [Test]
        public void ImplementationMilestoneSpec_ContainsOrderingRulesAndTestCases()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "16_ImplementationMilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("## Test Cases"));
            Assert.That(content, Does.Contain("TC-16-01"));
            Assert.That(content, Does.Contain("TC-16-02"));
            Assert.That(content, Does.Contain("TC-16-03"));
            Assert.That(content, Does.Contain("M13.4 hot-path allocation tests"));
            Assert.That(content, Does.Contain("M13.5 performance report output"));
            Assert.That(content, Does.Contain("M13.6 regression thresholds for allocation, elapsed time, baseline delta, and marker presence"));
            Assert.That(content, Does.Contain("resolve, handle validation, tick dispatch, command dispatch, value read or write, dynamic cached read, and diagnostics-disabled trace path"));
            Assert.That(content, Does.Contain("TC-16-04"));
            Assert.That(content, Does.Contain("TC-16-05"));
            Assert.That(content, Does.Contain("M1"));
            Assert.That(content, Does.Contain("M6"));
            Assert.That(content, Does.Contain("M15"));
        }

        [Test]
        public void PerformanceBudgetSpec_ContainsPerformanceReportFormatAndThresholdLanguage()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "14_PerformanceBudgetAndRuntimeRulesSpec.md"));

            Assert.That(content, Does.Contain("## Performance Report Format"));
            Assert.That(content, Does.Contain("PerformanceReport.json"));
            Assert.That(content, Does.Contain("PerformanceReport.md"));
            Assert.That(content, Does.Contain("allocation"));
            Assert.That(content, Does.Contain("baseline"));
            Assert.That(content, Does.Contain("PERF_BENCHMARK_THRESHOLD_REGRESSION"));
            Assert.That(content, Does.Contain("expected max elapsed milliseconds"));
        }

        [Test]
        public void AssemblyDefinitionSpec_ContainsCompileBoundaryRules()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2", "17_AssemblyDefinitionAndCompileBoundarySpec.md"));

            Assert.That(content, Does.Contain("## Document Status"));
            Assert.That(content, Does.Contain("## Assembly Layer Philosophy"));
            Assert.That(content, Does.Contain("## Allowed Dependency Matrix"));
            Assert.That(content, Does.Contain("## Forbidden Dependency Matrix"));
            Assert.That(content, Does.Contain("## Required Test Cases"));
            Assert.That(content, Does.Contain("GameLib.Foundation"));
            Assert.That(content, Does.Contain("GameLib.Kernel.Diagnostics.Unity"));
            Assert.That(content, Does.Contain("GameLib.Legacy.Compat"));
            Assert.That(content, Does.Contain("Unity Test Framework"));
            Assert.That(content, Does.Contain("VContainer"));
            Assert.That(content, Does.Contain("noEngineReferences"));
        }
    }
}
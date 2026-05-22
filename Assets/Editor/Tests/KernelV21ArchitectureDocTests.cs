using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelV21ArchitectureDocTests
    {
        static string ReadDoc(string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(fullPath), Is.True, "Missing doc file: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        [Test]
        public void V21Readme_ExposesIndexPackageAndBaselineFreezePolicy()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "README.md"));

            Assert.That(content, Does.Contain("Index/README.md"));
            Assert.That(content, Does.Contain("07_KernelV21MigrationMilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("baseline ledger"));
            Assert.That(content, Does.Contain("preservation floor ledger"));
            Assert.That(content, Does.Contain("proof-anchor catalog"));
            Assert.That(content, Does.Contain("TC-V21-README-11"));
            Assert.That(content, Does.Contain("TC-V21-README-12"));
        }

        [Test]
        public void V21IndexReadme_ExposesMigrationSpecificArtifactsWithoutForkingV2M0()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "Index", "README.md"));

            Assert.That(content, Does.Contain("KernelV21BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV21PreservationFloorLedger.md"));
            Assert.That(content, Does.Contain("KernelV21ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("../../v2/Index/KernelV2ConceptMap.md"));
            Assert.That(content, Does.Contain("../../v2/Index/ForbiddenPatternRegistry.md"));
            Assert.That(content, Does.Contain("../../v2/Index/CrossSpecDependencyMatrix.md"));
            Assert.That(content, Does.Contain("../../v2/Index/ExistingAnchorInventory.md"));
            Assert.That(content, Does.Contain("TC-V21-INDEX-01"));
            Assert.That(content, Does.Contain("TC-V21-INDEX-05"));
        }

        [Test]
        public void V21BaselineLedger_JoinsAllClaimCriticalDomainsWithStableRows()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "Index", "KernelV21BaselineLedger.md"));

            Assert.That(content, Does.Contain("## Joined Baseline Ledger"));
            Assert.That(content, Does.Contain("V21-BL-BOOT-001"));
            Assert.That(content, Does.Contain("V21-BL-BOOT-004"));
            Assert.That(content, Does.Contain("V21-BL-SCOPE-001"));
            Assert.That(content, Does.Contain("V21-BL-CMD-001"));
            Assert.That(content, Does.Contain("V21-BL-VALUE-001"));
            Assert.That(content, Does.Contain("V21-BL-GAME-001"));
            Assert.That(content, Does.Contain("V21-BL-RES-002"));
            Assert.That(content, Does.Contain("Primary proof family"));
            Assert.That(content, Does.Contain("TC-V21-BL-01"));
        }

        [Test]
        public void V21PreservationFloorLedger_SeparatesGlobalFloorFromWaveLocalContinuityAndReplaceableSurfaces()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "Index", "KernelV21PreservationFloorLedger.md"));

            Assert.That(content, Does.Contain("V21-PF-001"));
            Assert.That(content, Does.Contain("V21-PF-002"));
            Assert.That(content, Does.Contain("V21-PF-003"));
            Assert.That(content, Does.Contain("GlobalFloor"));
            Assert.That(content, Does.Contain("WaveLocalContinuity"));
            Assert.That(content, Does.Contain("Replaceable"));
            Assert.That(content, Does.Contain("QuarantineOnly"));
            Assert.That(content, Does.Contain("SceneChangeCommandData.cs"));
            Assert.That(content, Does.Contain("KernelScopeHost.cs"));
            Assert.That(content, Does.Contain("TC-V21-PF-01"));
        }

        [Test]
        public void V21ProofAnchorCatalog_SeparatesProofFamiliesAndConcreteAnchors()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "Index", "KernelV21ProofAnchorCatalog.md"));

            Assert.That(content, Does.Contain("Live boot"));
            Assert.That(content, Does.Contain("Direct-play reference"));
            Assert.That(content, Does.Contain("Representative gameplay"));
            Assert.That(content, Does.Contain("Residue hardening"));
            Assert.That(content, Does.Contain("V21-PA-DIRECT-002"));
            Assert.That(content, Does.Contain("AuthoringBridgeDirectPlayTests.cs"));
            Assert.That(content, Does.Contain("LegacyCompatBoundaryTests.cs"));
            Assert.That(content, Does.Contain("KernelDiagnosticsAsmdefBoundaryTests.cs"));
            Assert.That(content, Does.Contain("CommandExecutionTrace.cs"));
            Assert.That(content, Does.Contain("TC-V21-PA-01"));
        }

        [Test]
        public void V21Overview_ReferencesBaselineFreezeArtifacts()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "00_KernelV21MigrationOverviewSpec.md"));

            Assert.That(content, Does.Contain("## V21-M0 Baseline Freeze Artifacts"));
            Assert.That(content, Does.Contain("Index/README.md"));
            Assert.That(content, Does.Contain("KernelV21BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV21PreservationFloorLedger.md"));
            Assert.That(content, Does.Contain("KernelV21ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("../v2/Index/KernelV2ConceptMap.md"));
            Assert.That(content, Does.Contain("TC-V21-00-06"));
            Assert.That(content, Does.Contain("TC-V21-00-07"));
        }

        [Test]
        public void V21WaveDSpec_ContainsBlackboardAndVarMigrationBoundary()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "04_WaveDValueBlackboardAndVarCutoverSpec.md"));

            Assert.That(content, Does.Contain("BlackboardAuthoring"));
            Assert.That(content, Does.Contain("BlackboardMB"));
            Assert.That(content, Does.Contain("VarIdResolver"));
            Assert.That(content, Does.Contain("VarKeyRegistryLocator"));
            Assert.That(content, Does.Contain("ValueStore"));
        }

        [Test]
        public void V21WaveCSpec_ContainsCommandRunnerAuthoringSplit()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "03_WaveCCommandDispatchCutoverSpec.md"));

            Assert.That(content, Does.Contain("CommandRunnerAuthoring"));
            Assert.That(content, Does.Contain("CommandRunnerMB"));
            Assert.That(content, Does.Contain("CommandCatalogService"));
        }

        [Test]
        public void V21MilestoneSpec_M0_NamesCanonicalArtifacts()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.1", "07_KernelV21MigrationMilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("KernelV21BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV21PreservationFloorLedger.md"));
            Assert.That(content, Does.Contain("KernelV21ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("Index/README.md"));
            Assert.That(content, Does.Contain("TC-V21-MO-10"));
        }
    }
}


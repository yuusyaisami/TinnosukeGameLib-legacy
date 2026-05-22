#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelM154GateScriptTests
    {
        [Test]
        public void RunM154GateScript_ContainsExpectedLaneOrderAndCommands()
        {
            string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "Run-M15.4Gate.ps1"));
            Assert.That(File.Exists(scriptPath), Is.True, "Missing gate script: Tools/Run-M15.4Gate.ps1");

            string content = File.ReadAllText(scriptPath);

            Assert.That(content, Does.Contain("Invoke-CheckedCommand -DisplayName \"Build\""));
            Assert.That(content, Does.Contain("& $dotNetExe build $solutionPath -v minimal"));
            Assert.That(content, Does.Contain("Run-UnityTests.ps1"));
            Assert.That(content, Does.Contain("-Platform \"EditMode\""));
            Assert.That(content, Does.Contain("-Platform \"PlayMode\""));
            Assert.That(content, Does.Contain("KernelV22LiveBootBundleTests"));
            Assert.That(content, Does.Contain("AuthoringBridgeDirectPlayTests"));
            Assert.That(content, Does.Contain("KernelMinimalBootPlayModeTests"));
            Assert.That(content, Does.Contain("KernelV22RepresentativeGameSceneBundleTests"));
            Assert.That(content, Does.Contain("GameStateMachineMigrationTests"));
            Assert.That(content, Does.Contain("ConversationDialogueMigrationTests"));
            Assert.That(content, Does.Contain("GridObjectAuthorityMigrationTests"));
            Assert.That(content, Does.Contain("TraitListAuthorityMigrationTests"));
            Assert.That(content, Does.Contain("StatusEffectServiceDependencyCaptureTests"));
            Assert.That(content, Does.Contain("GameplayAuthorityRegressionTests"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternTests"));
            Assert.That(content, Does.Not.Contain("KernelM64ResidueStaticGateTests"));
            Assert.That(content, Does.Contain("KernelTestArtifactWriterTests"));
            Assert.That(content, Does.Contain("KernelPerformanceRegressionGateTests"));
            Assert.That(content, Does.Contain("LegacyCompatBoundaryTests"));
            Assert.That(content, Does.Contain("KernelDiagnosticsAsmdefBoundaryTests"));
            Assert.That(content, Does.Contain("DiagnosticCodeTraceabilityTests"));
            Assert.That(content, Does.Contain("TinnosukeGameLib.Tests.Editor.KernelTestArtifactWriterTests"));
            Assert.That(content, Does.Contain("TinnosukeGameLib.Tests.Editor.KernelDiagnosticsModelTests"));
            Assert.That(content, Does.Contain("TinnosukeGameLib.Tests.Editor.KernelDiagnosticServiceTests"));
            Assert.That(content, Does.Contain("Game.Editor.Tests.KernelPerformanceAllocationTests"));

            AssertLaneOrder(content, new[]
            {
                "Build",
                "EditMode validation",
                "EditMode generation",
                "EditMode live-boot bundle",
                "PlayMode minimal boot",
                "EditMode representative gameplay bundle",
                "Static forbidden-pattern tests",
                "Diagnostics snapshot tests",
                "Performance smoke tests",
                "Legacy-boundary tests",
            });
        }

        static void AssertLaneOrder(string content, string[] markers)
        {
            int previousIndex = -1;
            for (int i = 0; i < markers.Length; i++)
            {
                int currentIndex = content.IndexOf(markers[i], StringComparison.Ordinal);
                Assert.That(currentIndex, Is.GreaterThan(previousIndex), "Expected marker to appear in order: " + markers[i]);
                previousIndex = currentIndex;
            }
        }
    }
}
using System.IO;
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
            Assert.That(content, Does.Contain("Run-UnityTests.ps1"));
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
            Assert.That(content, Does.Contain("TC-16-04"));
            Assert.That(content, Does.Contain("TC-16-05"));
            Assert.That(content, Does.Contain("M1"));
            Assert.That(content, Does.Contain("M6"));
            Assert.That(content, Does.Contain("M15"));
        }
    }
}
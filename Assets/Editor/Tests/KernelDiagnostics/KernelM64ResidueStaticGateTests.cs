using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelM64ResidueStaticGateTests
    {
        static readonly string LtsRootPath = Path.Combine(
            KernelForbiddenPatternScanner.ProjectRootPath,
            "Assets",
            "GameLib",
            "Script",
            "Common",
            "LTS");

        [Test]
        public void ResidueRoots_ReportTransformParentInferenceAnchors()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME");

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanTargetRuntimeRoots(new[] { LtsRootPath }, rule);

            Assert.That(
                HasViolation(violations, rule.RuleId, "Assets/GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs"),
                Is.True,
                KernelForbiddenPatternScanner.FormatViolations(rule, violations));
            Assert.That(
                HasViolation(violations, rule.RuleId, "Assets/GameLib/Script/Common/LTS/Runtime/KernelScopeHost.cs"),
                Is.True,
                KernelForbiddenPatternScanner.FormatViolations(rule, violations));
        }

        [Test]
        public void ResidueRoots_ReportDirectLoggingAnchors()
        {
            ForbiddenPatternRule[] rules = KernelForbiddenPatternScanner.CreateDebugRules();

            Assert.That(
                HasViolationForAnyRule(rules, "Assets/GameLib/Script/Common/LTS/LTSLog.cs"),
                Is.True,
                "Expected Common/LTS residue scanning to surface direct Debug logging in LTSLog.cs.");
            Assert.That(
                HasViolationForAnyRule(rules, "Assets/GameLib/Script/Common/LTS/Lifecycle/Service/RuntimeScopeLifecycleService.cs"),
                Is.True,
                "Expected Common/LTS residue scanning to surface direct Debug logging in RuntimeScopeLifecycleService.cs.");
        }

        static ForbiddenPatternRule GetForbiddenApiRule(string ruleId)
        {
            ForbiddenPatternRule[] rules = KernelForbiddenPatternScanner.CreateForbiddenApiRules();
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].RuleId == ruleId)
                    return rules[i];
            }

            throw new AssertionException("Missing forbidden API rule: " + ruleId);
        }

        static bool HasViolation(IReadOnlyList<ForbiddenPatternViolation> violations, string ruleId, string filePath)
        {
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].RuleId == ruleId && violations[i].FilePath == filePath)
                    return true;
            }

            return false;
        }

        static bool HasViolationForAnyRule(IReadOnlyList<ForbiddenPatternRule> rules, string filePath)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanTargetRuntimeRoots(new[] { LtsRootPath }, rules[i]);
                if (HasViolation(violations, rules[i].RuleId, filePath))
                    return true;
            }

            return false;
        }
    }
}


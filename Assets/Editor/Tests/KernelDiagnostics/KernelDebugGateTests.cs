using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelDebugGateTests
    {
        [TestCaseSource(nameof(GetRules))]
        public void KernelSources_DoNotContainForbiddenDebugCalls(ForbiddenPatternRule rule)
        {
            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanKernelSources(rule);

            Assert.That(violations, Is.Empty, KernelForbiddenPatternScanner.FormatViolations(rule, violations));
        }

        static ForbiddenPatternRule[] GetRules()
        {
            return KernelForbiddenPatternScanner.CreateDebugRules();
        }
    }
}
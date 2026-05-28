#nullable enable

using System.IO;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class M12_1BridgeAuthorityAuditTests
    {
        [Test]
        public void BuildWorkspaceReport_CapturesRuntimeAndEditorBridgeResidue()
        {
            M12_1BridgeAuthorityAuditReport report = M12_1BridgeAuthorityAuditService.BuildWorkspaceReport();

            Assert.That(report.HasFindings, Is.True, "Expected the M12.1 audit to surface current residual bridge usage.");
            Assert.That(report.HasRuntimeFindings, Is.True, "Expected at least one runtime authority finding.");
            Assert.That(report.HasEditorFindings, Is.True, "Expected at least one editor-side bridge finding.");
            Assert.That(ContainsFinding(report, "M12_1_RULE_RUNTIME_TRYRESOLVE_USAGE", "UISelectionService.cs"), Is.True);
            Assert.That(ContainsFinding(report, "M12_1_RULE_RUNTIME_TRYRESOLVE_USAGE", "BlackboardMB.cs"), Is.True);
            Assert.That(ContainsFinding(report, "STATIC_RULE_RESOURCES_LOAD_IN_KERNEL_RUNTIME", "VarKeyRegistryLocator.cs"), Is.True);
            Assert.That(ContainsFinding(report, "STATIC_RULE_FIND_OBJECTS_BY_TYPE_IN_KERNEL_RUNTIME", "LoadingScreenService.cs"), Is.True);
            Assert.That(ContainsFinding(report, "M12_1_RULE_INSTALL_FEATURE_USAGE", "CommandExecutorBindingExtractionBridge.cs"), Is.True);
        }

        static bool ContainsFinding(M12_1BridgeAuthorityAuditReport report, string ruleId, string fileName)
        {
            for (int index = 0; index < report.Findings.Count; index++)
            {
                M12_1BridgeAuthorityAuditFinding finding = report.Findings[index];
                if (!string.Equals(finding.RuleId, ruleId, System.StringComparison.Ordinal))
                    continue;

                if (string.Equals(Path.GetFileName(finding.FilePath), fileName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
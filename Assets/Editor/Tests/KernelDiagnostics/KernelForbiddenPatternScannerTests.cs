using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelForbiddenPatternScannerTests
    {
        [Test]
        public void ScanText_IgnoresStringAndCommentFalsePositives()
        {
            ForbiddenPatternRule rule = new ForbiddenPatternRule(
                "STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK",
                "Debug.LogError must not be used outside the approved Unity diagnostic sink.",
                "Debug.LogError",
                new System.Text.RegularExpressions.Regex(@"\bDebug\s*\.\s*LogError\s*\(", System.Text.RegularExpressions.RegexOptions.CultureInvariant));

            string source = @"
using UnityEngine;

namespace Sample
{
    public sealed class Demo
    {
        public void Run()
        {
            string text = ""Debug.LogError(fake)"";
            // Debug.LogError(fake);
            /* Debug.LogError(fake); */
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_IgnoresVerbatimAndRawStringFalsePositives()
        {
            ForbiddenPatternRule rule = new ForbiddenPatternRule(
                "STATIC_RULE_RESOURCES_LOAD_IN_KERNEL_RUNTIME",
                "Resources.Load must not be used in Kernel code paths.",
                "Resources.Load",
                new System.Text.RegularExpressions.Regex(@"\bResources\s*\.\s*Load(?:Async)?\s*(?:<[^>]+>)?\s*\(", System.Text.RegularExpressions.RegexOptions.CultureInvariant));

            string source =
@"using UnityEngine;

namespace Sample
{
    public sealed class Demo
    {
        public string A = @""Resources.Load<GameObject>(""""Demo"""")"";
        public string B = " + "\"\"\"Resources.Load<GameObject>(\\\"Demo\\\")\\\"\"\"" + @";
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_AllowListsApprovedUnityDiagnosticSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[1];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            Debug.LogError(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Diagnostics/Unity/UnityLogDiagnosticSink.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_DoesNotAllowUnapprovedDebugCallInsideApprovedFile()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[1];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            Debug.LogError(""not-approved"");
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Diagnostics/Unity/UnityLogDiagnosticSink.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_ReportsForbiddenApiWithStableRuleIdAndLineNumber()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[0];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run()
        {
            var asset = Resources.Load<GameObject>(""Demo"");
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_RESOURCES_LOAD_IN_KERNEL_RUNTIME"));
            Assert.That(violations[0].LineNumber, Is.EqualTo(10));
            Assert.That(violations[0].FilePath, Is.EqualTo("Assets/GameLib/Script/Kernel/Sample/Demo.cs"));
        }

        [Test]
        public void ScanText_ReportsQualifiedFindObjectsByTypeCalls()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[1];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run()
        {
            var objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_FIND_OBJECTS_BY_TYPE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsDebugLogExceptionCalls()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[3];

            string source = @"
using System;
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_EXCEPTION_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_ReportsDebugLogWarningCallsOutsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[2];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            Debug.LogWarning(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_WARNING_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_ReportsGetComponentsInChildrenCalls()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[2];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo : MonoBehaviour
    {
        public void Run()
        {
            var components = GetComponentsInChildren<Transform>();
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_GET_COMPONENTS_IN_CHILDREN_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_AllowListsApprovedDebugLogCallInsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[0];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            Debug.Log(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Diagnostics/Unity/UnityLogDiagnosticSink.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_ReportsDebugLogCallsOutsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[0];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            Debug.Log(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_ReportsAliasBasedDebugLogErrorCallsOutsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[1];

            string source = @"
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            UDebug.LogError(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_ReportsUsingStaticDebugLogWarningCallsOutsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[2];

            string source = @"
using UnityEngine;
using static UnityEngine.Debug;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run(string message)
        {
            LogWarning(message);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_WARNING_OUTSIDE_SINK"));
        }

        [Test]
        public void ScanText_AllowListsApprovedDebugLogExceptionCallInsideApprovedSink()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[3];

            string source = @"
using System;
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    public sealed class Demo
    {
        public void Run(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Diagnostics/Unity/UnityLogDiagnosticSink.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_ReportsTransformParentScopeInferenceCalls()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[3];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo : MonoBehaviour
    {
        public Transform ResolveParent(Transform current)
        {
            Transform directParent = current.parent;
            return current.transform.parent;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(2));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME"));
            Assert.That(violations[1].RuleId, Is.EqualTo("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_IgnoresTransformParentInsideStringsAndComments()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[3];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public string Text = ""current.transform.parent"";

        public void Run()
        {
            // current.transform.parent
            /* current.transform.parent */
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_DoesNotReportUnrelatedParentIdentifiers()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[3];

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        string parent;
        string transformParent;

        public void Run(string parentValue)
        {
            parent = parentValue;
            transformParent = parentValue;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_ReportsTransformParentWalkInferenceCalls()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[3];

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public Transform ResolveRoot(Transform current)
        {
            while (current.parent != null)
            {
                current = current.parent;
            }

            return current;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(2));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME"));
            Assert.That(violations[1].RuleId, Is.EqualTo("STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_DoesNotReportUppercaseParentMembers()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateForbiddenApiRules()[3];

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class Node
    {
        public Node Parent { get; set; }
    }

    public sealed class Demo
    {
        public Node Resolve(Node current)
        {
            return current.Parent;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }
    }
}

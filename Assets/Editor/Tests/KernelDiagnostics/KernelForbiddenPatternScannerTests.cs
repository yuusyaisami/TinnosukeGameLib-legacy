using System.IO;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelForbiddenPatternScannerTests
    {
        private static ForbiddenPatternRule GetForbiddenApiRule(string ruleId)
        {
            ForbiddenPatternRule[] rules = KernelForbiddenPatternScanner.CreateForbiddenApiRules();
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].RuleId == ruleId)
                    return rules[i];
            }

            throw new AssertionException("Missing forbidden API rule: " + ruleId);
        }

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

        [Test]
        public void ScanText_ReportsFindFirstObjectByTypeCalls()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_FIND_FIRST_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME");

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public GameObject Resolve()
        {
            return UnityEngine.Object.FindFirstObjectByType<GameObject>();
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_FIND_FIRST_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsFindAnyObjectByTypeCalls()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_FIND_ANY_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME");

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public GameObject Resolve()
        {
            return Object.FindAnyObjectByType<GameObject>();
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_FIND_ANY_OBJECT_BY_TYPE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsGetComponentsInParentCalls()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_GET_COMPONENTS_IN_PARENT_IN_KERNEL_RUNTIME");

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo : MonoBehaviour
    {
        public Transform[] Resolve()
        {
            return GetComponentsInParent<Transform>();
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_GET_COMPONENTS_IN_PARENT_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsGameObjectFindCalls()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_GAMEOBJECT_FIND_IN_KERNEL_RUNTIME");

            string source = @"
using UnityEngine;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public GameObject Resolve()
        {
            return GameObject.Find(""Demo"");
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_GAMEOBJECT_FIND_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsActivatorCreateInstanceCalls()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_ACTIVATOR_CREATE_INSTANCE_IN_KERNEL_RUNTIME");

            string source = @"
using System;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public object Create(Type type)
        {
            return Activator.CreateInstance(type)!;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_ACTIVATOR_CREATE_INSTANCE_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsCommandExecutorListDiscovery()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_COMMAND_EXECUTOR_LIST_DISCOVERY_IN_KERNEL_RUNTIME");

            string source = @"
using System.Collections.Generic;

namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public IReadOnlyList<ICommandExecutor> Executors;
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_COMMAND_EXECUTOR_LIST_DISCOVERY_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_ReportsCommandKeyResolverStringDispatch()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_COMMAND_KEY_RESOLVER_STRING_DISPATCH_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public bool Run(bool allowRuntimeFallback)
        {
            return AllowRuntimeFallback(allowRuntimeFallback);
        }

        bool AllowRuntimeFallback(bool allowRuntimeFallback)
        {
            return allowRuntimeFallback;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_COMMAND_KEY_RESOLVER_STRING_DISPATCH_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_DoesNotReportCommandKeyResolverTypeDeclarations()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_COMMAND_KEY_RESOLVER_STRING_DISPATCH_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class CommandKeyResolver { }

    public sealed class Demo
    {
        private CommandKeyResolver resolver;
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanFiles_ReportsCommandKeyResolverStringDispatchInRealFile()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_COMMAND_KEY_RESOLVER_STRING_DISPATCH_IN_KERNEL_RUNTIME");
            string resolverPath = Path.Combine(KernelForbiddenPatternScanner.ProjectRootPath, "Assets", "GameLib", "Script", "Common", "Commands", "VNext", "Catalog", "CommandKeyResolver.cs");
            string rootPath = Path.Combine(KernelForbiddenPatternScanner.ProjectRootPath, "Assets", "GameLib", "Script", "Common", "Commands", "VNext");

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanFiles(new[] { resolverPath }, rule, new[] { rootPath });

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_COMMAND_KEY_RESOLVER_STRING_DISPATCH_IN_KERNEL_RUNTIME"));
            Assert.That(violations[0].FilePath, Is.EqualTo("Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs"));
        }

        [Test]
        public void ScanText_ReportsStableKeyLookupOutsideValidationPaths()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_RUNTIME_STABLE_KEY_LOOKUP_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public string Resolve(ValueNode value)
        {
            return value.StableKey;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Runtime/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_RUNTIME_STABLE_KEY_LOOKUP_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_IgnoresStableKeyLookupInsideValidationPaths()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_RUNTIME_STABLE_KEY_LOOKUP_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Validation
{
    public sealed class Demo
    {
        public string Resolve(ValueNode value)
        {
            return value.StableKey;
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Validation/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanText_ReportsLifecycleHandlerInterfaceScans()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Sample
{
    public sealed class Demo
    {
        public void Run()
        {
            CollectHandlers<IScopeAcquireHandler>();
            CollectHandlers<IScopeTickHandler>();
            CollectHandlers<IScopeLateTickHandler>();
            CollectHandlers<IScopeReleaseHandler>();
        }

        void CollectHandlers<THandler>()
        {
        }
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Has.Length.EqualTo(4));
            Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME"));
            Assert.That(violations[1].RuleId, Is.EqualTo("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME"));
            Assert.That(violations[2].RuleId, Is.EqualTo("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME"));
            Assert.That(violations[3].RuleId, Is.EqualTo("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME"));
        }

        [Test]
        public void ScanText_DoesNotReportLifecycleHandlerTypeDeclarations()
        {
            ForbiddenPatternRule rule = GetForbiddenApiRule("STATIC_RULE_LIFECYCLE_INTERFACE_SCAN_IN_KERNEL_RUNTIME");

            string source = @"
namespace Game.Kernel.Sample
{
    public interface IScopeAcquireHandler { }
    public interface IScopeTickHandler { }
    public interface IScopeLateTickHandler { }
    public interface IScopeReleaseHandler { }

    public sealed class Demo : IScopeAcquireHandler, IScopeTickHandler, IScopeLateTickHandler, IScopeReleaseHandler
    {
    }
}";

            ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanText("Assets/GameLib/Script/Kernel/Sample/Demo.cs", source, rule);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ScanFiles_AllowsCustomTargetRuntimeRoots()
        {
            ForbiddenPatternRule rule = KernelForbiddenPatternScanner.CreateDebugRules()[0];
            string rootPath = Path.Combine(Path.GetTempPath(), "KernelForbiddenPatternScannerTests_" + System.Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(rootPath, "Runtime", "Sample.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "using UnityEngine;\npublic sealed class Sample { public void Run() { Debug.Log(\"x\"); } }");

            try
            {
                ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanFiles(new[] { filePath }, rule, new[] { rootPath });

                Assert.That(violations, Has.Length.EqualTo(1));
                Assert.That(violations[0].RuleId, Is.EqualTo("STATIC_RULE_DEBUG_LOG_OUTSIDE_SINK"));
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);

                if (Directory.Exists(rootPath))
                    Directory.Delete(rootPath, true);
            }
        }
    }
}

using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using NUnit.Framework;
using UnityEngine;

using KernelModuleKind = Game.Kernel.IR.ModuleKind;
using KernelModuleVersion = Game.Kernel.IR.ModuleVersion;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ScopeAuthoringExtractionTests
    {
        [Test]
        public void Extraction_UsesExplicitRootsAndStableOrdering()
        {
            GameObject firstRootObject = new GameObject("BattleRootB");
            GameObject secondRootObject = new GameObject("BattleRootA");

            try
            {
                ScopeAuthoringRoot firstRoot = firstRootObject.AddComponent<ScopeAuthoringRoot>();
                ScopeAuthoringRoot secondRoot = secondRootObject.AddComponent<ScopeAuthoringRoot>();

                ConfigureRoot(firstRoot, 21, "BattleModuleB", "Assets/Scenes/BattleB.unity", "BattleRootB", "ScopeAuthoringRoot", "module");
                ConfigureRoot(secondRoot, 11, "BattleModuleA", "Assets/Scenes/BattleA.unity", "BattleRootA", "ScopeAuthoringRoot", "module");

                CreateLink(firstRootObject.transform, "LateScope", 42, "Assets/Scenes/BattleB.unity", "BattleRootB/LateScope", "ScopeAuthoringLink", "scope");
                CreateLink(firstRootObject.transform, "EarlyScope", 7, "Assets/Scenes/BattleB.unity", "BattleRootB/EarlyScope", "ScopeAuthoringLink", "scope");

                CreateLink(secondRootObject.transform, "MiddleScope", 19, "Assets/Scenes/BattleA.unity", "BattleRootA/MiddleScope", "ScopeAuthoringLink", "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(new[] { firstRoot, secondRoot });

                Assert.That(report.IsValid, Is.True);
                Assert.That(report.Issues.Count, Is.EqualTo(0));
                Assert.That(report.Contributions.Count, Is.EqualTo(2));
                Assert.That(report.Contributions[0].ModuleId, Is.EqualTo(new ModuleId(11)));
                Assert.That(report.Contributions[1].ModuleId, Is.EqualTo(new ModuleId(21)));
                Assert.That(report.Contributions[0].SourceLocation.UnitySource!.Value.GameObjectPath, Is.EqualTo("BattleRootA"));
                Assert.That(report.Contributions[0].SourceLocation.UnitySource!.Value.ComponentType, Is.EqualTo("ScopeAuthoringRoot"));

                ModuleContributionData firstContribution = report.Contributions[0];
                Assert.That(firstContribution.Items.Length, Is.EqualTo(1));
                Assert.That(firstContribution.Items[0].StableId, Is.EqualTo("scope-authoring-0000000019"));
                Assert.That(firstContribution.Items[0].Source, Is.EqualTo(ContributionSource.SceneObject));

                ModuleContributionData secondContribution = report.Contributions[1];
                Assert.That(secondContribution.Items.Length, Is.EqualTo(2));
                Assert.That(secondContribution.Items[0].StableId, Is.EqualTo("scope-authoring-0000000007"));
                Assert.That(secondContribution.Items[1].StableId, Is.EqualTo("scope-authoring-0000000042"));
                Assert.That(secondContribution.Items[0].SourceLocation.UnitySource!.Value.GameObjectPath, Is.EqualTo("BattleRootB/EarlyScope"));
            }
            finally
            {
                Object.DestroyImmediate(firstRootObject);
                Object.DestroyImmediate(secondRootObject);
            }
        }

        [Test]
        public void Extraction_ReportsDuplicateIdsAndInvalidTrace()
        {
            GameObject rootObject = new GameObject("BrokenRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 31, "BrokenModule", "Assets/Scenes/Broken.unity", "BrokenRoot", "ScopeAuthoringRoot", "module");

                CreateLink(rootObject.transform, "BrokenScopeA", 5, "Assets/Scenes/Broken.unity", "BrokenRoot/BrokenScopeA", "ScopeAuthoringLink", "scope");
                CreateLink(rootObject.transform, "BrokenScopeB", 5, "Assets/Scenes/Broken.unity", "BrokenRoot/BrokenScopeB", "ScopeAuthoringLink", "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Contributions.Count, Is.EqualTo(0));
                Assert.That(report.Issues.Count, Is.GreaterThan(0));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_DUPLICATE_ID"));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_RejectsMissingModuleMetadataAndSourceTrace()
        {
            GameObject rootObject = new GameObject("MissingMetadataRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                CreateLink(rootObject.transform, "LonelyScope", 9, "Assets/Scenes/MissingMetadata.unity", "MissingMetadataRoot/LonelyScope", "ScopeAuthoringLink", "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Contributions.Count, Is.EqualTo(0));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_ROOT_INVALID"));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_RejectsNestedRootsAndFailsClosed()
        {
            GameObject parentRootObject = new GameObject("ParentRoot");
            GameObject childRootObject = new GameObject("ChildRoot");

            try
            {
                ScopeAuthoringRoot parentRoot = parentRootObject.AddComponent<ScopeAuthoringRoot>();
                ScopeAuthoringRoot childRoot = childRootObject.AddComponent<ScopeAuthoringRoot>();

                childRootObject.transform.SetParent(parentRootObject.transform, false);

                ConfigureRoot(parentRoot, 41, "ParentModule", "Assets/Scenes/Parent.unity", "ParentRoot", "ScopeAuthoringRoot", "module");
                ConfigureRoot(childRoot, 42, "ChildModule", "Assets/Scenes/Child.unity", "ParentRoot/ChildRoot", "ScopeAuthoringRoot", "module");

                CreateLink(parentRootObject.transform, "ParentScope", 11, "Assets/Scenes/Parent.unity", "ParentRoot/ParentScope", "ScopeAuthoringLink", "scope");
                CreateLink(childRootObject.transform, "ChildScope", 12, "Assets/Scenes/Child.unity", "ParentRoot/ChildRoot/ChildScope", "ScopeAuthoringLink", "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(parentRoot);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Contributions.Count, Is.EqualTo(0));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_NESTED_ROOT"));
            }
            finally
            {
                Object.DestroyImmediate(childRootObject);
                Object.DestroyImmediate(parentRootObject);
            }
        }

        [Test]
        public void Extraction_RejectsNullRootsAndFailsClosed()
        {
            GameObject rootObject = new GameObject("NullRootCheck");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 51, "NullRootModule", "Assets/Scenes/NullRoot.unity", "NullRootCheck", "ScopeAuthoringRoot", "module");
                CreateLink(rootObject.transform, "NullRootScope", 21, "Assets/Scenes/NullRoot.unity", "NullRootCheck/NullRootScope", "ScopeAuthoringLink", "scope");

                ScopeAuthoringRoot? missingRoot = null;
                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(new[] { missingRoot, root });

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Contributions.Count, Is.EqualTo(0));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_ROOT_NULL"));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_RejectsBaseTraceBearingLinksAndFailsClosed()
        {
            GameObject rootObject = new GameObject("BaseTraceRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 61, "BaseTraceModule", "Assets/Scenes/BaseTrace.unity", "BaseTraceRoot", "ScopeAuthoringRoot", "module");

                ScopeAuthoringLink link = CreateLink(rootObject.transform, "BaseTraceScope", 31, "Assets/Scenes/BaseTrace.unity", "BaseTraceRoot/BaseTraceScope", "ScopeAuthoringLink", "scope");
                link.SetBaseSourceLocation(
                    UnityAuthoringSourceKind.PrefabAsset,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Prefabs/BaseTrace.prefab",
                    22001,
                    null,
                    "BaseTraceRoot/BaseTraceScope",
                    "ScopeAuthoringLink",
                    "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Contributions.Count, Is.EqualTo(0));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_BASE_TRACE_UNSUPPORTED"));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ExtractionReport_ConvertsValidationIssuesToDiagnostics()
        {
            GameObject rootObject = new GameObject("DiagnosticRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 71, "DiagnosticModule", "Assets/Scenes/Diagnostic.unity", "DiagnosticRoot", "ScopeAuthoringRoot", "module");

                ScopeAuthoringLink link = CreateLink(rootObject.transform, "DiagnosticScope", 41, "Assets/Scenes/Diagnostic.unity", "DiagnosticRoot/DiagnosticScope", "ScopeAuthoringLink", "scope");
                link.SetBaseSourceLocation(
                    UnityAuthoringSourceKind.PrefabAsset,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Prefabs/Diagnostic.prefab",
                    22001,
                    null,
                    "DiagnosticRoot/DiagnosticScope",
                    "ScopeAuthoringLink",
                    "scope");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);
                KernelDiagnostic[] diagnostics = report.ToKernelDiagnostics();

                Assert.That(report.IsValid, Is.False);
                Assert.That(diagnostics.Length, Is.EqualTo(1));
                Assert.That(diagnostics[0].Code.Value, Is.EqualTo("UNITY_SCOPE_AUTHORING_BASE_TRACE_UNSUPPORTED"));
                Assert.That(diagnostics[0].Domain, Is.EqualTo(DiagnosticDomain.Validation));
                Assert.That(diagnostics[0].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Build));
                Assert.That(diagnostics[0].Context.RuntimeIdentities.Count, Is.EqualTo(2));
                Assert.That(diagnostics[0].Context.RuntimeIdentities[0], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.Module, 71)));
                Assert.That(diagnostics[0].Context.RuntimeIdentities[1], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, 41)));
                Assert.That(HasPayloadEntry(diagnostics[0], "AuthoringSourceLocation"), Is.True);
                Assert.That(HasPayloadEntry(diagnostics[0], "AuthoringBaseSourceLocation"), Is.True);
                Assert.That(HasPayloadEntry(diagnostics[0], "AuthoringCategory"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        static void ConfigureRoot(ScopeAuthoringRoot root, int moduleId, string moduleName, string assetPath, string gameObjectPath, string componentType, string propertyPath)
        {
            root.SetModuleMetadata(moduleId, moduleName, KernelModuleKind.Feature, new KernelModuleVersion(1));
            root.SetContributionAvailability("Battle", "Windows", "Desktop", ContributionEnvironment.Release);
            root.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                12001,
                assetPath,
                gameObjectPath,
                componentType,
                propertyPath);
        }

        static ScopeAuthoringLink CreateLink(Transform parent, string name, int authoringId, string assetPath, string gameObjectPath, string componentType, string propertyPath)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);

            ScopeAuthoringLink link = child.AddComponent<ScopeAuthoringLink>();
            link.SetAuthoringId(new ScopeAuthoringId(authoringId));
            link.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                12001,
                assetPath,
                gameObjectPath,
                componentType,
                propertyPath);
            return link;
        }

        static bool HasPayloadEntry(KernelDiagnostic diagnostic, string key)
        {
            IReadOnlyList<DiagnosticPayloadEntry> entries = diagnostic.Payload.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Key == key)
                    return true;
            }

            return false;
        }
    }
}
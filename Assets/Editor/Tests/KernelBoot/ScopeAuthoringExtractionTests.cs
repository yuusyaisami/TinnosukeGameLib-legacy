using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.UI;
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

        [Test]
        public void Extraction_CollectsEntityAndDeclarationAuthoringInputs()
        {
            GameObject rootObject = new GameObject("EntityRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 81, "EntityModule", "Assets/Scenes/Entity.unity", "EntityRoot", "ScopeAuthoringRoot", "module");
                CreateLink(rootObject.transform, "ScopeLink", 51, "Assets/Scenes/Entity.unity", "EntityRoot/ScopeLink", "ScopeAuthoringLink", "scope");

                GameObject entityObject = new GameObject("PlayerEntity");
                entityObject.transform.SetParent(rootObject.transform, false);
                EntityIdentityMB entity = entityObject.AddComponent<EntityIdentityMB>();
                entity.id = "entity.player";
                entity.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/Entity.unity",
                    13001,
                    "Assets/Scenes/Entity.unity",
                    "EntityRoot/PlayerEntity",
                    nameof(EntityIdentityMB),
                    "entity");

                GameObject declarationObject = new GameObject("ButtonDecl");
                declarationObject.transform.SetParent(entityObject.transform, false);
                TestEntityDeclarationMB declaration = declarationObject.AddComponent<TestEntityDeclarationMB>();
                declaration.SetEntityIdentity(entity);
                declaration.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/Entity.unity",
                    13002,
                    "Assets/Scenes/Entity.unity",
                    "EntityRoot/PlayerEntity/ButtonDecl",
                    nameof(TestEntityDeclarationMB),
                    "declaration");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.True);
                Assert.That(report.EntityInputs.Count, Is.EqualTo(1));
                Assert.That(report.DeclarationInputs.Count, Is.EqualTo(1));
                Assert.That(report.EntityInputs[0].OwnerModule, Is.EqualTo(new ModuleId(81)));
                Assert.That(report.EntityInputs[0].EntityRef.Value, Is.EqualTo("entity.player"));
                Assert.That(report.EntityInputs[0].Source.Kind, Is.EqualTo(SourceLocationKind.Unity));
                Assert.That(report.DeclarationInputs[0].OwnerModule, Is.EqualTo(new ModuleId(81)));
                Assert.That(report.DeclarationInputs[0].OwnerEntityRef.Value, Is.EqualTo("entity.player"));
                Assert.That(report.DeclarationInputs[0].Source.Kind, Is.EqualTo(SourceLocationKind.Unity));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_CollectsRepresentativeServiceDeclarationsAndServiceContributionItems()
        {
            GameObject rootObject = new GameObject("NavigationRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 83, "NavigationModule", "Assets/Scenes/Navigation.unity", "NavigationRoot", "ScopeAuthoringRoot", "module");
                CreateLink(rootObject.transform, "ScopeLink", 53, "Assets/Scenes/Navigation.unity", "NavigationRoot/ScopeLink", "ScopeAuthoringLink", "scope");

                EntityIdentityMB entity = CreateEntity(rootObject.transform, "NavigationEntity", "entity.navigation", "Assets/Scenes/Navigation.unity", "NavigationRoot/NavigationEntity", 15001);

                GameObject declarationObject = new GameObject("NavigationDecl");
                declarationObject.transform.SetParent(entity.transform, false);

                UINavigationDeclarationMB declaration = declarationObject.AddComponent<UINavigationDeclarationMB>();
                declaration.SetEntityIdentity(entity);
                declaration.SetServiceIds(5101, 5102, 5103, 5104);
                declaration.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/Navigation.unity",
                    15002,
                    "Assets/Scenes/Navigation.unity",
                    "NavigationRoot/NavigationEntity/NavigationDecl",
                    nameof(UINavigationDeclarationMB),
                    "declaration");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.True);
                Assert.That(report.ServiceDeclarations.Count, Is.EqualTo(2));
                Assert.That(report.ServiceDeclarations[0].ServiceId, Is.EqualTo(new ServiceId(5101)));
                Assert.That(report.ServiceDeclarations[1].ServiceId, Is.EqualTo(new ServiceId(5102)));
                Assert.That(report.ServiceDeclarations[0].OwnerEntityRef.Value, Is.EqualTo("entity.navigation"));
                Assert.That(report.ServiceDeclarations[0].Dependencies.Length, Is.EqualTo(3));
                Assert.That(report.ServiceDeclarations[0].Dependencies[0].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(5103))));
                Assert.That(report.ServiceDeclarations[0].Dependencies[1].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(5104))));
                Assert.That(report.ServiceDeclarations[0].Dependencies[2].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(5102))));
                Assert.That(report.ServiceDeclarations[1].Dependencies.Length, Is.EqualTo(1));
                Assert.That(report.ServiceDeclarations[1].Dependencies[0].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(5103))));
                Assert.That(report.Contributions.Count, Is.EqualTo(1));
                Assert.That(report.Contributions[0].OwnedContributionKinds.Length, Is.EqualTo(2));
                Assert.That(report.Contributions[0].OwnedContributionKinds[0], Is.EqualTo(ContributionKind.ServiceContribution));
                Assert.That(report.Contributions[0].OwnedContributionKinds[1], Is.EqualTo(ContributionKind.ScopeContribution));
                Assert.That(report.Contributions[0].Items.Length, Is.EqualTo(3));
                Assert.That(report.Contributions[0].Items[0].Kind, Is.EqualTo(ContributionKind.ServiceContribution));
                Assert.That(report.Contributions[0].Items[1].Kind, Is.EqualTo(ContributionKind.ServiceContribution));
                Assert.That(report.Contributions[0].Items[2].Kind, Is.EqualTo(ContributionKind.ScopeContribution));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_RejectsDuplicateRepresentativeServiceIds()
        {
            GameObject rootObject = new GameObject("NavigationDuplicateRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 84, "NavigationDuplicateModule", "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot", "ScopeAuthoringRoot", "module");
                CreateLink(rootObject.transform, "ScopeLink", 54, "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot/ScopeLink", "ScopeAuthoringLink", "scope");

                EntityIdentityMB firstEntity = CreateEntity(rootObject.transform, "FirstEntity", "entity.first", "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot/FirstEntity", 16001);
                EntityIdentityMB secondEntity = CreateEntity(rootObject.transform, "SecondEntity", "entity.second", "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot/SecondEntity", 16002);

                UINavigationDeclarationMB firstDeclaration = CreateNavigationDeclaration(firstEntity, "FirstDecl", "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot/FirstEntity/FirstDecl", 16003, 5201, 5202, 5203, 5204);
                _ = firstDeclaration;
                CreateNavigationDeclaration(secondEntity, "SecondDecl", "Assets/Scenes/NavigationDuplicate.unity", "NavigationDuplicateRoot/SecondEntity/SecondDecl", 16004, 5201, 5205, 5206, 5207);

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues[0].Code, Is.EqualTo(ScopeAuthoringValidationCodes.DuplicateServiceDeclaration));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Extraction_RejectsDeclarationBoundOutsideEntityHierarchy()
        {
            GameObject rootObject = new GameObject("OwnerMismatchRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 82, "OwnerMismatchModule", "Assets/Scenes/OwnerMismatch.unity", "OwnerMismatchRoot", "ScopeAuthoringRoot", "module");
                CreateLink(rootObject.transform, "ScopeLink", 52, "Assets/Scenes/OwnerMismatch.unity", "OwnerMismatchRoot/ScopeLink", "ScopeAuthoringLink", "scope");

                GameObject entityObject = new GameObject("BoundEntity");
                entityObject.transform.SetParent(rootObject.transform, false);
                EntityIdentityMB entity = entityObject.AddComponent<EntityIdentityMB>();
                entity.id = "entity.bound";
                entity.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/OwnerMismatch.unity",
                    14001,
                    "Assets/Scenes/OwnerMismatch.unity",
                    "OwnerMismatchRoot/BoundEntity",
                    nameof(EntityIdentityMB),
                    "entity");

                GameObject declarationObject = new GameObject("DetachedDecl");
                declarationObject.transform.SetParent(rootObject.transform, false);
                TestEntityDeclarationMB declaration = declarationObject.AddComponent<TestEntityDeclarationMB>();
                declaration.SetEntityIdentity(entity);
                declaration.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/OwnerMismatch.unity",
                    14002,
                    "Assets/Scenes/OwnerMismatch.unity",
                    "OwnerMismatchRoot/DetachedDecl",
                    nameof(TestEntityDeclarationMB),
                    "declaration");

                ScopeAuthoringExtractionReport report = ScopeAuthoringExtractionService.Extract(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues[0].Code, Is.EqualTo(ScopeAuthoringValidationCodes.DeclarationOwnerMismatch));
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

        static EntityIdentityMB CreateEntity(Transform parent, string name, string entityRef, string assetPath, string gameObjectPath, long localFileId)
        {
            GameObject entityObject = new GameObject(name);
            entityObject.transform.SetParent(parent, false);

            EntityIdentityMB entity = entityObject.AddComponent<EntityIdentityMB>();
            entity.id = entityRef;
            entity.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(EntityIdentityMB),
                "entity");
            return entity;
        }

        static UINavigationDeclarationMB CreateNavigationDeclaration(EntityIdentityMB entity, string name, string assetPath, string gameObjectPath, long localFileId, int navigationServiceId, int inputNavigateServiceId, int selectionServiceId, int controlSchemeServiceId)
        {
            GameObject declarationObject = new GameObject(name);
            declarationObject.transform.SetParent(entity.transform, false);

            UINavigationDeclarationMB declaration = declarationObject.AddComponent<UINavigationDeclarationMB>();
            declaration.SetEntityIdentity(entity);
            declaration.SetServiceIds(navigationServiceId, inputNavigateServiceId, selectionServiceId, controlSchemeServiceId);
            declaration.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(UINavigationDeclarationMB),
                "declaration");
            return declaration;
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

        sealed class TestEntityDeclarationMB : EntityDeclarationMB
        {
        }
    }
}

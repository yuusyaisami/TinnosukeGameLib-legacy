#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class RuntimeLtsShutdownSourceGateTests
    {
        static readonly string[] ExplicitTargetRoots =
        {
            "Assets/GameLib/Script/Kernel",
            "Assets/Editor/KernelBoot",
        };

        static readonly string[] ServiceFacingTargetRoots =
        {
            "Assets/GameLib/Script/Common/Commands/VNext",
            "Assets/GameLib/Script/Common/Search/Core",
            "Assets/GameLib/Script/Common/Variables/Profile",
        };

        static readonly string[] DynamicDiscoveryTargetRoots =
        {
            "Assets/GameLib/Script/Common/Search/Core",
            "Assets/GameLib/Script/Common/FirePattern/Core",
            "Assets/GameLib/Script/Common/Commands/VNext/Catalog",
            "Assets/GameLib/Script/Common/Variables/VarStore/Registry",
            "Assets/GameLib/Script/Common/Variables/VariableKey",
            "Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core",
            "Assets/GameLib/Script/Project/Scene/Channels/Targeting/MB",
            "Assets/GameLib/Script/Project/Scene/Entity/Search",
            "Assets/GameLib/Script/Project/UI/Core/Conversation/Character/Registry",
            "Assets/GameLib/Script/Project/Scene/RoomMap/Registry",
        };

        static readonly string[] DynamicRegistryQuarantineRoots =
        {
            "Assets/GameLib/Script/Common/Search/Core",
            "Assets/GameLib/Script/Common/FirePattern/Core",
            "Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core",
            "Assets/GameLib/Script/Project/Scene/Channels/Targeting/MB",
            "Assets/GameLib/Script/Project/Scene/Entity/Search",
        };

        static readonly string[] ScopeKindBranchingTargetRoots =
        {
            "Assets/GameLib/Script/Common/Commands/VNext",
        };

        static readonly string[] ExplicitTargetFiles =
        {
            "Assets/GameLib/Script/Kernel/Authoring/EntityIdentityMB.cs",
            "Assets/GameLib/Script/Kernel/Authoring/EntityAuthoringTraceMB.cs",
        };

        static readonly string[] ExplicitTargetGlobs =
        {
            "*DeclarationMB.cs",
        };

        static readonly string[] LegacyBlackboardRequireComponentFiles =
        {
            "Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs",
            "Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs",
            "Assets/GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs",
            "Assets/GameLib/Script/Platform/LTS/PlatformLifetimeScope.cs",
            "Assets/GameLib/Script/Project/UI/LTS/UILifetimeScope.cs",
            "Assets/GameLib/Script/Project/UI/Core/Elements/LTS/UIElementLifetimeScope.cs",
            "Assets/GameLib/Script/Project/Scene/Field/LTS/FieldLifetimeScope.cs",
            "Assets/GameLib/Script/Project/Scene/Field/Entity/LTS/EntityLifetimeScope.cs",
        };

        static readonly string[] LegacyBlackboardRequireComponentMarkers =
        {
            "RequireComponent(typeof(BlackboardMB))",
            "RequireComponent(typeof(Game.Common.BlackboardMB))",
        };

        static readonly string[] ExcludedPathMarkers =
        {
            "/Common/LTS/",
            "/Tests/Legacy/",
        };

        static readonly string[] ForbiddenLegacyMarkers =
        {
            "RuntimeLifetimeScopeBase",
            "BaseLifetimeScope",
            "RuntimeLifetimeScope",
            "IScopeNode",
            "IRuntimeResolver",
            "IBaseLifetimeScopeRegistry",
            "IFeatureInstaller",
            "ScopeNodeHierarchy",
            "ScopeFeatureInstallerUtility",
            "RuntimeResolverHub",
            "InstallOwnedFeatureInstallers",
            "TryGetNearestScopeNode",
            "GetChildrenOrEmpty",
            "FindNearestAncestorByKind",
            "FindNearestGameLogicRoot",
            "GetComponentInParent<RuntimeLifetimeScopeBase>",
            "GetComponent<BaseLifetimeScope>",
            "TryGetComponent<BaseLifetimeScope>",
            "TryGetComponent<RuntimeLifetimeScope>",
            "scope is BaseLifetimeScope",
            "scope is RuntimeLifetimeScope",
            "resolver.TryResolve<RuntimeLifetimeScope>",
            "resolver.TryResolve<BaseLifetimeScope>",
        };

        static readonly string[] NewPathMarkers =
        {
            "ApplicationKernel",
            "SceneKernel",
            "KernelRuntimeServiceGraph",
            "KernelRuntimeShell",
            "KernelBootBoundary",
            "EntityRef",
            "ServiceId",
            "EntityIdentityMB",
            "EntityDeclarationMB",
            "KernelProjectionGenerator",
            "EntityRegistrationPlan",
            "EntityServiceRoutePlan",
        };

        static readonly string[] LegacyCoexistenceMarkers =
        {
            "RuntimeLifetimeScopeBase",
            "BaseLifetimeScope",
            "RuntimeLifetimeScope",
            "IScopeNode",
            "IRuntimeResolver",
            "IBaseLifetimeScopeRegistry",
            "IFeatureInstaller",
            "ScopeNodeHierarchy",
            "ScopeFeatureInstallerUtility",
            "RuntimeResolverHub",
        };

        static readonly string[] HelperAuthorityMarkers =
        {
            "IBaseLifetimeScopeRegistry",
            "BaseLifetimeScopeRegistry",
            "ScopeNodeHierarchy",
            "ScopeFeatureInstallerUtility",
            "InstallOwnedFeatureInstallers",
            "TryGetNearestScopeNode",
            "GetChildrenOrEmpty",
            "FindNearestAncestorByKind",
            "FindNearestGameLogicRoot",
        };

        static readonly string[] DynamicDiscoveryMarkers =
        {
            "DynamicObjectRegistryService",
            "DynamicObjectRegistryMB",
            "DynamicObjectAutoRegistrar",
            "IDynamicObjectRegistryService",
            "IDynamicSearchService",
            "RegisterToDynamicRegistry",
            "VarKeyRegistryLocator",
            "CommandCatalogLocator",
            "CommandKeyRegistryLocator",
            "VariableKeyRegistryLocator",
            "CharacterKeyRegistryLocator",
            "RoomMapTileRegistryLocator",
            "Resources.Load",
            "Resources.LoadAsync",
        };

        static readonly string[] DynamicRegistryQuarantineMarkers =
        {
            "DynamicObjectRegistryService",
            "DynamicObjectRegistryMB",
            "DynamicObjectAutoRegistrar",
            "IDynamicObjectRegistryService",
            "IDynamicSearchService",
        };

        static readonly string[] ScopeKindBranchingMarkers =
        {
            "filter.kind != LifetimeScopeKind.None",
            "candidate.Kind != LifetimeScopeKind.Global",
            "kind = LifetimeScopeKind.Global",
            "new List<LifetimeScopeKind>",
            "FindNearestAncestorByKind(",
            "FindNearestGameLogicRoot(",
        };

        static readonly Dictionary<string, string> DeferredMixedFileInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectAutoRegistrar.cs"] = "M5.2 scope/resolver public dependency removal",
        };

        static readonly Dictionary<string, string> DeferredServiceFacingInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectAutoRegistrar.cs"] = "service-facing new-path mixed with legacy resolver authority",
        };

        static readonly Dictionary<string, string> DeferredHelperAuthorityInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/Commands/VNext/Core/ActorScopeResolver.cs"] = "M5.3 registry authority removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs"] = "M5.3 resolve-other-scope authority removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs"] = "M5.3 actor hierarchy and registry authority removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Core/CommandResolveContext.cs"] = "M5.3 resolve-other-scope authority removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/AnimationSpriteChannelExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/TextChannelResolveUtility.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/TransformAnimationChannelExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/CommandChannelExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/LifetimeScopeStateExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorDescendantRouterExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithPlayerExecutor.cs"] = "M5.3 subtree helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Time/TimeScaleExecutor.cs"] = "M5.3 nearest-ancestor helper removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/UI/UIControlExecutor.cs"] = "M5.3 registry authority removal",
        };

        static readonly Dictionary<string, string> DeferredDynamicDiscoveryInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs"] = "M5.4 runtime catalog locator fallback removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs"] = "M5.4 runtime command registry locator removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs"] = "M5.4 runtime catalog locator removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyRegistryLocator.cs"] = "M5.4 runtime command key registry fallback removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs"] = "M5.4 runtime command key registry fallback removal",
            ["Assets/GameLib/Script/Common/FirePattern/Core/SpawnContextToFireAdapter.cs"] = "M5.4 dynamic search resolver removal",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectAutoRegistrar.cs"] = "M5.4 dynamic registry auto-registration quarantine",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectRegistryService.cs"] = "M5.4 dynamic registry service quarantine",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicSearchTypes.cs"] = "M5.4 dynamic registry contract quarantine",
            ["Assets/GameLib/Script/Common/Variables/VariableKey/VariableKeyRegistryLocator.cs"] = "M5.4 runtime variable key registry fallback removal",
            ["Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs"] = "M5.4 runtime var-key registry dependency removal",
            ["Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs"] = "M5.4 runtime var-key registry fallback removal",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs"] = "M5.4 dynamic search dependency removal",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelScopeRuntime.cs"] = "M5.4 dynamic search dependency removal",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/MB/TargetChannelHubController.cs"] = "M5.4 dynamic search resolver removal",
            ["Assets/GameLib/Script/Project/Scene/Entity/Search/DynamicObjectRegistryMB.cs"] = "M5.4 dynamic registry feature installer quarantine",
            ["Assets/GameLib/Script/Project/Scene/RoomMap/Registry/RoomMapTileRegistryLocator.cs"] = "M5.4 room-map registry fallback removal",
            ["Assets/GameLib/Script/Project/UI/Core/Conversation/Character/Registry/CharacterKeyRegistryLocator.cs"] = "M5.4 character registry fallback removal",
        };

        static readonly Dictionary<string, string> DeferredDynamicRegistryQuarantineInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/FirePattern/Core/SpawnContextToFireAdapter.cs"] = "M5.5 fire-pattern dynamic search quarantine",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectAutoRegistrar.cs"] = "M5.5 dynamic registry auto-registration quarantine",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicObjectRegistryService.cs"] = "M5.5 dynamic registry service quarantine",
            ["Assets/GameLib/Script/Common/Search/Core/DynamicSearchTypes.cs"] = "M5.5 dynamic registry contract quarantine",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs"] = "M5.5 targeting dynamic search quarantine",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelScopeRuntime.cs"] = "M5.5 targeting dynamic search quarantine",
            ["Assets/GameLib/Script/Project/Scene/Channels/Targeting/MB/TargetChannelHubController.cs"] = "M5.5 targeting dynamic search quarantine",
            ["Assets/GameLib/Script/Project/Scene/Entity/Search/DynamicObjectRegistryMB.cs"] = "M5.5 dynamic registry feature installer quarantine",
        };

        static readonly Dictionary<string, string> DeferredScopeKindBranchingInventory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs"] = "M5.5 scope-kind and hierarchy branch removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorDescendantRouterExecutor.cs"] = "M5.5 identity filter kind branching removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorExecutor.cs"] = "M5.5 identity filter kind branching removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithPlayerExecutor.cs"] = "M5.5 identity filter kind branching removal",
            ["Assets/GameLib/Script/Common/Commands/VNext/Executors/Time/TimeScaleExecutor.cs"] = "M5.5 explicit time-service owner routing removal",
        };

        [Test]
        public void ExplicitTargetPath_DoesNotUseLegacyRuntimeLtsAuthority()
        {
            List<string> violations = new();

            foreach (string relativePath in EnumerateExplicitTargetFiles())
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> hitMarkers = CollectHits(sanitized, ForbiddenLegacyMarkers);
                if (hitMarkers.Count == 0)
                    continue;

                violations.Add($"{relativePath} -> {string.Join(", ", hitMarkers)}");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M5.1 source gate rejected legacy RuntimeLTS authority in target path.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ExplicitTargetPath_DoesNotMixNewPathAndLegacyAuthority()
        {
            List<string> violations = new();

            foreach (string relativePath in EnumerateExplicitTargetFiles())
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> newMarkers = CollectHits(sanitized, NewPathMarkers);
                if (newMarkers.Count == 0)
                    continue;

                List<string> legacyMarkers = CollectHits(sanitized, LegacyCoexistenceMarkers);
                if (legacyMarkers.Count == 0)
                    continue;

                violations.Add(
                    $"{relativePath} -> new[{string.Join(", ", newMarkers)}] legacy[{string.Join(", ", legacyMarkers)}]");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M5.1 source gate found new-path and legacy-authority coexistence in target path.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ExplicitTargetPath_DoesNotUseRegistryOrHierarchyHelperAuthority()
        {
            List<string> violations = new();

            foreach (string relativePath in EnumerateExplicitTargetFiles())
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> hitMarkers = CollectHits(sanitized, HelperAuthorityMarkers);
                if (hitMarkers.Count == 0)
                    continue;

                violations.Add($"{relativePath} -> {string.Join(", ", hitMarkers)}");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M5.3 source gate rejected registry / hierarchy helper authority in target path.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ExplicitTargetPath_DoesNotUseDynamicRegistryOrRuntimeDiscoveryAuthority()
        {
            List<string> violations = new();

            foreach (string relativePath in EnumerateExplicitTargetFiles())
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> hitMarkers = CollectHits(sanitized, DynamicDiscoveryMarkers);
                if (hitMarkers.Count == 0)
                    continue;

                violations.Add($"{relativePath} -> {string.Join(", ", hitMarkers)}");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M5.4 source gate rejected dynamic registry or runtime discovery authority in target path.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void BroadMixedFileInventory_DoesNotExpandBeyondDeferredSet()
        {
            Dictionary<string, string> actualMixedFiles = CollectBroadMixedFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualMixedFiles)
            {
                if (!DeferredMixedFileInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredMixedFileInventory)
            {
                if (!actualMixedFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.1 mixed-file inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredMixedFileInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        [Test]
        public void ServiceFacingTargetPath_DoesNotMixNewPathAndLegacyAuthority()
        {
            Dictionary<string, string> actualMixedFiles = CollectServiceFacingMixedFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualMixedFiles)
            {
                if (!DeferredServiceFacingInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredServiceFacingInventory)
            {
                if (!actualMixedFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.2 service-facing mixed inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredServiceFacingInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        [Test]
        public void ServiceFacingTargetPath_DoesNotUseRegistryOrHierarchyHelperAuthority()
        {
            Dictionary<string, string> actualHelperAuthorityFiles = CollectServiceFacingHelperAuthorityFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualHelperAuthorityFiles)
            {
                if (!DeferredHelperAuthorityInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredHelperAuthorityInventory)
            {
                if (!actualHelperAuthorityFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.3 helper-authority inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredHelperAuthorityInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        [Test]
        public void DynamicDiscoveryTargetPath_DoesNotExpandBeyondDeferredInventory()
        {
            Dictionary<string, string> actualDynamicDiscoveryFiles = CollectDynamicDiscoveryFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualDynamicDiscoveryFiles)
            {
                if (!DeferredDynamicDiscoveryInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredDynamicDiscoveryInventory)
            {
                if (!actualDynamicDiscoveryFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.4 dynamic-discovery inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredDynamicDiscoveryInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        [Test]
        public void DynamicRegistryQuarantineInventory_DoesNotExpandBeyondDeferredInventory()
        {
            Dictionary<string, string> actualQuarantineFiles = CollectDynamicRegistryQuarantineFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualQuarantineFiles)
            {
                if (!DeferredDynamicRegistryQuarantineInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredDynamicRegistryQuarantineInventory)
            {
                if (!actualQuarantineFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.5 dynamic-registry quarantine inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredDynamicRegistryQuarantineInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        [Test]
        public void DynamicRegistryAuthority_DoesNotLeakOutsideQuarantineRoots()
        {
            List<string> violations = new();

            foreach (string absolutePath in Directory.EnumerateFiles(Path.Combine(ProjectRootPath, "Assets/GameLib/Script"), "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = ToRelativePath(absolutePath);
                if (IsPathUnderRoots(relativePath, DynamicRegistryQuarantineRoots) || IsExplicitlyExcluded(relativePath))
                    continue;

                string sanitized = SanitizeSource(File.ReadAllText(absolutePath));
                List<string> hits = CollectHits(sanitized, DynamicRegistryQuarantineMarkers);
                if (hits.Count == 0)
                    continue;

                violations.Add($"{relativePath} -> {string.Join(", ", hits)}");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M5.5 dynamic-registry authority leaked outside quarantine roots.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void LifetimeScopeRoots_DoNotRequireBlackboardMb()
        {
            List<string> violations = new();

            for (int fileIndex = 0; fileIndex < LegacyBlackboardRequireComponentFiles.Length; fileIndex++)
            {
                string relativePath = NormalizePath(LegacyBlackboardRequireComponentFiles[fileIndex]);
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> hits = CollectHits(sanitized, LegacyBlackboardRequireComponentMarkers);
                if (hits.Count == 0)
                    continue;

                violations.Add($"{relativePath} -> {string.Join(", ", hits)}");
            }

            Assert.That(
                violations,
                Is.Empty,
                "M7.4 source gate rejected BlackboardMB as a required runtime component on lifetime-scope roots.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ScopeKindBranchingInventory_DoesNotExpandBeyondDeferredInventory()
        {
            Dictionary<string, string> actualBranchingFiles = CollectScopeKindBranchingFiles();
            List<string> unexpected = new();
            List<string> missing = new();

            foreach (KeyValuePair<string, string> pair in actualBranchingFiles)
            {
                if (!DeferredScopeKindBranchingInventory.ContainsKey(pair.Key))
                    unexpected.Add($"{pair.Key} -> {pair.Value}");
            }

            foreach (KeyValuePair<string, string> pair in DeferredScopeKindBranchingInventory)
            {
                if (!actualBranchingFiles.ContainsKey(pair.Key))
                    missing.Add($"{pair.Key} -> {pair.Value}");
            }

            string failureMessage =
                "M5.5 scope-kind branching inventory drifted.\n" +
                "Unexpected:\n" + FormatList(unexpected) + "\n" +
                "Missing expected deferred files:\n" + FormatList(missing) + "\n" +
                "Deferred inventory:\n" + FormatDictionary(DeferredScopeKindBranchingInventory);

            Assert.That(unexpected, Is.Empty, failureMessage);
            Assert.That(missing, Is.Empty, failureMessage);
        }

        static Dictionary<string, string> CollectBroadMixedFiles()
        {
            Dictionary<string, string> mixedFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string absolutePath in Directory.EnumerateFiles(Path.Combine(ProjectRootPath, "Assets/GameLib/Script"), "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = ToRelativePath(absolutePath);
                if (IsExplicitlyExcluded(relativePath))
                    continue;

                string sanitized = SanitizeSource(File.ReadAllText(absolutePath));
                List<string> newHits = CollectHits(sanitized, NewPathMarkers);
                if (newHits.Count == 0)
                    continue;

                List<string> legacyHits = CollectHits(sanitized, LegacyCoexistenceMarkers);
                if (legacyHits.Count == 0)
                    continue;

                mixedFiles.Add(
                    relativePath,
                    $"new[{string.Join(", ", newHits)}] legacy[{string.Join(", ", legacyHits)}]");
            }

            return mixedFiles;
        }

        static Dictionary<string, string> CollectServiceFacingMixedFiles()
        {
            Dictionary<string, string> mixedFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EnumerateFilesUnderRoots(ServiceFacingTargetRoots))
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> newHits = CollectHits(sanitized, NewPathMarkers);
                if (newHits.Count == 0)
                    continue;

                List<string> legacyHits = CollectHits(sanitized, LegacyCoexistenceMarkers);
                if (legacyHits.Count == 0)
                    continue;

                mixedFiles.Add(
                    relativePath,
                    $"new[{string.Join(", ", newHits)}] legacy[{string.Join(", ", legacyHits)}]");
            }

            return mixedFiles;
        }

        static Dictionary<string, string> CollectServiceFacingHelperAuthorityFiles()
        {
            Dictionary<string, string> helperAuthorityFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EnumerateFilesUnderRoots(ServiceFacingTargetRoots))
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> helperHits = CollectHits(sanitized, HelperAuthorityMarkers);
                if (helperHits.Count == 0)
                    continue;

                helperAuthorityFiles.Add(
                    relativePath,
                    $"helper[{string.Join(", ", helperHits)}]");
            }

            return helperAuthorityFiles;
        }

        static Dictionary<string, string> CollectDynamicDiscoveryFiles()
        {
            Dictionary<string, string> dynamicDiscoveryFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EnumerateFilesUnderRoots(DynamicDiscoveryTargetRoots))
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> dynamicHits = CollectHits(sanitized, DynamicDiscoveryMarkers);
                if (dynamicHits.Count == 0)
                    continue;

                dynamicDiscoveryFiles.Add(
                    relativePath,
                    $"dynamic[{string.Join(", ", dynamicHits)}]");
            }

            return dynamicDiscoveryFiles;
        }

        static Dictionary<string, string> CollectDynamicRegistryQuarantineFiles()
        {
            Dictionary<string, string> quarantineFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EnumerateFilesUnderRoots(DynamicRegistryQuarantineRoots))
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> dynamicHits = CollectHits(sanitized, DynamicRegistryQuarantineMarkers);
                if (dynamicHits.Count == 0)
                    continue;

                quarantineFiles.Add(
                    relativePath,
                    $"quarantine[{string.Join(", ", dynamicHits)}]");
            }

            return quarantineFiles;
        }

        static Dictionary<string, string> CollectScopeKindBranchingFiles()
        {
            Dictionary<string, string> branchingFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EnumerateFilesUnderRoots(ScopeKindBranchingTargetRoots))
            {
                string sanitized = LoadSanitizedSource(relativePath);
                List<string> branchHits = CollectHits(sanitized, ScopeKindBranchingMarkers);
                if (branchHits.Count == 0)
                    continue;

                branchingFiles.Add(
                    relativePath,
                    $"branch[{string.Join(", ", branchHits)}]");
            }

            return branchingFiles;
        }

        static IEnumerable<string> EnumerateExplicitTargetFiles()
        {
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

            for (int rootIndex = 0; rootIndex < ExplicitTargetRoots.Length; rootIndex++)
            {
                string rootPath = Path.Combine(ProjectRootPath, ExplicitTargetRoots[rootIndex].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(rootPath))
                    continue;

                foreach (string filePath in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
                {
                    string relativePath = ToRelativePath(filePath);
                    if (IsExplicitlyExcluded(relativePath))
                        continue;

                    paths.Add(relativePath);
                }
            }

            for (int fileIndex = 0; fileIndex < ExplicitTargetFiles.Length; fileIndex++)
            {
                string relativePath = NormalizePath(ExplicitTargetFiles[fileIndex]);
                if (File.Exists(Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                    paths.Add(relativePath);
            }

            string assetsRoot = Path.Combine(ProjectRootPath, "Assets");
            if (Directory.Exists(assetsRoot))
            {
                foreach (string filePath in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string relativePath = ToRelativePath(filePath);
                    if (IsExplicitlyExcluded(relativePath))
                        continue;

                    for (int globIndex = 0; globIndex < ExplicitTargetGlobs.Length; globIndex++)
                    {
                        if (!MatchesSimpleSuffixGlob(relativePath, ExplicitTargetGlobs[globIndex]))
                            continue;

                        paths.Add(relativePath);
                        break;
                    }
                }
            }

            List<string> orderedPaths = new(paths);
            orderedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return orderedPaths;
        }

        static IEnumerable<string> EnumerateFilesUnderRoots(IEnumerable<string> roots)
        {
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string absoluteRoot = Path.Combine(ProjectRootPath, root.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteRoot))
                    continue;

                foreach (string filePath in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                    paths.Add(ToRelativePath(filePath));
            }

            List<string> orderedPaths = new(paths);
            orderedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return orderedPaths;
        }

        static string LoadSanitizedSource(string relativePath)
        {
            string absolutePath = Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(absolutePath), Is.True, "Missing target-path source file: " + relativePath);
            return SanitizeSource(File.ReadAllText(absolutePath));
        }

        static List<string> CollectHits(string source, string[] markers)
        {
            List<string> hits = new();
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                string marker = markers[markerIndex];
                if (source.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    hits.Add(marker);
            }

            return hits;
        }

        static bool IsExplicitlyExcluded(string relativePath)
        {
            string normalizedPath = "/" + NormalizePath(relativePath) + "/";
            if (normalizedPath.IndexOf("/Common/LTS/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            int projectIndex = normalizedPath.IndexOf("/Project/", StringComparison.OrdinalIgnoreCase);
            if (projectIndex >= 0)
            {
                int ltsIndex = normalizedPath.IndexOf("/LTS/", projectIndex, StringComparison.OrdinalIgnoreCase);
                if (ltsIndex >= 0)
                    return true;
            }

            for (int markerIndex = 0; markerIndex < ExcludedPathMarkers.Length; markerIndex++)
            {
                if (normalizedPath.IndexOf(ExcludedPathMarkers[markerIndex], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static bool MatchesSimpleSuffixGlob(string relativePath, string glob)
        {
            if (string.IsNullOrEmpty(glob))
                return false;

            if (glob[0] == '*')
                return relativePath.EndsWith(glob.Substring(1), StringComparison.OrdinalIgnoreCase);

            return string.Equals(relativePath, glob, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsPathUnderRoots(string relativePath, IEnumerable<string> roots)
        {
            string normalized = NormalizePath(relativePath);
            foreach (string root in roots)
            {
                string normalizedRoot = NormalizePath(root).TrimEnd('/');
                if (normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string ToRelativePath(string absolutePath)
        {
            Uri projectRootUri = new(AppendDirectorySeparator(ProjectRootPath));
            Uri absoluteUri = new(absolutePath);
            return NormalizePath(Uri.UnescapeDataString(projectRootUri.MakeRelativeUri(absoluteUri).ToString()));
        }

        static string AppendDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();

            char last = path[path.Length - 1];
            return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        static string SanitizeSource(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            StringBuilder builder = new(source.Length);
            SourceSanitizerState state = SourceSanitizerState.Normal;

            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';

                switch (state)
                {
                    case SourceSanitizerState.Normal:
                        if (current == '/' && next == '/')
                        {
                            state = SourceSanitizerState.LineComment;
                            builder.Append(' ');
                            index++;
                            continue;
                        }

                        if (current == '/' && next == '*')
                        {
                            state = SourceSanitizerState.BlockComment;
                            builder.Append(' ');
                            index++;
                            continue;
                        }

                        if (current == '@' && next == '"')
                        {
                            state = SourceSanitizerState.VerbatimString;
                            builder.Append(' ');
                            index++;
                            continue;
                        }

                        if (current == '"')
                        {
                            state = SourceSanitizerState.String;
                            builder.Append(' ');
                            continue;
                        }

                        if (current == '\'')
                        {
                            state = SourceSanitizerState.Char;
                            builder.Append(' ');
                            continue;
                        }

                        builder.Append(current);
                        break;

                    case SourceSanitizerState.LineComment:
                        if (current == '\r' || current == '\n')
                        {
                            state = SourceSanitizerState.Normal;
                            builder.Append(current);
                        }
                        break;

                    case SourceSanitizerState.BlockComment:
                        if (current == '*' && next == '/')
                        {
                            state = SourceSanitizerState.Normal;
                            builder.Append(' ');
                            index++;
                        }
                        else if (current == '\r' || current == '\n')
                        {
                            builder.Append(current);
                        }
                        break;

                    case SourceSanitizerState.String:
                        if (current == '\\')
                        {
                            index++;
                            continue;
                        }

                        if (current == '"')
                            state = SourceSanitizerState.Normal;
                        break;

                    case SourceSanitizerState.VerbatimString:
                        if (current == '"' && next == '"')
                        {
                            index++;
                            continue;
                        }

                        if (current == '"')
                            state = SourceSanitizerState.Normal;
                        else if (current == '\r' || current == '\n')
                            builder.Append(current);
                        break;

                    case SourceSanitizerState.Char:
                        if (current == '\\')
                        {
                            index++;
                            continue;
                        }

                        if (current == '\'')
                            state = SourceSanitizerState.Normal;
                        break;
                }
            }

            return builder.ToString();
        }

        static string FormatList(List<string> entries)
        {
            if (entries.Count == 0)
                return "(none)";

            return string.Join("\n", entries);
        }

        static string FormatDictionary(Dictionary<string, string> entries)
        {
            if (entries.Count == 0)
                return "(none)";

            List<string> lines = new(entries.Count);
            foreach (KeyValuePair<string, string> pair in entries)
                lines.Add($"{pair.Key} -> {pair.Value}");

            lines.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("\n", lines);
        }

        static string ProjectRootPath
        {
            get
            {
                DirectoryInfo? assetsDirectory = Directory.GetParent(Application.dataPath);
                if (assetsDirectory == null)
                    throw new InvalidOperationException("Unable to resolve project root.");

                return assetsDirectory.FullName;
            }
        }

        enum SourceSanitizerState : byte
        {
            Normal = 0,
            LineComment = 1,
            BlockComment = 2,
            String = 3,
            VerbatimString = 4,
            Char = 5,
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Flow;
using Game.Loading;
using Game.Project;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class SceneFlowInstallerMBTests
    {
        [SetUp]
        public void SetUp()
        {
            VerifiedCompositionRuntime.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            VerifiedCompositionRuntime.Deactivate();
        }

        [Test]
        public void SceneFlowInstallerMB_IsAuthoringSurfaceAndAdapterBase()
        {
            Assert.That(typeof(SceneFlowInstallerMB).IsSubclassOf(typeof(SceneFlowAuthoring)), Is.True);
            Assert.That(typeof(SceneFlowInstallerMB).GetInterface(nameof(IScopeInstaller)), Is.Null);
            Assert.That(typeof(SceneFlowInstallerMB).GetInterface(nameof(ILoadingScreenConfig)), Is.Not.Null);
        }

        [Test]
        public void InstallSceneFlowRuntime_RegistersSceneFlowServicesForProjectScope_DuringVerifiedComposition()
        {
            VerifiedCompositionRuntime.Activate();

            GameObject gameObject = new GameObject("scene-flow-installer-test");
            try
            {
                SceneFlowInstallerMB installer = AddConfiguredInstaller(gameObject);
                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.RegisterInstance<IProjectBlackboardService>(new TestProjectBlackboardService());

                installer.InstallSceneFlowRuntime(builder, new TestScopeNode());

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<ISceneService>(out ISceneService sceneService), Is.True);
                    Assert.That(sceneService, Is.Not.Null);
                    Assert.That(resolver.TryResolve<ILoadingScreenService>(out ILoadingScreenService loadingService), Is.True);
                    Assert.That(loadingService, Is.Not.Null);
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InstallSceneFlowRuntime_SkipsNonProjectScope_DuringVerifiedComposition()
        {
            VerifiedCompositionRuntime.Activate();

            GameObject gameObject = new GameObject("scene-flow-installer-non-project-test");
            try
            {
                SceneFlowInstallerMB installer = AddConfiguredInstaller(gameObject);

                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.RegisterInstance<IProjectBlackboardService>(new TestProjectBlackboardService());

                installer.InstallSceneFlowRuntime(builder, new TestSceneScopeNode());

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<ISceneService>(out _), Is.False);
                    Assert.That(resolver.TryResolve<ILoadingScreenService>(out _), Is.False);
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        static SceneFlowInstallerMB AddConfiguredInstaller(GameObject gameObject)
        {
            LogAssert.Expect(LogType.Error, "Scene flow authoring requires a loading scene prefab when loading screen support is enabled.");

            SceneFlowInstallerMB installer = gameObject.AddComponent<SceneFlowInstallerMB>();
            FieldInfo? useLoadingScreenField = typeof(SceneFlowAuthoring).GetField("useLoadingScreen", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(useLoadingScreenField, Is.Not.Null, "SceneFlowAuthoring.useLoadingScreen was not found.");
            useLoadingScreenField!.SetValue(installer, false);
            return installer;
        }

        sealed class TestProjectBlackboardService : IProjectBlackboardService
        {
            readonly VarStore localVars = new();

            public IVarStore LocalVars => localVars;

            public IScopeNode ScopeNode => new TestScopeNode();

            public bool TryLocalGetVariant(int varId, out DynamicVariant value)
            {
                return localVars.TryGetVariant(varId, out value);
            }

            public bool TryLocalSetVariant(int varId, in DynamicVariant value)
            {
                return localVars.TrySetVariant(varId, in value);
            }

            public bool TryGlobalGetVariant(int varId, out DynamicVariant value)
            {
                return TryLocalGetVariant(varId, out value);
            }

            public bool TryGlobalSetVariant(int varId, in DynamicVariant value)
            {
                return TryLocalSetVariant(varId, in value);
            }

            public bool TryGlobalSetVariant(int varId, in DynamicVariant value, GlobalBlackboardSetFallback fallback)
            {
                _ = fallback;
                return TryLocalSetVariant(varId, in value);
            }

            public DynamicVariant GlobalGetVariant(int varId, DynamicVariant defaultValue)
            {
                return TryGlobalGetVariant(varId, out DynamicVariant value) ? value : defaultValue;
            }

            public IScopeNode FindGlobalVariantScope(int varId)
            {
                return localVars.Contains(varId) ? ScopeNode : null;
            }

            public void MergeInto(IVarStore dest, bool overwrite = false)
            {
                _ = overwrite;
                if (dest == null)
                    return;

                foreach (int key in localVars.EnumerateVarIds())
                {
                    if (localVars.TryGetVariant(key, out DynamicVariant value))
                        dest.TrySetVariant(key, value);
                }
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            public IScopeNode? Parent => null;

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind => LifetimeScopeKind.Project;

            public IRuntimeResolver? Resolver => null;

            public bool IsVisible => true;

            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }

        sealed class TestSceneScopeNode : IScopeNode
        {
            public IScopeNode? Parent => null;

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind => LifetimeScopeKind.Scene;

            public IRuntimeResolver? Resolver => null;

            public bool IsVisible => true;

            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }
}

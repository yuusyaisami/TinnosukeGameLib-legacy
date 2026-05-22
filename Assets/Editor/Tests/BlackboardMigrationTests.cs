#nullable enable

using System;
using System.Reflection;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Entity;
using Game.Field;
using NUnit.Framework;
using Game.Platform;
using Game.Scene;
using Game.UI;
using UnityEngine;

namespace Game.Editor.Tests
{
    public sealed class BlackboardMigrationTests
    {
        [Test]
        public void BlackboardMB_RemainsAdapterBase_ButIsNotFeatureInstallerAuthority()
        {
            Assert.That(typeof(BlackboardAuthoring).IsAssignableFrom(typeof(BlackboardMB)), Is.True);
            Assert.That(typeof(IScopeInstaller).IsAssignableFrom(typeof(BlackboardMB)), Is.False);
            Assert.That(typeof(IScopeAcquireHandler).IsAssignableFrom(typeof(BlackboardMB)), Is.False);
            Assert.That(typeof(IScopeReleaseHandler).IsAssignableFrom(typeof(BlackboardMB)), Is.False);
        }

        [Test]
        public void RepresentativeScopeTypes_DoNotRequireBlackboardMB()
        {
            Assert.That(HasBlackboardRequirement(typeof(ProjectLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(GlobalLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(PlatformLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(SceneLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(FieldLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(EntityLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(UILifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(UIElementLifetimeScope)), Is.False);
            Assert.That(HasBlackboardRequirement(typeof(KernelScopeHost)), Is.False);
        }

        [Test]
        public void RuntimeLifetimeScope_BuildsVerifiedBlackboardServicesWithoutBlackboardMB()
        {
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession());
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            GameObject scopeObject = new GameObject("VerifiedBlackboardRuntimeScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true);

                Assert.That(scope.GetComponent<BlackboardMB>(), Is.Null);
                Assert.That(scope.TryResolveLocal<IProjectBlackboardService>(out IProjectBlackboardService blackboard), Is.True);
                Assert.That(blackboard, Is.Not.Null);
                Assert.That(scope.TryResolveLocal<IGridBlackboardService>(out IGridBlackboardService gridBlackboard), Is.True);
                Assert.That(gridBlackboard, Is.Not.Null);
            }
            finally
            {
                VerifiedValueRuntimeBridge.Deactivate();
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void RuntimeLifetimeScope_BuildsVerifiedBlackboardServicesWithAuthoringOnlyComponent()
        {
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession());
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            GameObject scopeObject = new GameObject("AuthoringOnlyBlackboardRuntimeScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                BlackboardAuthoring authoring = scopeObject.AddComponent<TestBlackboardAuthoring>();
                _ = authoring;

                scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true);

                Assert.That(scope.GetComponent<BlackboardMB>(), Is.Null);
                Assert.That(scope.GetComponent<BlackboardAuthoring>(), Is.TypeOf<TestBlackboardAuthoring>());
                Assert.That(scope.TryResolveLocal<IProjectBlackboardService>(out IProjectBlackboardService blackboard), Is.True);
                Assert.That(blackboard, Is.Not.Null);
                Assert.That(scope.TryResolveLocal<IGridBlackboardService>(out IGridBlackboardService gridBlackboard), Is.True);
                Assert.That(gridBlackboard, Is.Not.Null);
            }
            finally
            {
                VerifiedValueRuntimeBridge.Deactivate();
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void RuntimeLifetimeScope_ThrowsWhenVerifiedRuntimeWouldFallbackToLegacyLocalInit()
        {
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession());
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            GameObject scopeObject = new GameObject("BlackboardLocalFallbackScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                BlackboardMB blackboardMB = scopeObject.AddComponent<BlackboardMB>();
                SetPrivateField(blackboardMB, "_valueInitPlansBuilt", true);
                SetPrivateField(blackboardMB, "_createLocalBlackboardPlan", new BlackboardLocalValueInitPlan(
                    BlackboardValueInitPhase.Create,
                    overwriteExisting: false,
                    new[]
                    {
                        new BlackboardLocalValueInitEntryPlan(1001, DynamicValueExtensions.FromLiteral(7), 0),
                    }));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                    scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true));

                Assert.That(exception.Message, Does.Contain("Legacy local blackboard fallback is forbidden"));
            }
            finally
            {
                VerifiedValueRuntimeBridge.Deactivate();
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void RuntimeLifetimeScope_ThrowsWhenVerifiedRuntimeWouldFallbackToLegacyGridInit()
        {
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession());
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            GameObject scopeObject = new GameObject("BlackboardGridFallbackScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                BlackboardMB blackboardMB = scopeObject.AddComponent<BlackboardMB>();
                SetPrivateField(blackboardMB, "_valueInitPlansBuilt", true);
                SetPrivateField(blackboardMB, "_createLocalGridBlackboardPlan", new BlackboardGridValueInitPlan(
                    BlackboardValueInitPhase.Create,
                    overwriteExisting: false,
                    gridIdVarId: 0,
                    new[]
                    {
                        new BlackboardGridValueInitCellPlan(0, 0, 0),
                    }));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                    scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true));

                Assert.That(exception.Message, Does.Contain("Legacy grid fallback is forbidden"));
            }
            finally
            {
                VerifiedValueRuntimeBridge.Deactivate();
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        static bool HasBlackboardRequirement(Type scopeType)
        {
            object[] attributes = scopeType.GetCustomAttributes(typeof(RequireComponent), inherit: true);
            for (int index = 0; index < attributes.Length; index++)
            {
                RequireComponent attribute = (RequireComponent)attributes[index];
                if (attribute.m_Type0 == typeof(BlackboardMB)
                    || attribute.m_Type1 == typeof(BlackboardMB)
                    || attribute.m_Type2 == typeof(BlackboardMB))
                {
                    return true;
                }
            }

            return false;
        }

        static RuntimeIdentityData CreateIdentity(LifetimeScopeKind kind)
        {
            return new RuntimeIdentityData
            {
                Kind = kind,
                InitiallyActive = false,
            };
        }

        static void SetPrivateField(object target, string fieldName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + fieldName + " must remain available for deterministic tests.");
            field!.SetValue(target, value);
        }

        sealed class TestVerifiedValueSession : IVerifiedValueRuntimeSession
        {
            public bool TryResolveValueKey(string stableKey, out int valueKeyId)
            {
                valueKeyId = 0;
                return false;
            }

            public bool TryGetStableKey(int valueKeyId, out string stableKey)
            {
                stableKey = string.Empty;
                return false;
            }

            public VerifiedValueInitApplyResult ApplyLocalBlackboardInit(IScopeNode scope, IBlackboardService blackboard, VerifiedValueInitPhase phase, DynamicEvaluationRuntime runtime)
            {
                _ = scope;
                _ = blackboard;
                _ = phase;
                _ = runtime;
                return VerifiedValueInitApplyResult.NotAvailable();
            }
        }

        sealed class TestVerifiedCommandSession : IVerifiedCommandRuntimeSession
        {
            readonly TestCommandExecutorCatalog _catalog = new TestCommandExecutorCatalog();

            public ICommandCatalog Catalog => NullCommandCatalog.Instance;

            public ICommandKeyResolver KeyResolver => NullCommandKeyResolver.Instance;

            public ICommandPayloadReferenceValidator PayloadReferenceValidator => MissingCommandPayloadReferenceValidator.Instance;

            public ICommandExecutorCatalog CreateExecutorCatalog(IRuntimeResolver resolver, System.Collections.Generic.IReadOnlyList<ExplicitCommandExecutorBinding> bindings)
            {
                _ = resolver;
                _ = bindings;
                return _catalog;
            }
        }

        sealed class TestCommandExecutorCatalog : ICommandExecutorCatalog
        {
            public bool TryGet(int commandId, out ICommandExecutor executor)
            {
                _ = commandId;
                executor = null!;
                return false;
            }
        }

        sealed class TestBlackboardAuthoring : BlackboardAuthoring
        {
        }
    }
}



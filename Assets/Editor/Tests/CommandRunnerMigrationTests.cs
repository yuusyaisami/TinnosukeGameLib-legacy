#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;

using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.DI;
using Game.Entity;
using Game.Field;
using Game.Platform;
using Game.Scene;
using Game.UI;

namespace TinnosukeGameLib.Tests.Editor
{
    public sealed class CommandRunnerMigrationTests
    {
        [Test]
        public void CommandRunnerMB_RemainsAuthoringAdapter_ButIsNotFeatureInstallerAuthority()
        {
            Assert.That(typeof(CommandRunnerAuthoring).IsAssignableFrom(typeof(CommandRunnerMB)), Is.True);
            Assert.That(typeof(IScopeInstaller).IsAssignableFrom(typeof(CommandRunnerMB)), Is.False);
            Assert.That(typeof(IScopeAcquireHandler).IsAssignableFrom(typeof(CommandRunnerMB)), Is.False);
            Assert.That(typeof(IScopeReleaseHandler).IsAssignableFrom(typeof(CommandRunnerMB)), Is.False);
        }

        [Test]
        public void RepresentativeScopeTypes_DoNotRequireCommandRunnerMB()
        {
            Assert.That(HasCommandRunnerRequirement(typeof(ProjectLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(GlobalLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(PlatformLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(SceneLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(FieldLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(EntityLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(UILifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(UIElementLifetimeScope)), Is.False);
            Assert.That(HasCommandRunnerRequirement(typeof(KernelScopeHost)), Is.False);
        }

        [Test]
        public void RuntimeLifetimeScope_BuildsVerifiedCommandRuntimeWithoutCommandRunnerMB()
        {
            TestCommandExecutorCatalog executorCatalog = new TestCommandExecutorCatalog();
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession(executorCatalog));

            GameObject scopeObject = new GameObject("VerifiedCommandRuntimeScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true);

                Assert.That(scope.GetComponent<CommandRunnerMB>(), Is.Null);
                Assert.That(scope.TryResolveLocal<IProjectCommandRunner>(out IProjectCommandRunner runner), Is.True);
                Assert.That(runner, Is.Not.Null);
                Assert.That(scope.TryResolveLocal<ICommandExecutorCatalog>(out ICommandExecutorCatalog resolvedCatalog), Is.True);
                Assert.That(resolvedCatalog, Is.SameAs(executorCatalog));
            }
            finally
            {
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void RuntimeLifetimeScope_BuildsVerifiedCommandRuntimeWithAuthoringOnlyComponent()
        {
            TestCommandExecutorCatalog executorCatalog = new TestCommandExecutorCatalog();
            VerifiedCommandRuntimeBridge.Activate(new TestVerifiedCommandSession(executorCatalog));

            GameObject scopeObject = new GameObject("AuthoringOnlyCommandRuntimeScope");
            try
            {
                KernelScopeHost scope = scopeObject.AddComponent<KernelScopeHost>();
                CommandRunnerAuthoring authoring = scopeObject.AddComponent<TestCommandRunnerAuthoring>();
                _ = authoring;

                scope.ConfigureForAcquire(null, CreateIdentity(LifetimeScopeKind.Project), ensureBuilt: true);

                Assert.That(scope.GetComponent<CommandRunnerMB>(), Is.Null);
                Assert.That(scope.GetComponent<CommandRunnerAuthoring>(), Is.TypeOf<TestCommandRunnerAuthoring>());
                Assert.That(scope.TryResolveLocal<IProjectCommandRunner>(out IProjectCommandRunner runner), Is.True);
                Assert.That(runner, Is.Not.Null);
                Assert.That(scope.TryResolveLocal<ICommandExecutorCatalog>(out ICommandExecutorCatalog resolvedCatalog), Is.True);
                Assert.That(resolvedCatalog, Is.SameAs(executorCatalog));
            }
            finally
            {
                VerifiedCommandRuntimeBridge.Deactivate();
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        static bool HasCommandRunnerRequirement(Type scopeType)
        {
            object[] attributes = scopeType.GetCustomAttributes(typeof(RequireComponent), inherit: true);
            for (int index = 0; index < attributes.Length; index++)
            {
                RequireComponent attribute = (RequireComponent)attributes[index];
                if (attribute.m_Type0 == typeof(CommandRunnerMB)
                    || attribute.m_Type1 == typeof(CommandRunnerMB)
                    || attribute.m_Type2 == typeof(CommandRunnerMB))
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

        sealed class TestVerifiedCommandSession : IVerifiedCommandRuntimeSession
        {
            readonly ICommandExecutorCatalog _executorCatalog;

            public TestVerifiedCommandSession(ICommandExecutorCatalog executorCatalog)
            {
                _executorCatalog = executorCatalog;
            }

            public ICommandCatalog Catalog => NullCommandCatalog.Instance;

            public ICommandKeyResolver KeyResolver => NullCommandKeyResolver.Instance;

            public ICommandPayloadReferenceValidator PayloadReferenceValidator => MissingCommandPayloadReferenceValidator.Instance;

            public ICommandExecutorCatalog CreateExecutorCatalog(IRuntimeResolver resolver, System.Collections.Generic.IReadOnlyList<ExplicitCommandExecutorBinding> bindings)
            {
                _ = resolver;
                _ = bindings;
                return _executorCatalog;
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

        sealed class TestCommandRunnerAuthoring : CommandRunnerAuthoring
        {
        }
    }
}



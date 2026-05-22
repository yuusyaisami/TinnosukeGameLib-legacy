#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class BlackboardPayloadProjectionTests
    {
        static readonly MethodInfo ProjectCommandVarsMethod = typeof(BlackboardService).Assembly
            .GetType("Game.Common.BlackboardPayloadProjectionUtility", throwOnError: true)!
            .GetMethod("ProjectCommandVars", BindingFlags.Public | BindingFlags.Static)!;

        [Test]
        public void BuildCommandVars_DoesNotWritePayloadToLocalBlackboardOrLeakExistingLocalVars()
        {
            const int existingVarId = 700101;
            const int payloadVarId = 700102;

            TestScopeNode scope = new TestScopeNode();
            BlackboardService blackboard = new BlackboardService(scope);
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            resolver.Register<IBlackboardService>(blackboard);
            scope.Resolver = resolver;

            blackboard.LocalVars.TrySetVariant(existingVarId, DynamicVariant.FromInt(11));

            VarStore payload = new VarStore(initialCapacity: 4);
            payload.TrySetVariant(payloadVarId, DynamicVariant.FromInt(22));

            IVarStore commandVars = InvokeProjectCommandVars(payload);

            Assert.That(commandVars.TryGetVariant(existingVarId, out _), Is.False);
            Assert.That(commandVars.TryGetVariant(payloadVarId, out DynamicVariant commandValue), Is.True);
            Assert.That(commandValue.AsInt, Is.EqualTo(22));

            Assert.That(blackboard.LocalVars.TryGetVariant(existingVarId, out DynamicVariant existingValue), Is.True);
            Assert.That(existingValue.AsInt, Is.EqualTo(11));
            Assert.That(blackboard.LocalVars.TryGetVariant(payloadVarId, out _), Is.False);
        }

        [Test]
        public void BuildCommandVars_UsesPayloadDirectlyWhenNoBlackboardExists()
        {
            const int payloadVarId = 700201;

            VarStore payload = new VarStore(initialCapacity: 4);
            payload.TrySetVariant(payloadVarId, DynamicVariant.FromInt(33));

            IVarStore commandVars = InvokeProjectCommandVars(payload);

            Assert.That(commandVars.TryGetVariant(payloadVarId, out DynamicVariant commandValue), Is.True);
            Assert.That(commandValue.AsInt, Is.EqualTo(33));
        }

        [Test]
        public void ProjectCommandVars_ReturnsIndependentCopy()
        {
            const int payloadVarId = 700301;

            VarStore payload = new VarStore(initialCapacity: 4);
            payload.TrySetVariant(payloadVarId, DynamicVariant.FromInt(44));

            IVarStore commandVars = InvokeProjectCommandVars(payload);
            commandVars.TrySetVariant(payloadVarId, DynamicVariant.FromInt(55));

            Assert.That(commandVars.TryGetVariant(payloadVarId, out DynamicVariant projectedValue), Is.True);
            Assert.That(projectedValue.AsInt, Is.EqualTo(55));
            Assert.That(payload.TryGetVariant(payloadVarId, out DynamicVariant payloadValue), Is.True);
            Assert.That(payloadValue.AsInt, Is.EqualTo(44));
        }

        static IVarStore InvokeProjectCommandVars(VarStore payload)
        {
            try
            {
                return (IVarStore)ProjectCommandVarsMethod.Invoke(null, new object[] { payload })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object> services = new();

            public void Register<T>(T instance)
            {
                services[typeof(T)] = instance!;
            }

            public void Dispose()
            {
            }

            public void Inject(object instance)
            {
                _ = instance;
            }

            public object Resolve(Type type)
            {
                if (services.TryGetValue(type, out object instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {type.FullName}.");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out T instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {typeof(T).FullName}.");
            }

            public object? ResolveOrDefault(Type type)
            {
                return services.TryGetValue(type, out object instance)
                    ? instance
                    : null;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                bool found = services.TryGetValue(type, out object resolved);
                instance = resolved;
                return found;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (services.TryGetValue(typeof(T), out object resolved) && resolved is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            public IScopeNode? Parent => null;

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind => LifetimeScopeKind.Scene;

            public IRuntimeResolver? Resolver { get; set; }

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
                return new[] { this };
            }
        }
    }
}

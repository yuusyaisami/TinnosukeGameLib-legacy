#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Editor.Tests
{
    public sealed class BlackboardServiceAuthorityTests
    {
        [SetUp]
        public void SetUp()
        {
            VerifiedValueRuntimeBridge.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            VerifiedValueRuntimeBridge.Deactivate();
        }

        [Test]
        public void TryGlobalGetVariant_TraversesParentWhileVerifiedValueRuntimeIsInactive()
        {
            TestScopeNode parentScope = new TestScopeNode(LifetimeScopeKind.Runtime);
            BlackboardService parentBlackboard = new BlackboardService(parentScope);
            parentScope.Resolver = new TestResolver(parentBlackboard);

            TestScopeNode childScope = new TestScopeNode(LifetimeScopeKind.Runtime)
            {
                Parent = parentScope,
            };

            BlackboardService childBlackboard = new BlackboardService(childScope);
            childScope.Resolver = new TestResolver(childBlackboard);

            Assert.That(parentBlackboard.TryLocalSetVariant(901, DynamicVariant.FromInt(11)), Is.True);
            Assert.That(childBlackboard.TryGlobalGetVariant(901, out DynamicVariant resolved), Is.True);
            Assert.That(resolved.AsInt, Is.EqualTo(11));
        }

        [Test]
        public void TryGlobalGetVariant_FailsClosedWhenVerifiedValueRuntimeIsActive()
        {
            TestScopeNode parentScope = new TestScopeNode(LifetimeScopeKind.Runtime);
            BlackboardService parentBlackboard = new BlackboardService(parentScope);
            parentScope.Resolver = new TestResolver(parentBlackboard);

            TestScopeNode childScope = new TestScopeNode(LifetimeScopeKind.Runtime)
            {
                Parent = parentScope,
            };

            BlackboardService childBlackboard = new BlackboardService(childScope);
            childScope.Resolver = new TestResolver(childBlackboard);

            Assert.That(parentBlackboard.TryLocalSetVariant(901, DynamicVariant.FromInt(11)), Is.True);

            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            LogAssert.Expect(LogType.Error, new Regex("Wave D verified value authority blocked BlackboardService\\.TryGlobalGetVariant"));
            LogAssert.Expect(LogType.Error, new Regex("Wave D verified value authority blocked BlackboardService\\.FindGlobalVariantScope"));
            LogAssert.Expect(LogType.Error, new Regex("Wave D verified value authority blocked BlackboardService\\.TryGlobalSetVariant"));

            Assert.That(childBlackboard.TryGlobalGetVariant(901, out _), Is.False);
            Assert.That(childBlackboard.TryGlobalGetVariant(901, out _), Is.False);
            Assert.That(childBlackboard.FindGlobalVariantScope(901), Is.Null);
            Assert.That(childBlackboard.FindGlobalVariantScope(901), Is.Null);
            Assert.That(childBlackboard.TryGlobalSetVariant(901, DynamicVariant.FromInt(13)), Is.False);
            Assert.That(childBlackboard.TryGlobalSetVariant(901, DynamicVariant.FromInt(13)), Is.False);
            Assert.That(parentBlackboard.TryLocalGetVariant(901, out DynamicVariant retained), Is.True);
            Assert.That(retained.AsInt, Is.EqualTo(11));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DynamicValueResolver_GlobalBlackboardRead_FailsClosedWhenVerifiedValueRuntimeIsActive()
        {
            TestScopeNode parentScope = new TestScopeNode(LifetimeScopeKind.Runtime);
            BlackboardService parentBlackboard = new BlackboardService(parentScope);
            parentScope.Resolver = new TestResolver(parentBlackboard);

            TestScopeNode childScope = new TestScopeNode(LifetimeScopeKind.Runtime)
            {
                Parent = parentScope,
            };

            BlackboardService childBlackboard = new BlackboardService(childScope);
            childScope.Resolver = new TestResolver(childBlackboard);

            Assert.That(parentBlackboard.TryLocalSetVariant(901, DynamicVariant.FromInt(17)), Is.True);

            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession(("value.test", 901)));

            LogAssert.Expect(LogType.Error, new Regex("Wave D verified value authority blocked DynamicValueResolver global blackboard read"));

            Assert.That(
                DynamicValueResolver.TryGetFromSelfBlackboard<int>(childScope, "value.test", out int resolved, BlackboardReadScope.Global),
                Is.False);
            Assert.That(
                DynamicValueResolver.TryGetFromSelfBlackboard<int>(childScope, "value.test", out resolved, BlackboardReadScope.Global),
                Is.False);
            Assert.That(resolved, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DynamicSources_BlackboardFallbackLogsOnlyOnceWhenVerifiedValueRuntimeIsActive()
        {
            TestScopeNode scope = new TestScopeNode(LifetimeScopeKind.Runtime);
            BlackboardService blackboard = new BlackboardService(scope);
            scope.Resolver = new TestResolver(blackboard);

            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            LogAssert.Expect(LogType.Error, new Regex("Wave D verified value authority blocked DynamicSources blackboard fallback creation"));

            InvokeBlackboardFallback(scope, blackboard, 901, BlackboardReadFallback.CreateLocal, DynamicVariant.FromInt(5));
            InvokeBlackboardFallback(scope, blackboard, 901, BlackboardReadFallback.CreateLocal, DynamicVariant.FromInt(5));

            LogAssert.NoUnexpectedReceived();
        }

        static void InvokeBlackboardFallback(IScopeNode scope, IBlackboardService blackboard, int varId, BlackboardReadFallback fallback, DynamicVariant initialValue)
        {
            Type? utilityType = typeof(SelfBlackboardSource).Assembly.GetType("Game.Common.BlackboardSourceUtility");
            Assert.That(utilityType, Is.Not.Null);

            MethodInfo? method = utilityType!.GetMethod("ApplyFallback", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method!.Invoke(null, new object?[] { scope, blackboard, varId, fallback, initialValue });
        }

        sealed class TestVerifiedValueSession : IVerifiedValueRuntimeSession
        {
            readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);

            public TestVerifiedValueSession(params (string StableKey, int ValueKeyId)[] entries)
            {
                for (int index = 0; index < entries.Length; index++)
                    values[entries[index].StableKey] = entries[index].ValueKeyId;
            }

            public bool TryResolveValueKey(string stableKey, out int valueKeyId)
            {
                return values.TryGetValue(stableKey, out valueKeyId);
            }

            public bool TryGetStableKey(int valueKeyId, out string stableKey)
            {
                _ = valueKeyId;
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

        sealed class TestResolver : IRuntimeResolver
        {
            readonly object instance;

            public TestResolver(object instance)
            {
                this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            }

            public bool TryResolve(Type type, out object? resolved)
            {
                if (type.IsInstanceOfType(instance))
                {
                    resolved = instance;
                    return true;
                }

                resolved = null;
                return false;
            }

            public bool TryResolve<T>(out T resolved)
            {
                if (instance is T typed)
                {
                    resolved = typed;
                    return true;
                }

                resolved = default!;
                return false;
            }

            public object Resolve(Type type)
            {
                if (TryResolve(type, out object? resolved) && resolved != null)
                    return resolved;

                throw new InvalidOperationException("Requested type is not registered in TestResolver.");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out T resolved))
                    return resolved;

                throw new InvalidOperationException("Requested type is not registered in TestResolver.");
            }

            public object? ResolveOrDefault(Type type)
            {
                TryResolve(type, out object? resolved);
                return resolved;
            }

            public void Inject(object instance)
            {
                _ = instance;
            }

            public void Dispose()
            {
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly LifetimeScopeKind kind;

            public TestScopeNode(LifetimeScopeKind kind)
            {
                this.kind = kind;
            }

            public IScopeNode? Parent { get; set; }

            public IScopeIdentityService? Identity { get; set; }

            public LifetimeScopeKind Kind => kind;

            public IRuntimeResolver? Resolver { get; set; }

            public bool IsVisible { get; private set; } = true;

            public bool IsActive { get; private set; } = true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                IsVisible = visible;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                IsActive = active;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = isReset;
                _ = ct;
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                List<IScopeNode> nodes = new List<IScopeNode>();
                IScopeNode? current = this;
                while (current != null)
                {
                    nodes.Add(current);
                    current = current.Parent;
                }

                nodes.Reverse();
                return nodes;
            }
        }
    }
}

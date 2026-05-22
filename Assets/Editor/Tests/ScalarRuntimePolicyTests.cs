#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game;
using Game.Scalar;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ScalarRuntimePolicyTests
    {
        [Test]
        public void GlobalTryGet_StopsAtDirectParent()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");

            var grandparentScope = new TestScopeNode(LifetimeScopeKind.Project);
            var parentScope = new TestScopeNode(LifetimeScopeKind.Global) { Parent = grandparentScope };
            var childScope = new TestScopeNode(LifetimeScopeKind.Scene) { Parent = parentScope };

            var grandparentService = new BaseScalarService(grandparentScope, null);
            var parentService = new BaseScalarService(parentScope, null);
            var childService = new BaseScalarService(childScope, null);

            grandparentScope.Resolver = new TestRuntimeResolver().Register<IBaseScalarService>(grandparentService);
            parentScope.Resolver = new TestRuntimeResolver().Register<IBaseScalarService>(parentService);
            childScope.Resolver = new TestRuntimeResolver().Register<IBaseScalarService>(childService);

            grandparentService.SetLocalBase(key, 42f);

            Assert.That(parentService.GlobalTryGet(key, out var parentValue), Is.True);
            Assert.That(parentValue, Is.EqualTo(42f));

            Assert.That(childService.GlobalTryGet(key, out var childValue), Is.False);
            Assert.That(childValue, Is.EqualTo(0f));
        }

        [Test]
        public void BindScalarRef_FailsClosedWithoutRegistrySearch()
        {
            var manager = new ScalarBindingManager();
            var source = new ScalarRef(LifetimeScopeKind.Global, new ScalarKey("GameLib.Movement.DefaultSpeed"));
            var target = new ScalarRef(LifetimeScopeKind.Scene, new ScalarKey("GameLib.Movement.SpeedMultiplier"));

            Assert.Throws<NotSupportedException>(() => manager.Bind(source, target, ScalarLinkMode.ValueToMul, 1f));
        }

        [Test]
        public void ScalarRuntime_AppliesLayeredPipelineAndClamp()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var rt = new ScalarKeyRuntime(key, null, null);

            rt.SetBaseline(10f);
            rt.Mul(null, "combat", 2f, ScalarMulPhase.PreAdd, -1f, null, "pre");
            rt.Add(null, "combat", 5f, -1f, null, "add");
            rt.Mul(null, "combat", 3f, ScalarMulPhase.PostAdd, -1f, null, "post");

            var clamp = new ScalarClamp
            {
                UseMin = true,
                Min = DynamicValueExtensions.FromLiteral(0f),
                UseMax = true,
                Max = DynamicValueExtensions.FromLiteral(20f),
            };

            rt.SetFinalClamp(clamp);

            Assert.That(rt.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(20f));
            Assert.That(rt.Revision, Is.GreaterThan(0));

            var lanes = new List<LayeredNumericLaneKind>();
            foreach (var snapshot in rt.EnumerateSnapshots())
            {
                lanes.Add(snapshot.Lane);
            }

            Assert.That(lanes, Has.Member(LayeredNumericLaneKind.FinalClamp));
        }

        [Test]
        public void ScalarRuntime_RejectsDynamicClampInputs()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var rt = new ScalarKeyRuntime(key, null, null);

            rt.SetBaseline(10f);
            rt.Add(null, "combat", 15f, -1f, null, "add");

            var clamp = new ScalarClamp
            {
                UseMax = true,
                Max = DynamicValue<float>.FromSource(VarStoreSource.FromVarId(123)),
            };

            rt.SetFinalClamp(clamp);

            Assert.That(rt.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(25f));
            Assert.That(rt.Revision, Is.GreaterThan(0));

            var lanes = new List<LayeredNumericLaneKind>();
            foreach (var snapshot in rt.EnumerateSnapshots())
            {
                lanes.Add(snapshot.Lane);
            }

            Assert.That(lanes, Has.No.Member(LayeredNumericLaneKind.FinalClamp));
        }

        [Test]
        public void ScalarBinding_DeltaRebaseRestoresExplicitEndpointBaseline()
        {
            var sourceScope = new TestScopeNode(LifetimeScopeKind.Global);
            var targetScope = new TestScopeNode(LifetimeScopeKind.Scene);
            var sourceService = new BaseScalarService(sourceScope, null);
            var targetService = new BaseScalarService(targetScope, null);
            var manager = new ScalarBindingManager();

            var sourceKey = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var targetKey = new ScalarKey("GameLib.Movement.SpeedMultiplier");

            sourceService.SetLocalBase(sourceKey, 10f);
            targetService.SetLocalBase(targetKey, 1f);

            var handle = manager.Bind(sourceService, sourceKey, targetService, targetKey, ScalarLinkMode.DeltaToAdd, 1f);

            Assert.That(handle, Is.Not.Null);
            Assert.That(targetService.LocalGet(targetKey), Is.EqualTo(1f));

            sourceService.SetLocalBase(sourceKey, 15f);
            manager.Tick();

            Assert.That(targetService.LocalGet(targetKey), Is.EqualTo(6f));

            handle.Rebase();

            Assert.That(targetService.LocalGet(targetKey), Is.EqualTo(1f));
        }

        [Test]
        public void ScalarService_AcquireResetRestoresConfiguredBaseline()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var configProvider = new TestScalarRuntimeConfigProvider(key, 4f);
            var scope = new TestScopeNode(LifetimeScopeKind.Scene);
            var service = new BaseScalarService(scope, configProvider);

            service.SetLocalBase(key, 99f);
            Assert.That(service.LocalGet(key), Is.EqualTo(99f));

            service.OnRelease(scope, true);
            service.OnAcquire(scope, true);

            Assert.That(service.LocalGet(key), Is.EqualTo(4f));
        }

        [Test]
        public void ScalarService_ClearAllInvalidatesLiveHandles()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var scope = new TestScopeNode(LifetimeScopeKind.Scene);
            var service = new BaseScalarService(scope, null);
            var runtime = service.EnsureAndGetRuntime(key, new ScalarRuntimeConfig { BaseValue = 0f });

            var handle = service.LocalAdd(key, null, 2f);

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.IsValid, Is.True);
            Assert.That(runtime.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(2f));

            service.ClearAll();

            handle.SetValue(5f);

            Assert.That(handle.IsValid, Is.False);
            Assert.That(runtime.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(2f));
        }

        [Test]
        public void ScalarService_EnsureRuntimeReplacementInvalidatesPreviousHandles()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var scope = new TestScopeNode(LifetimeScopeKind.Scene);
            var service = new BaseScalarService(scope, null);
            var firstRuntime = service.EnsureAndGetRuntime(key, new ScalarRuntimeConfig { BaseValue = 1f });

            var handle = service.LocalAdd(key, null, 2f);

            Assert.That(firstRuntime.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(3f));

            service.EnsureRuntime(key, new ScalarRuntimeConfig { BaseValue = 4f });

            handle.SetValue(5f);

            Assert.That(handle.IsValid, Is.False);
            Assert.That(firstRuntime.Get(null, includeAllLayers: true, layer: null), Is.EqualTo(3f));
            Assert.That(service.LocalGet(key), Is.EqualTo(4f));
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object?> _services = new Dictionary<Type, object?>();

            public TestRuntimeResolver Register<T>(T instance) where T : class
            {
                _services[typeof(T)] = instance;
                return this;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                return _services.TryGetValue(type, out instance) && instance != null;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out var value) && value is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default;
                return false;
            }

            public object Resolve(Type type)
            {
                if (TryResolve(type, out var instance) && instance != null)
                    return instance;

                throw new InvalidOperationException($"Missing service: {type.FullName}");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out var instance))
                    return instance;

                throw new InvalidOperationException($"Missing service: {typeof(T).FullName}");
            }

            public object? ResolveOrDefault(Type type)
            {
                TryResolve(type, out var instance);
                return instance;
            }

            public void Inject(object instance)
            {
            }

            public void Dispose()
            {
            }
        }

        sealed class TestScalarRuntimeConfigProvider : IScalarRuntimeConfigProvider
        {
            readonly Dictionary<int, ScalarRuntimeConfig> _configs = new Dictionary<int, ScalarRuntimeConfig>();

            public TestScalarRuntimeConfigProvider(ScalarKey key, float baseValue)
            {
                _configs[key.Id] = new ScalarRuntimeConfig
                {
                    BaseValue = baseValue,
                    UseEffectMod = false,
                    UseRoundMod = false,
                    RoundDigits = 0,
                    UseClampMod = false,
                };
            }

            public bool TryGetBase(ScalarKey key, out float value)
            {
                if (_configs.TryGetValue(key.Id, out var config))
                {
                    value = config.BaseValue;
                    return true;
                }

                value = 0f;
                return false;
            }

            public bool TryGetConfig(ScalarKey key, out ScalarRuntimeConfig config)
                => _configs.TryGetValue(key.Id, out config);
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly LifetimeScopeKind _kind;

            public TestScopeNode(LifetimeScopeKind kind)
            {
                _kind = kind;
            }

            public IScopeNode? Parent { get; set; }
            public IScopeIdentityService? Identity { get; set; }
            public LifetimeScopeKind Kind => _kind;
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
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                var nodes = new List<IScopeNode>();
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

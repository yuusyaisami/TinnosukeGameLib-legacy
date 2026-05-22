#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Common;
using Game.Trait;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class TraitListAuthorityMigrationTests
    {
        const int PayloadVarId = 900001;
        const int LegacyOnlyVarId = 900002;

        static readonly Type TraitListRuntimeType = typeof(TraitListChannelHubMB).Assembly
            .GetType("Game.UI.TraitListChannelRuntime", throwOnError: true)!;

        static readonly Type TraitListVisualInstanceType = typeof(TraitListChannelHubMB).Assembly
            .GetType("Game.UI.TraitListChannelVisualInstance", throwOnError: true)!;

        static readonly MethodInfo ApplyPayloadMethod = TraitListRuntimeType
            .GetMethod("ApplyPayloadToBlackboard", BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Test]
        public void ApplyPayloadToBlackboard_DoesNotMergeBlackboardBackIntoCommandVars()
        {
            GameObject hubGo = new GameObject("TraitListHub");
            GameObject visualGo = new GameObject("TraitListVisual");

            try
            {
                TraitListChannelHubMB hubMb = hubGo.AddComponent<TraitListChannelHubMB>();
                TestScopeNode owner = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
                object runtime = Activator.CreateInstance(TraitListRuntimeType, owner, hubMb, new TraitListChannelDefinition(), "default")!;

                TestRuntimeResolver instanceResolver = new TestRuntimeResolver();
                TestScopeNode visualScope = new TestScopeNode(parent: owner, kind: LifetimeScopeKind.Entity, resolver: instanceResolver);
                BlackboardService blackboard = new BlackboardService(visualScope);
                blackboard.LocalVars.TrySetVariant(LegacyOnlyVarId, DynamicVariant.FromString("legacy-only"));
                instanceResolver.Register<IBlackboardService>(blackboard);

                object visualInstance = Activator.CreateInstance(
                    TraitListVisualInstanceType,
                    "display",
                    new TestTraitInstance(),
                    visualGo.transform,
                    visualScope,
                    instanceResolver)!;

                VarStore payload = new VarStore(initialCapacity: 4);
                payload.TrySetVariant(PayloadVarId, DynamicVariant.FromString("payload"));

                IVarStore commandVars = (IVarStore)ApplyPayloadMethod.Invoke(runtime, new object[] { visualInstance, payload })!;

                Assert.That(commandVars.TryGetVariant(PayloadVarId, out DynamicVariant payloadValue), Is.True);
                Assert.That(payloadValue.TryGet<string>(out string? payloadText), Is.True);
                Assert.That(payloadText, Is.EqualTo("payload"));

                Assert.That(commandVars.Contains(LegacyOnlyVarId), Is.False);

                Assert.That(blackboard.LocalVars.TryGetVariant(PayloadVarId, out DynamicVariant blackboardValue), Is.True);
                Assert.That(blackboardValue.TryGet<string>(out string? blackboardText), Is.True);
                Assert.That(blackboardText, Is.EqualTo("payload"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visualGo);
                UnityEngine.Object.DestroyImmediate(hubGo);
            }
        }

        sealed class TestTraitDefinition : ITraitDefinition
        {
            public string DefinitionId => "test";
            public string RefKeyPrefix => "test";
            public TransformAnimationPreset? TraitListMovePreset => null;
            public PlaceableTraitSettings PlaceableSettings => default;

            public ITraitInstance CreateInstance(TraitInstanceContext context)
            {
                return new TestTraitInstance(context);
            }
        }

        sealed class TestTraitInstance : ITraitInstance
        {
            readonly TraitInstanceContext _context;

            public TestTraitInstance()
                : this(new TraitInstanceContext(scope: null, vars: new VarStore(initialCapacity: 4)))
            {
            }

            public TestTraitInstance(TraitInstanceContext context)
            {
                _context = context;
            }

            public string InstanceId => "trait-instance";
            public ITraitDefinition Definition { get; } = new TestTraitDefinition();
            public TraitInstanceContext Context => _context;

            public void OnLtsInstantiated(IScopeNode scope)
            {
                _ = scope;
            }

            public void OnAdded()
            {
            }

            public void OnHold()
            {
            }

            public void OnUse()
            {
            }

            public void OnRemove()
            {
            }
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object> _services = new();

            public void Register<T>(T instance)
            {
                _services[typeof(T)] = instance!;
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
                if (_services.TryGetValue(type, out object instance))
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
                return _services.TryGetValue(type, out object instance)
                    ? instance
                    : null;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                bool found = _services.TryGetValue(type, out object resolved);
                instance = resolved;
                return found;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out object resolved) && resolved is T typed)
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
            public TestScopeNode(IScopeNode? parent, LifetimeScopeKind kind, IRuntimeResolver resolver)
            {
                Parent = parent;
                Kind = kind;
                Resolver = resolver;
            }

            public IScopeNode? Parent { get; }

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind { get; }

            public IRuntimeResolver? Resolver { get; }

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
                return Parent == null
                    ? new IScopeNode[] { this }
                    : null;
            }
        }
    }
}

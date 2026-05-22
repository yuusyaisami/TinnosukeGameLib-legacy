#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class TooltipChannelMigrationTests
    {
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TooltipChannelHubService_RejectsMissingTooltipSystemService()
        {
            GameObject go = new GameObject("TooltipChannelHub");
            try
            {
                TooltipChannelHubMB hubMb = go.AddComponent<TooltipChannelHubMB>();
                TestScopeNode owner = new TestScopeNode(new TestRuntimeResolver());
                TooltipChannelHubService hub = new TooltipChannelHubService(owner, hubMb);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => hub.OnAcquire(owner, false));

                Assert.That(exception!.Message, Does.Contain("TooltipSystemService"));
                Assert.That(exception.Message, Does.Contain("Explicit family authority"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TooltipChannelHubService_RejectsUiSpaceWithoutExplicitTooltipRoot()
        {
            GameObject go = new GameObject("TooltipChannelHub", typeof(RectTransform));
            try
            {
                TooltipChannelHubMB hubMb = go.AddComponent<TooltipChannelHubMB>();
                SetPrivateField(hubMb, "_spaceKind", TooltipChannelSpaceKind.UIScreen);

                TestRuntimeResolver resolver = new TestRuntimeResolver();
                resolver.Register<ITooltipSystemService>(new TestTooltipSystemService(
                    tooltipRoot: null,
                    sharedHubPreset: CreateSharedHubPreset(TooltipChannelRenderSpaceKind.UIScreen)));

                TestScopeNode owner = new TestScopeNode(resolver);
                TooltipChannelHubService hub = new TooltipChannelHubService(owner, hubMb);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => hub.OnAcquire(owner, false));

                Assert.That(exception!.Message, Does.Contain("TooltipRoot"));
                Assert.That(exception.Message, Does.Contain("explicit UI root"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
            field!.SetValue(target, value);
        }

        static TooltipHubPreset CreateSharedHubPreset(TooltipChannelRenderSpaceKind renderSpace)
        {
            TooltipHubPreset preset = new TooltipHubPreset();
            SetPrivateField(preset, "_renderSpace", renderSpace);
            return preset;
        }

        sealed class TestTooltipSystemService : ITooltipSystemService
        {
            readonly RectTransform? _tooltipRoot;
            readonly TooltipHubPreset _sharedHubPreset;

            public TestTooltipSystemService(RectTransform? tooltipRoot, TooltipHubPreset sharedHubPreset)
            {
                _tooltipRoot = tooltipRoot;
                _sharedHubPreset = sharedHubPreset;
            }

            public RectTransform TooltipRoot => _tooltipRoot!;
            public Transform? WorldRoot => null;
            public RectTransform? ClampArea => null;
            public TooltipChannelInputMode InputMode => TooltipChannelInputMode.Pointer;
            public TooltipClampSettings ClampSettings => TooltipClampSettings.Default;
            public int SpawnWarmupFrames => 0;
            public TooltipSystemSharedDefaults SharedDefaults => new TooltipSystemSharedDefaults();
            public TooltipHubPreset SharedHubPreset => _sharedHubPreset;
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
            public TestScopeNode(IRuntimeResolver resolver)
            {
                Resolver = resolver;
            }

            public IScopeNode? Parent => null;
            public IScopeIdentityService? Identity => null;
            public LifetimeScopeKind Kind => LifetimeScopeKind.UI;
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

            public UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default)
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

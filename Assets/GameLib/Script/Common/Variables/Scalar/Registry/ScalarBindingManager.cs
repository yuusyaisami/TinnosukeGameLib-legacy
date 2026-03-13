// Assets/Game/Script/Core/Scalar/ScalarBindingManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Game;
using Game.Commands;
using VContainer;

namespace Game.Scalar
{
    public interface IScalarBindingManager : ITickable
    {
        // 直接サービスを渡す版（Entity-UI など動的なもの向け）
        ScalarBindingHandle Bind(
            IBaseScalarService sourceService,
            ScalarKey sourceKey,
            IBaseScalarService targetService,
            ScalarKey targetKey,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp = default,
            string tag = null,
            ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd);

        // ScalarRef + Registry を使う版（Library, Global, Scene, UI など単一インスタンス前提）
        ScalarBindingHandle Bind(
            ScalarRef source,
            ScalarRef target,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp = default,
            string tag = null,
            ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd);
    }
    public sealed class ScalarBindingHandle : IDisposable
    {
        internal ScalarBindingRuntime Runtime;
        ScalarBindingManager _owner;

        internal ScalarBindingHandle(ScalarBindingManager owner, ScalarBindingRuntime runtime)
        {
            _owner = owner;
            Runtime = runtime;
        }

        public bool IsValid => Runtime != null;

        /// <summary>
        /// Delta 系バインドの基準値を現在値にリベースする。BaseScalarService の Baseline/LocalBase が動いた後に使う想定。
        /// </summary>
        public void Rebase()
        {
            Runtime?.Rebase();
        }

        public void Dispose()
        {
            if (_owner != null && Runtime != null)
            {
                _owner.Unregister(Runtime);
                Runtime.Dispose();
            }

            _owner = null;
            Runtime = null;
        }
    }
    internal sealed class ScalarBindingRuntime : IDisposable
    {
        public readonly IBaseScalarService Source;
        public readonly IBaseScalarService Target;
        public readonly ScalarKey SourceKey;
        public readonly ScalarKey TargetKey;
        public readonly ScalarLinkMode Mode;
        public readonly float Factor;
        public readonly ScalarLinkClamp Clamp;
        public readonly string Tag;

        // デバッグ用メタ
        public readonly ScalarRef SourceRef;
        public readonly ScalarRef TargetRef;

        public readonly ScalarMulPhase TargetMulPhase;  // ★ 追加

        readonly ScalarHandle _targetHandle;
        float _baseSource; // 初期値を基準に差分を取る。必要なら Rebase で更新する。
        float _lastEffective;
        float _lastModValue;
        bool _disposed;

        public float BaseSource => _baseSource;
        public float LastEffective => _lastEffective;
        public float LastModValue => _lastModValue;

        public ScalarBindingRuntime(
        IBaseScalarService source,
        ScalarKey sourceKey,
        IBaseScalarService target,
        ScalarKey targetKey,
        ScalarLinkMode mode,
        float factor,
        ScalarLinkClamp clamp,
        string tag,
        ScalarMulPhase targetMulPhase)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            SourceKey = sourceKey;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            TargetKey = targetKey;
            Mode = mode;
            Factor = factor;
            Clamp = clamp;
            Tag = tag;
            TargetMulPhase = targetMulPhase;

            if (source is not BaseScalarService srcBase || target is not BaseScalarService dstBase)
                throw new ArgumentException("ScalarBinding requires BaseScalarService for source/target.");

            SourceRef = new ScalarRef(srcBase.Space, sourceKey);
            TargetRef = new ScalarRef(dstBase.Space, targetKey);

            _baseSource = Source.LocalGet(SourceKey);
            float initialEffective = ComputeEffectiveSource(_baseSource);
            float initialModValue = ComputeTargetMod(initialEffective);

            bool isMul = Mode == ScalarLinkMode.DeltaToMul || Mode == ScalarLinkMode.ValueToMul;

            _targetHandle = isMul
                ? Target.LocalMul(TargetKey, layer: null,
                    factor: initialModValue,
                    phase: TargetMulPhase,
                    duration: -1f,
                    source: this,
                    tag: tag)
                : Target.LocalAdd(TargetKey, layer: null,
                    delta: initialModValue,
                    duration: -1f,
                    source: this,
                    tag: tag);

            _lastEffective = initialEffective;
            _lastModValue = initialModValue;
        }

        public void Rebase()
        {
            if (_disposed)
                return;

            _baseSource = Source.LocalGet(SourceKey);
            float effective = ComputeEffectiveSource(_baseSource);
            float modValue = ComputeTargetMod(effective);
            _targetHandle.SetValue(modValue);
            _lastEffective = effective;
            _lastModValue = modValue;
        }

        public void Tick()
        {
            if (_disposed)
                return;

            float current = Source.LocalGet(SourceKey);
            float effective = ComputeEffectiveSource(current);

            if (Mathf.Approximately(effective, _lastEffective))
                return;

            float modValue = ComputeTargetMod(effective);
            _targetHandle.SetValue(modValue);
            _lastEffective = effective;
            _lastModValue = modValue;
        }

        float ComputeEffectiveSource(float current)
        {
            float raw;
            switch (Mode)
            {
                case ScalarLinkMode.DeltaToAdd:
                case ScalarLinkMode.DeltaToMul:
                    raw = current - _baseSource;
                    break;
                case ScalarLinkMode.ValueToAdd:
                case ScalarLinkMode.ValueToMul:
                    raw = current;
                    break;
                default:
                    raw = current;
                    break;
            }

            return Clamp.Apply(raw);
        }

        float ComputeTargetMod(float effective)
        {
            switch (Mode)
            {
                case ScalarLinkMode.DeltaToAdd:
                case ScalarLinkMode.ValueToAdd:
                    return Factor * effective;

                case ScalarLinkMode.DeltaToMul:
                case ScalarLinkMode.ValueToMul:
                    return 1f + Factor * effective;

                default:
                    return Factor * effective;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _targetHandle?.Dispose();
        }
    }
    public sealed class ScalarBindingManager : IScalarBindingManager, IScalarBindingTelemetry
    {
        readonly IBaseLifetimeScopeRegistry _ltsRegistry;
        readonly List<ScalarBindingRuntime> _bindings = new();

        public ScalarBindingManager(IBaseLifetimeScopeRegistry ltsRegistry)
        {
            _ltsRegistry = ltsRegistry ?? throw new ArgumentNullException(nameof(ltsRegistry));
        }

        public ScalarBindingHandle Bind(
        IBaseScalarService sourceService,
        ScalarKey sourceKey,
        IBaseScalarService targetService,
        ScalarKey targetKey,
        ScalarLinkMode mode,
        float factor,
        ScalarLinkClamp clamp = default,
        string tag = null,
        ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd)
        {
            if (sourceService == null) throw new ArgumentNullException(nameof(sourceService));
            if (targetService == null) throw new ArgumentNullException(nameof(targetService));

            var runtime = new ScalarBindingRuntime(
                sourceService,
                sourceKey,
                targetService,
                targetKey,
                mode,
                factor,
                clamp,
                tag,
                targetMulPhase);

            _bindings.Add(runtime);
            return new ScalarBindingHandle(this, runtime);
        }

        public ScalarBindingHandle Bind(
            ScalarRef source,
            ScalarRef target,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp = default,
            string tag = null,
            ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd)
        {
            var srcServices = ResolveServices(source.Space);
            var dstServices = ResolveServices(target.Space);

            if (srcServices.Count == 0 || dstServices.Count == 0)
            {
                Debug.LogWarning($"[ScalarBindingManager] Bind failed. src={source}, dst={target} service not found.");
                return null;
            }

            var src = srcServices[0];
            var dst = dstServices[0];

            return Bind(src, source.Key, dst, target.Key, mode, factor, clamp, tag, targetMulPhase);
        }

        internal void Unregister(ScalarBindingRuntime runtime)
        {
            _bindings.Remove(runtime);
        }

        public void Tick()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                _bindings[i].Tick();
            }
        }

        IReadOnlyList<IBaseScalarService> ResolveServices(LifetimeScopeKind space)
        {
            var list = new List<IBaseScalarService>();

            if (_ltsRegistry == null)
                return list;

            var filter = new CommandTargetIdentityFilter
            {
                kind = space,
                requireActive = false,
                searchScope = CommandTargetSearchScope.All,
            };

            var scopes = _ltsRegistry.ResolveAll(filter);
            if (scopes == null)
                return list;

            for (int i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope?.Resolver == null)
                    continue;

                if (scope.Resolver.TryResolve<IBaseScalarService>(out var service) && service != null)
                {
                    list.Add(service);
                }
            }

            return list;
        }
        // ===== テレメトリ =====

        public IReadOnlyList<ScalarBindingDebugInfo> GetBindings()
        {
            var list = new List<ScalarBindingDebugInfo>(_bindings.Count);

            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                list.Add(new ScalarBindingDebugInfo(
                    b.SourceRef,
                    b.TargetRef,
                    b.Mode,
                    b.Factor,
                    b.Clamp,
                    b.Tag,
                    b.BaseSource,
                    b.LastEffective,
                    b.LastModValue));
            }

            return list;
        }
    }
}




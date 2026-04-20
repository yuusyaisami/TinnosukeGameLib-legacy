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
    public interface IScalarBindingManager : IScopeTickHandler
    {
        // 逶ｴ謗･繧ｵ繝ｼ繝薙せ繧呈ｸ｡縺咏沿・・ntity-UI 縺ｪ縺ｩ蜍慕噪縺ｪ繧ゅ・蜷代￠・・
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

        // ScalarRef + Registry 繧剃ｽｿ縺・沿・・ibrary, Global, Scene, UI 縺ｪ縺ｩ蜊倅ｸ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ蜑肴署・・
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
        /// Delta 邉ｻ繝舌う繝ｳ繝峨・蝓ｺ貅門､繧堤樟蝨ｨ蛟､縺ｫ繝ｪ繝吶・繧ｹ縺吶ｋ縲・aseScalarService 縺ｮ Baseline/LocalBase 縺悟虚縺・◆蠕後↓菴ｿ縺・Φ螳壹・
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

        // 繝・ヰ繝・げ逕ｨ繝｡繧ｿ
        public readonly ScalarRef SourceRef;
        public readonly ScalarRef TargetRef;

        public readonly ScalarMulPhase TargetMulPhase;  // 笘・霑ｽ蜉

        readonly ScalarHandle _targetHandle;
        float _baseSource; // 蛻晄悄蛟､繧貞渕貅悶↓蟾ｮ蛻・ｒ蜿悶ｋ縲ょｿ・ｦ√↑繧・Rebase 縺ｧ譖ｴ譁ｰ縺吶ｋ縲・
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
        // ===== 繝・Ξ繝｡繝医Μ =====

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




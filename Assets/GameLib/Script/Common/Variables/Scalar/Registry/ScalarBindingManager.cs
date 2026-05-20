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
        // 直接サービスを渡す版�E�Entity-UI など動的なも�E向け�E�E
        ScalarBindingHandle Bind(
            BaseScalarService sourceService,
            ScalarKey sourceKey,
            BaseScalarService targetService,
            ScalarKey targetKey,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp = default,
            string tag = null,
            ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd);

        // ScalarRef + Registry を使ぁE���E�Eibrary, Global, Scene, UI など単一インスタンス前提�E�E
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
        /// Delta 系バインド�E基準値を現在値にリベ�Eスする、EaseScalarService の Baseline/LocalBase が動ぁE��後に使ぁE��定、E
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

        // チE��チE��用メタ
        public readonly ScalarRef SourceRef;
        public readonly ScalarRef TargetRef;

        public readonly ScalarMulPhase TargetMulPhase;  // ☁E追加

        readonly ScalarHandle _targetHandle;
        float _baseSource; // 初期値を基準に差刁E��取る。忁E��なめERebase で更新する、E
        float _lastEffective;
        float _lastModValue;
        bool _disposed;

        public float BaseSource => _baseSource;
        public float LastEffective => _lastEffective;
        public float LastModValue => _lastModValue;

        public ScalarBindingRuntime(
            BaseScalarService source,
            ScalarKey sourceKey,
            BaseScalarService target,
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

            SourceRef = new ScalarRef(source.Space, sourceKey);
            TargetRef = new ScalarRef(target.Space, targetKey);

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
        readonly List<ScalarBindingRuntime> _bindings = new();

        public ScalarBindingHandle Bind(
            BaseScalarService sourceService,
            ScalarKey sourceKey,
            BaseScalarService targetService,
            ScalarKey targetKey,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp = default,
            string tag = null,
            ScalarMulPhase targetMulPhase = ScalarMulPhase.PreAdd)
        {
            if (sourceService == null) throw new ArgumentNullException(nameof(sourceService));
            if (targetService == null) throw new ArgumentNullException(nameof(targetService));

            if (!sourceKey.IsVerified || !targetKey.IsVerified)
            {
                Debug.LogError($"[ScalarBindingManager] Bind failed. unresolved key source={sourceKey} target={targetKey}");
                return null;
            }

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
            if (!source.Key.IsVerified || !target.Key.IsVerified)
            {
                throw new NotSupportedException($"[ScalarBindingManager] Bind failed. unresolved key source={source} target={target}");
            }

            throw new NotSupportedException($"[ScalarBindingManager] Bind failed. explicit verified endpoints are required. source={source} target={target}");
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
        // ===== チE��メトリ =====

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




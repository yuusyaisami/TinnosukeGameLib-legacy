#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.TransformSystem
{
    [Flags]
    public enum TransformAnimationProperty
    {
        None = 0,
        WorldPosition = 1 << 0,
        LocalPosition = 1 << 1,
        LocalRotation = 1 << 2,
        LocalScale = 1 << 3,
        AnchoredPosition = 1 << 4,
        SizeDelta = 1 << 5,
        Pivot = 1 << 6,
    }

    /// <summary>
    /// 合成済み pose バッファ。
    /// TransformTargetDirector が合成結果を書き込み、TransformControllerService が読み出して適用する。
    /// </summary>
    public sealed class TransformAnimationOutput
    {
        // composed pose flags
        TransformAnimationProperty _composedActive;

        Vector3 _worldPosition;
        Vector3 _localPosition;
        Vector3 _localEulerAngles;
        Vector3 _localScale = Vector3.one;
        Vector2 _anchoredPosition;
        Vector2 _sizeDelta;
        Vector2 _pivot;

        // additive-only flags (no replace base)
        bool _localPositionAdditiveOnly;
        bool _localRotationAdditiveOnly;

        public Vector3 WorldPosition => _worldPosition;
        public Vector3 LocalPosition => _localPosition;
        public Vector3 LocalEulerAngles => _localEulerAngles;
        public Vector3 LocalScale => _localScale;
        public Vector2 AnchoredPosition => _anchoredPosition;
        public Vector2 SizeDelta => _sizeDelta;
        public Vector2 Pivot => _pivot;

        /// <summary>additive-only (replace なし) の LocalPosition かどうか。</summary>
        public bool IsLocalPositionAdditiveOnly => _localPositionAdditiveOnly;
        /// <summary>additive-only (replace なし) の LocalRotation かどうか。</summary>
        public bool IsLocalRotationAdditiveOnly => _localRotationAdditiveOnly;

        public bool IsActive(TransformAnimationProperty property)
        {
            return (_composedActive & property) != 0;
        }

        // ===== Composed pose write API (called by TransformTargetDirector) =====

        internal void ClearComposed()
        {
            _composedActive = TransformAnimationProperty.None;
            _worldPosition = Vector3.zero;
            _localPosition = Vector3.zero;
            _localEulerAngles = Vector3.zero;
            _localScale = Vector3.one;
            _anchoredPosition = Vector2.zero;
            _sizeDelta = Vector2.zero;
            _pivot = Vector2.zero;
            _localPositionAdditiveOnly = false;
            _localRotationAdditiveOnly = false;
        }

        internal void SetComposedWorldPosition(Vector3 value)
        {
            _composedActive |= TransformAnimationProperty.WorldPosition;
            _worldPosition = value;
        }

        internal void SetComposedLocalPosition(Vector3 value)
        {
            _composedActive |= TransformAnimationProperty.LocalPosition;
            _localPosition = value;
            _localPositionAdditiveOnly = false;
        }

        internal void SetComposedLocalPositionAdditive(Vector3 additive)
        {
            _composedActive |= TransformAnimationProperty.LocalPosition;
            _localPosition = additive;
            _localPositionAdditiveOnly = true;
        }

        internal void SetComposedLocalEulerAngles(Vector3 value, bool hasReplace)
        {
            _composedActive |= TransformAnimationProperty.LocalRotation;
            _localEulerAngles = value;
            _localRotationAdditiveOnly = !hasReplace;
        }

        internal void SetComposedLocalScale(Vector3 value)
        {
            _composedActive |= TransformAnimationProperty.LocalScale;
            _localScale = value;
        }

        internal void SetComposedAnchoredPosition(Vector2 value)
        {
            _composedActive |= TransformAnimationProperty.AnchoredPosition;
            _anchoredPosition = value;
        }

        internal void SetComposedSizeDelta(Vector2 value)
        {
            _composedActive |= TransformAnimationProperty.SizeDelta;
            _sizeDelta = value;
        }

        internal void SetComposedPivot(Vector2 value)
        {
            _composedActive |= TransformAnimationProperty.Pivot;
            _pivot = value;
        }

        public void Clear()
        {
            ClearComposed();
        }
    }

    public interface ITransformAnimationOutputSink
    {
        Transform TargetTransform { get; }
        TransformAnimationOutput AnimationOutput { get; }
    }

    public interface ITransformAnimationOutputRegistry
    {
        bool TryGetSink(Transform target, out ITransformAnimationOutputSink sink);
        void Register(ITransformAnimationOutputSink sink);
        void Unregister(ITransformAnimationOutputSink sink);
    }

    public sealed class TransformAnimationOutputRegistryService : ITransformAnimationOutputRegistry
    {
        readonly Dictionary<Transform, ITransformAnimationOutputSink> _sinks = new();

        public bool TryGetSink(Transform target, out ITransformAnimationOutputSink sink)
        {
            if (target == null)
            {
                sink = null!;
                return false;
            }

            return _sinks.TryGetValue(target, out sink);
        }

        public void Register(ITransformAnimationOutputSink sink)
        {
            if (sink == null)
                return;

            var target = sink.TargetTransform;
            if (target == null)
                return;

            _sinks[target] = sink;
        }

        public void Unregister(ITransformAnimationOutputSink sink)
        {
            if (sink == null)
                return;

            var target = sink.TargetTransform;
            if (target == null)
                return;

            if (_sinks.TryGetValue(target, out var existing) && ReferenceEquals(existing, sink))
                _sinks.Remove(target);
        }
    }
}

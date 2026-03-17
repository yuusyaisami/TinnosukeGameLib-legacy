#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// target Transform ごとに 1 つ存在する合成器。
    /// 複数 track の寄与を集めて合成し、最終 pose を TransformAnimationOutput へ書き出す。
    /// </summary>
    public interface ITransformTargetDirector
    {
        Transform Target { get; }
        void AddTrack(ITransformModifierTrack track);
        void RemoveTrack(ITransformModifierTrack track);
        void Tick(float deltaTime);
        bool HasActiveTracks { get; }
        IReadOnlyList<ITransformModifierTrack> ActiveTracks { get; }
    }

    public sealed class TransformTargetDirector : ITransformTargetDirector
    {
        readonly Transform _target;
        TransformAnimationOutput _output;
        readonly List<ITransformModifierTrack> _tracks = new();
        readonly List<ITransformModifierTrack> _removeBuffer = new();
        bool _applyDirectly;

        public Transform Target => _target;
        public bool HasActiveTracks => _tracks.Count > 0;
        public IReadOnlyList<ITransformModifierTrack> ActiveTracks => _tracks;

        public TransformTargetDirector(Transform target, TransformAnimationOutput output, bool applyDirectly)
        {
            _target = target;
            _output = output;
            _applyDirectly = applyDirectly;
        }

        public void BindOutput(TransformAnimationOutput output)
        {
            if (output == null || ReferenceEquals(_output, output))
                return;

            _output = output;
            _applyDirectly = false;
        }

        public void AddTrack(ITransformModifierTrack track)
        {
            if (track == null || _tracks.Contains(track))
                return;
            _tracks.Add(track);
        }

        public void RemoveTrack(ITransformModifierTrack track)
        {
            _tracks.Remove(track);
        }

        public void Tick(float deltaTime)
        {
            if (_tracks.Count == 0)
                return;

            // 1. Tick all tracks
            for (int i = 0; i < _tracks.Count; i++)
                _tracks[i].Tick(deltaTime);

            // 2. Remove dead tracks
            _removeBuffer.Clear();
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (!_tracks[i].IsAlive)
                    _removeBuffer.Add(_tracks[i]);
            }
            for (int i = 0; i < _removeBuffer.Count; i++)
                _tracks.Remove(_removeBuffer[i]);

            // 3. Collect contributions
            var accumulator = TransformPoseAccumulator.Create();
            for (int i = 0; i < _tracks.Count; i++)
                _tracks[i].WriteContribution(ref accumulator);

            // 4. Resolve and write to output
            ApplyToOutput(ref accumulator);

            if (_applyDirectly)
                ApplyDirectlyToTarget();
        }

        void ApplyToOutput(ref TransformPoseAccumulator acc)
        {
            _output.ClearComposed();

            // Position: WorldPosition と LocalPosition の競合解決
            if (acc.HasWorldPosition || acc.HasLocalPosition)
            {
                acc.ResolvePositionConflict(out var useWorld);

                if (useWorld)
                {
                    _output.SetComposedWorldPosition(acc.WorldPositionValue + acc.LocalPositionAdditive);
                }
                else
                {
                    _output.SetComposedLocalPosition(acc.LocalPositionValue + acc.LocalPositionAdditive);
                }
            }
            else if (acc.LocalPositionAdditive != Vector3.zero)
            {
                // additive only (e.g. shake)
                _output.SetComposedLocalPositionAdditive(acc.LocalPositionAdditive);
            }

            // Rotation
            if (acc.HasLocalRotation || acc.LocalRotationAdditive != Vector3.zero)
            {
                var euler = acc.HasLocalRotation
                    ? acc.LocalRotationValue + acc.LocalRotationAdditive
                    : acc.LocalRotationAdditive;
                _output.SetComposedLocalEulerAngles(euler, acc.HasLocalRotation);
            }

            // Scale
            if (acc.HasLocalScale || acc.HasLocalScaleMultiply)
            {
                var scale = acc.HasLocalScale ? acc.LocalScaleValue : Vector3.one;
                if (acc.HasLocalScaleMultiply)
                    scale = Vector3.Scale(scale, acc.LocalScaleMultiply);
                _output.SetComposedLocalScale(scale);
            }

            // UI
            if (acc.HasAnchoredPosition)
                _output.SetComposedAnchoredPosition(acc.AnchoredPositionValue);

            if (acc.HasSizeDelta)
                _output.SetComposedSizeDelta(acc.SizeDeltaValue);

            if (acc.HasPivot)
                _output.SetComposedPivot(acc.PivotValue);
        }

        void ApplyDirectlyToTarget()
        {
            if (_target == null)
                return;

            if (_output.IsActive(TransformAnimationProperty.WorldPosition))
            {
                _target.position = _output.WorldPosition;
            }
            else if (_output.IsActive(TransformAnimationProperty.LocalPosition))
            {
                if (_output.IsLocalPositionAdditiveOnly)
                    _target.localPosition += _output.LocalPosition;
                else
                    _target.localPosition = _output.LocalPosition;
            }
            else if (_output.IsActive(TransformAnimationProperty.AnchoredPosition) && _target is RectTransform anchoredRect)
            {
                anchoredRect.anchoredPosition = _output.AnchoredPosition;
            }

            if (_output.IsActive(TransformAnimationProperty.LocalRotation))
            {
                if (_output.IsLocalRotationAdditiveOnly)
                    _target.localEulerAngles += _output.LocalEulerAngles;
                else
                    _target.localEulerAngles = _output.LocalEulerAngles;
            }

            if (_output.IsActive(TransformAnimationProperty.LocalScale))
                _target.localScale = _output.LocalScale;

            if (_target is not RectTransform rect)
                return;

            if (_output.IsActive(TransformAnimationProperty.SizeDelta))
                rect.sizeDelta = _output.SizeDelta;

            if (_output.IsActive(TransformAnimationProperty.Pivot))
            {
                if (_output.IsActive(TransformAnimationProperty.AnchoredPosition))
                {
                    rect.pivot = _output.Pivot;
                }
                else
                {
                    SetPivotWithPositionPreserved(rect, _output.Pivot);
                }
            }
        }

        static void SetPivotWithPositionPreserved(RectTransform rect, Vector2 newPivot)
        {
            var oldPivot = rect.pivot;
            var size = rect.rect.size;
            var deltaPos = new Vector2(
                (newPivot.x - oldPivot.x) * size.x,
                (newPivot.y - oldPivot.y) * size.y);

            rect.pivot = newPivot;
            rect.anchoredPosition += deltaPos;
        }
    }
}

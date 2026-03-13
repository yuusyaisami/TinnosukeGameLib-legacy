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

        public Transform Target => _target;
        public bool HasActiveTracks => _tracks.Count > 0;
        public IReadOnlyList<ITransformModifierTrack> ActiveTracks => _tracks;

        public TransformTargetDirector(Transform target, TransformAnimationOutput output)
        {
            _target = target;
            _output = output;
        }

        public void BindOutput(TransformAnimationOutput output)
        {
            if (output == null || ReferenceEquals(_output, output))
                return;

            _output = output;
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
    }
}

// Assets/Game/Script/Core/Scalar/ScalarTelemetryDef.cs
using System;
using System.Collections.Generic;

namespace Game.Scalar
{
    /// <summary>
    /// Debug 表示用のスナップショット。
    /// 基本的には Editor / UI テレメトリでのみ使用。
    /// </summary>
    public readonly struct ScalarSnapshot
    {
        public readonly ScalarKey Key;
        public readonly ScalarModKind Kind;
        public readonly ScalarMulPhase Phase;
        public readonly float Value;
        public readonly float Remain;
        public readonly object Source;
        public readonly string Tag;
        public readonly string Layer;
        public readonly Guid Id;

        public ScalarSnapshot(
            ScalarKey key,
            ScalarModKind kind,
            ScalarMulPhase phase,
            float value,
            float remain,
            object source,
            string tag,
            string layer,
            Guid id)
        {
            Key = key;
            Kind = kind;
            Phase = phase;
            Value = value;
            Remain = remain;
            Source = source;
            Tag = tag;
            Layer = layer;
            Id = id;
        }
    }

    public interface IScalarTelemetry
    {
        /// <summary>
        /// 特定キーに対するすべての修正値を列挙。
        /// </summary>
        IEnumerable<ScalarSnapshot> Enumerate(ScalarKey key);
    }
}

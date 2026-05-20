// Assets/Game/Script/Core/Scalar/ScalarTelemetryDef.cs
using System;
using System.Collections.Generic;

namespace Game.Scalar
{
    public enum LayeredNumericLaneKind : byte
    {
        Unknown = 0,
        Base = 10,
        PrefixMul = 20,
        Add = 30,
        SuffixMul = 40,
        FinalClamp = 50,
        Effective = 60,
    }

    /// <summary>
    /// Debug 表示用のスナップショット。
    /// 基本的には Editor / UI テレメトリでのみ使用。
    /// </summary>
    public readonly struct ScalarSnapshot
    {
        public readonly ScalarKey Key;
        public readonly LayeredNumericLaneKind Lane;
        public readonly ScalarModKind Kind;
        public readonly ScalarMulPhase Phase;
        public readonly float Value;
        public readonly float Remain;
        public readonly object Source;
        public readonly string Tag;
        public readonly string Layer;
        public readonly Guid Id;
        public readonly int Revision;
        public readonly float ClampMin;
        public readonly float ClampMax;
        public readonly bool HasClampMin;
        public readonly bool HasClampMax;

        public ScalarSnapshot(
            ScalarKey key,
            LayeredNumericLaneKind lane,
            ScalarModKind kind,
            ScalarMulPhase phase,
            float value,
            float remain,
            object source,
            string tag,
            string layer,
            Guid id,
            int revision,
            float clampMin,
            float clampMax,
            bool hasClampMin,
            bool hasClampMax)
        {
            Key = key;
            Lane = lane;
            Kind = kind;
            Phase = phase;
            Value = value;
            Remain = remain;
            Source = source;
            Tag = tag;
            Layer = layer;
            Id = id;
            Revision = revision;
            ClampMin = clampMin;
            ClampMax = clampMax;
            HasClampMin = hasClampMin;
            HasClampMax = hasClampMax;
        }
    }

    public interface IScalarTelemetry
    {
        /// <summary>
        /// 現在このサービスに登録されているすべての ScalarKey を列挙。
        /// </summary>
        IEnumerable<ScalarKey> EnumerateKeys();

        /// <summary>
        /// 特定キーに対するすべての修正値を列挙。
        /// </summary>
        IEnumerable<ScalarSnapshot> Enumerate(ScalarKey key);
    }
}

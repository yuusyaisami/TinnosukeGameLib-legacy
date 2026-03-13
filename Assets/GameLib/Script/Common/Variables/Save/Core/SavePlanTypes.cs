#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Save
{
    public enum SaveTargetKind : byte
    {
        Blackboard = 0,
        Scalar = 1,
    }

    public readonly struct SaveEntry : IEquatable<SaveEntry>
    {
        public readonly SaveLayer Layer;
        public readonly SaveTargetKind Kind;
        public readonly int VarId;
        public readonly int ScalarKeyId;
        public readonly string SourceProfileType;
        public readonly string SourceBindingName;

        public SaveEntry(
            SaveLayer layer,
            SaveTargetKind kind,
            int varId,
            int scalarKeyId,
            string sourceProfileType = "",
            string sourceBindingName = "")
        {
            Layer = layer;
            Kind = kind;
            VarId = varId;
            ScalarKeyId = scalarKeyId;
            SourceProfileType = sourceProfileType ?? string.Empty;
            SourceBindingName = sourceBindingName ?? string.Empty;
        }

        public bool IsValid => (VarId != 0) ^ (ScalarKeyId != 0);

        public bool Equals(SaveEntry other)
            => Layer == other.Layer
               && Kind == other.Kind
               && VarId == other.VarId
               && ScalarKeyId == other.ScalarKeyId
               && string.Equals(SourceProfileType, other.SourceProfileType, StringComparison.Ordinal)
               && string.Equals(SourceBindingName, other.SourceBindingName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SaveEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Layer, Kind, VarId, ScalarKeyId, SourceProfileType, SourceBindingName);
    }

    public sealed class SaveEntryComparer : IEqualityComparer<SaveEntry>
    {
        public bool Equals(SaveEntry x, SaveEntry y)
        {
            if (x.Kind != y.Kind) return false;
            if (x.Layer != y.Layer) return false;
            return x.Kind == SaveTargetKind.Blackboard ? x.VarId == y.VarId : x.ScalarKeyId == y.ScalarKeyId;
        }

        public int GetHashCode(SaveEntry obj)
        {
            return HashCode.Combine(obj.Layer, obj.Kind, obj.Kind == SaveTargetKind.Blackboard ? obj.VarId : obj.ScalarKeyId);
        }
    }

    public sealed class SavePlan
    {
        public readonly IReadOnlyList<SaveEntry> Entries;

        public SavePlan(IReadOnlyList<SaveEntry> entries)
        {
            Entries = entries ?? Array.Empty<SaveEntry>();
        }
    }

    [Serializable]
    public struct SavePayload
    {
        public int SaveVer;
        public BlackboardVarPayload[] Blackboard;
        public ScalarKeyPayload[] Scalars;
    }

    [Serializable]
    public struct BlackboardVarPayload
    {
        public int VarId;
        public byte Kind;
        public double Numeric;
        public string Str;
        public float X;
        public float Y;
        public float Z;
        public float W;
    }

    [Serializable]
    public struct ScalarModPayload
    {
        public byte Kind;
        public byte Phase;
        public float Value;
        public float Remain;
        public string Layer;
        public string Tag;
    }

    [Serializable]
    public struct ScalarKeyPayload
    {
        public int KeyId;
        public string Name;
        public float Baseline;
        public float LocalBase;
        public ScalarModPayload[] Mods;
    }
}

// Binding-less scalar debug viewer: shows values and modifiers for the attached scalar service.
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.Scalar
{
    [Serializable]
    public sealed class ScalarDebugView
    {
        [NonSerialized] IBaseScalarService _scalar;
        [NonSerialized] IScalarTelemetry _telemetry;

        bool _initialized;

        [FoldoutGroup("Edit"), LabelText("Key")]
        [InlineProperty, HideLabel]
        public ScalarKey editKey;

        [FoldoutGroup("Edit"), LabelText("Set LocalBase")]
        public float editLocalBase;

        [FoldoutGroup("Group")]
        public float editAddDelta;

        [FoldoutGroup("Edit"), LabelText("Mul Factor")]
        public float editMulFactor = 1f;

        [FoldoutGroup("Edit"), ShowInInspector, ReadOnly, LabelText("Current Value")]
        public float CurrentValue
        {
            get
            {
                if (!_initialized || string.IsNullOrWhiteSpace(editKey.Name))
                    return 0f;
                return _scalar.LocalGet(editKey);
            }
        }

        public void Initialize(IBaseScalarService scalar, IScalarTelemetry telemetry)
        {
            _scalar = scalar;
            _telemetry = telemetry;
            _initialized = scalar != null && telemetry != null;
        }

        [FoldoutGroup("Edit"), Button(ButtonSizes.Medium)]
        public void ApplySetLocalBase()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKey.Name))
                return;

            _scalar.SetLocalBase(editKey, editLocalBase);
        }

        [FoldoutGroup("Edit"), Button(ButtonSizes.Medium)]
        public void ApplyAddDelta()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKey.Name))
                return;

            _scalar.LocalAdd(editKey, layer: null, delta: editAddDelta, duration: -1f, source: this, tag: "Debug.Add");
        }

        [FoldoutGroup("Edit"), Button(ButtonSizes.Medium)]
        public void ApplyMul()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKey.Name))
                return;

            _scalar.LocalMul(editKey, layer: null, factor: editMulFactor, phase: ScalarMulPhase.PostAdd, duration: -1f, source: this, tag: "Debug.Mul");
        }

        [FoldoutGroup("Edit"), Button(ButtonSizes.Medium)]
        public void ClearDebugMods()
        {
            if (!_initialized)
                return;

            if (!string.IsNullOrWhiteSpace(editKey.Name))
            {
                _scalar.ClearAll(editKey);
            }
            else
            {
                _scalar.ClearAll();
            }
        }

        [FoldoutGroup("Watch"), ShowInInspector, ReadOnly]
        [LabelText("Registered Scalars")]
        public List<WatchedScalarRow> WatchedScalars =>
            !_initialized
                ? new List<WatchedScalarRow>()
                : BuildRegisteredRows();

        List<WatchedScalarRow> BuildRegisteredRows()
        {
            var list = new List<WatchedScalarRow>();
            if (!_initialized || _telemetry == null || _scalar == null)
                return list;

            foreach (var key in _telemetry.EnumerateKeys())
            {
                if (key.Id == 0 && string.IsNullOrEmpty(key.Name))
                    continue;

                list.Add(new WatchedScalarRow(key, _scalar.LocalGet(key)));
            }

            return list;
        }

        [FoldoutGroup("Watch"), ShowInInspector, ReadOnly]
        [LabelText("Registered Modifiers")]
        public List<WatchedScalarModsRow> WatchedModifiers =>
            !_initialized
                ? new List<WatchedScalarModsRow>()
                : BuildRegisteredModRows();

        List<WatchedScalarModsRow> BuildRegisteredModRows()
        {
            var list = new List<WatchedScalarModsRow>();
            if (!_initialized || _telemetry == null)
                return list;

            foreach (var key in _telemetry.EnumerateKeys())
            {
                if (key.Id == 0 && string.IsNullOrEmpty(key.Name))
                    continue;

                var mods = _telemetry.Enumerate(key);
                // Convert ScalarSnapshot to a readable wrapper for the editor
                var pretty = new List<PrettyScalarSnapshot>();
                foreach (var s in mods)
                    pretty.Add(new PrettyScalarSnapshot(s));

                list.Add(new WatchedScalarModsRow(key, pretty));
            }

            return list;
        }

        public readonly struct WatchedScalarRow
        {
            [ShowInInspector, ReadOnly]
            [LabelText("Key")]
            public readonly string KeyName;

            [ShowInInspector, ReadOnly]
            [LabelText("Value")]
            public readonly float Value;

            public WatchedScalarRow(ScalarKey key, float value)
            {
                KeyName = key.FormatLabel();
                Value = value;
            }
        }

        public readonly struct WatchedScalarModsRow
        {
            [ShowInInspector, ReadOnly]
            [LabelText("Key")]
            public readonly string KeyName;

            [ShowInInspector, ReadOnly]
            [LabelText("Mods")]
            public readonly List<PrettyScalarSnapshot> Mods;

            public WatchedScalarModsRow(ScalarKey key, List<PrettyScalarSnapshot> mods)
            {
                KeyName = key.FormatLabel();
                Mods = mods;
            }
        }

        // Editor-only helper wrapper to present ScalarSnapshot in a readable way
        public readonly struct PrettyScalarSnapshot
        {
            [ShowInInspector, ReadOnly, LabelText("Lane")]
            public readonly LayeredNumericLaneKind Lane;

            [ShowInInspector, ReadOnly, LabelText("Kind")]
            public readonly ScalarModKind Kind;

            [ShowInInspector, ReadOnly, LabelText("Revision")]
            public readonly int Revision;

            [ShowInInspector, ReadOnly, LabelText("Value")]
            public readonly float Value;

            [ShowInInspector, ReadOnly, LabelText("Remain")]
            public readonly float Remain;

            [ShowInInspector, ReadOnly, LabelText("Source")]
            public readonly string Source;

            [ShowInInspector, ReadOnly, LabelText("Tag")]
            public readonly string Tag;

            [ShowInInspector, ReadOnly, LabelText("Id")]
            public readonly Guid Id;

            [ShowInInspector, ReadOnly, LabelText("Summary")]
            public readonly string Summary;

            public PrettyScalarSnapshot(ScalarSnapshot s)
            {
                Lane = s.Lane;
                Kind = s.Kind;
                Revision = s.Revision;
                Value = s.Value;
                Remain = s.Remain;
                Source = s.Source?.ToString() ?? "(none)";
                Tag = s.Tag ?? string.Empty;
                Id = s.Id;

                var laneLabel = Lane switch
                {
                    LayeredNumericLaneKind.Base => "Base",
                    LayeredNumericLaneKind.PrefixMul => "PrefixMul",
                    LayeredNumericLaneKind.Add => "Add",
                    LayeredNumericLaneKind.SuffixMul => "SuffixMul",
                    LayeredNumericLaneKind.FinalClamp => "FinalClamp",
                    LayeredNumericLaneKind.Effective => "Effective",
                    _ => "Unknown",
                };

                var kindLabel = Kind switch
                {
                    ScalarModKind.Add => "Add",
                    ScalarModKind.Mul => "Mul",
                    ScalarModKind.Clamp => "Clamp",
                    _ => "Unknown",
                };

                var valueText = Kind == ScalarModKind.Mul ? $"x{Value:0.##}" : $"{(Value >= 0 ? "+" : string.Empty)}{Value:0.##}";
                var tagText = string.IsNullOrEmpty(Tag) ? string.Empty : $" (tag:{Tag})";
                var srcText = string.IsNullOrEmpty(Source) ? string.Empty : $" src={Source}";
                var remainText = Remain < 0 ? string.Empty : $" ﾂｷ remain={Remain:0.##}s";
                var revisionText = $" rev={Revision}";

                Summary = $"[{laneLabel}/{kindLabel}] {valueText}{srcText}{tagText}{remainText}{revisionText}";
            }
        }
    }
}

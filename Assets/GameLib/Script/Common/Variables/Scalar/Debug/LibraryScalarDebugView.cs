// Assets/Game/Script/Projects/Scalar/LibraryScalarDebugView.cs
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Scalar
{
    [Serializable]
    public sealed class LibraryScalarDebugView
    {
        [NonSerialized] IBaseScalarService _scalar;
        [NonSerialized] IScalarTelemetry _telemetry;
        [NonSerialized] IScalarBindingManager _bindingManager;
        [NonSerialized] IScalarBindingTelemetry _bindingTelemetry;

        bool _initialized;

        // ===== 險ｭ螳夲ｼ夂屮隕悶＠縺溘＞繧ｭ繝ｼ荳隕ｧ =====

        [LabelText("Watch Keys")]
        [Tooltip("Inspector setting.")]
        public List<ScalarKey> watchKeys = new List<ScalarKey>();

        // ===== 蜊倡匱邱ｨ髮・畑繧ｨ繝・ぅ繧ｿ =====

        [FoldoutGroup("Edit"), LabelText("Key Name")]
        public string editKeyName;

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
                if (!_initialized || string.IsNullOrWhiteSpace(editKeyName))
                    return 0f;
                var key = new ScalarKey(editKeyName.Trim());
                return _scalar.LocalGet(key);
            }
        }

        public void Initialize(
            IBaseScalarService scalar,
            IScalarTelemetry telemetry,
            IScalarBindingManager bindingManager,
            IScalarBindingTelemetry bindingTelemetry)
        {
            _scalar = scalar;
            _telemetry = telemetry;
            _bindingManager = bindingManager;
            _bindingTelemetry = bindingTelemetry;
            _initialized = scalar != null && telemetry != null;
        }

        // ===== Edit 謫堺ｽ・=====

        [FoldoutGroup("Edit")]
        [Button(ButtonSizes.Medium)]
        public void ApplySetLocalBase()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKeyName))
                return;

            var key = new ScalarKey(editKeyName.Trim());
            _scalar.SetLocalBase(key, editLocalBase);
        }

        [FoldoutGroup("Edit")]
        [Button(ButtonSizes.Medium)]
        public void ApplyAddDelta()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKeyName))
                return;

            var key = new ScalarKey(editKeyName.Trim());
            _scalar.LocalAdd(key, layer: null, delta: editAddDelta, duration: -1f, source: this, tag: "Debug.Add");
        }

        [FoldoutGroup("Edit")]
        [Button(ButtonSizes.Medium)]
        public void ApplyMul()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(editKeyName))
                return;

            var key = new ScalarKey(editKeyName.Trim());
            _scalar.LocalMul(key, layer: null, factor: editMulFactor, phase: ScalarMulPhase.PostAdd, duration: -1f, source: this, tag: "Debug.Mul");
        }

        [FoldoutGroup("Edit")]
        [Button(ButtonSizes.Medium)]
        public void ClearDebugMods()
        {
            if (!_initialized)
                return;

            if (!string.IsNullOrWhiteSpace(editKeyName))
            {
                var key = new ScalarKey(editKeyName.Trim());
                _scalar.ClearAll(key);
            }
            else
            {
                _scalar.ClearAll();
            }
        }

        // ===== WatchKeys 縺ｮ迴ｾ蝨ｨ蛟､・貴ods 陦ｨ遉ｺ =====

        [FoldoutGroup("Watch"), ShowInInspector, ReadOnly]
        [LabelText("Watched Scalars")]
        public IEnumerable<WatchedScalarRow> WatchedScalars
        {
            get
            {
                if (!_initialized || watchKeys == null)
                    yield break;

                for (int i = 0; i < watchKeys.Count; i++)
                {
                    var key = watchKeys[i];
                    if (key.Id == 0 && string.IsNullOrEmpty(key.Name))
                        continue;

                    float value = _scalar.LocalGet(key);
                    yield return new WatchedScalarRow(key, value);
                }
            }
        }

        [FoldoutGroup("Watch"), ShowInInspector, ReadOnly]
        [LabelText("Watched Modifiers")]
        public IEnumerable<WatchedScalarModsRow> WatchedModifiers
        {
            get
            {
                if (!_initialized || watchKeys == null)
                    yield break;

                for (int i = 0; i < watchKeys.Count; i++)
                {
                    var key = watchKeys[i];
                    if (key.Id == 0 && string.IsNullOrEmpty(key.Name))
                        continue;

                    var mods = _telemetry?.Enumerate(key) ?? Array.Empty<ScalarSnapshot>();

                    var pretty = new List<PrettyScalarSnapshot>();
                    foreach (var s in mods)
                        pretty.Add(new PrettyScalarSnapshot(s));

                    yield return new WatchedScalarModsRow(key, pretty);
                }
            }
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

        // Editor-friendly wrapper for snapshots
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

        // ===== Binding 荳隕ｧ陦ｨ遉ｺ =====

        [FoldoutGroup("Bindings"), ShowInInspector, ReadOnly]
        [LabelText("Active Bindings")]
        public IEnumerable<ScalarBindingDebugInfo> ActiveBindings
        {
            get
            {
                if (!_initialized || _bindingTelemetry == null)
                    return Array.Empty<ScalarBindingDebugInfo>();

                return _bindingTelemetry.GetBindings();
            }
        }
    }
}

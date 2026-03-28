#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Channel
{
    [Serializable]
    public sealed class Light2DChannelDef : ChannelDefBase
    {
        [BoxGroup("Target")]
        [LabelText("Target Light")]
        [SerializeField]
        Light2D? _targetLight;

        [BoxGroup("Preset")]
        [LabelText("Source Preset")]
        [SerializeField]
        DynamicValue<Light2DPreset> _sourcePreset =
            DynamicValue<Light2DPreset>.FromSource(
                new ManagedRefLiteralSource<Light2DPreset>(new Light2DPreset()));

        [BoxGroup("Global")]
        [LabelText("Global Link Key")]
        [Tooltip("一致した ancestor の primary global provider から intensity を継承します。")]
        [SerializeField]
        string _globalLinkKey = string.Empty;

        [BoxGroup("Lifecycle")]
        [LabelText("Apply On Acquire")]
        [SerializeField]
        bool _applyOnAcquire = true;

        [BoxGroup("Lifecycle")]
        [LabelText("Restore On Release")]
        [SerializeField]
        bool _restoreOnRelease = true;

        [BoxGroup("Runtime")]
        [LabelText("Allow Runtime Light Type Change")]
        [SerializeField]
        bool _allowRuntimeLightTypeChange;

        [BoxGroup("Runtime")]
        [LabelText("Debug Log")]
        [SerializeField]
        bool _debugLog;

        public Light2D? TargetLight => _targetLight;
        public DynamicValue<Light2DPreset> SourcePreset => _sourcePreset;
        public string GlobalLinkKey => string.IsNullOrWhiteSpace(_globalLinkKey) ? string.Empty : _globalLinkKey.Trim();
        public bool ApplyOnAcquire => _applyOnAcquire;
        public bool RestoreOnRelease => _restoreOnRelease;
        public bool AllowRuntimeLightTypeChange => _allowRuntimeLightTypeChange;
        public bool DebugLog => _debugLog;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (_targetLight == null && owner != null)
                _targetLight = owner.GetComponentInChildren<Light2D>(true);
        }
    }
}

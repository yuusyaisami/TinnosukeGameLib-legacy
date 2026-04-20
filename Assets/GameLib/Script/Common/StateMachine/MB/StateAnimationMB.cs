// Game.StateMachine.StateAnimationMB.cs

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StateMachine
{
    /// <summary>
    /// StateAnimationController 縺ｮ FeatureInstaller + Debug Viewer縲・
    /// LifetimeScope 驟堺ｸ九↓驟咲ｽｮ縺励※菴ｿ逕ｨ縺吶ｋ縲・
    /// </summary>
    [RequireComponent(typeof(StateMachineMB))]
    public sealed class StateAnimationMB : MonoBehaviour, IFeatureInstaller
    {
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武
        //  Inspector Fields
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武

        [Header("Profile")]
        [Tooltip("Inspector setting.")]
        [SerializeField, InlineProperty, HideLabel]
        DynamicValue<StateAnimationPreset> _preset;

        [SerializeField, HideInInspector]
        StateAnimationProfileSO _profile;

        [BoxGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        StateAnimationDebugViewer debugViewer = new();

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武
        //  Runtime References
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武

        StateAnimationController _controllerRef;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武
        //  IFeatureInstaller
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode baseLTS)
        {
            EnsurePresetMigrated();
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, baseLTS);
            var preset = _preset.GetOrDefault(dynamicContext, null);

            // StateAnimationController 逋ｻ骭ｲ
            builder.Register<StateAnimationController>(RuntimeLifetime.Singleton)
                .As<IScopeTickHandler>()
                .As<IStateAnimationTelemetry>()
                .AsSelf()
                .WithParameter("ownerScope", baseLTS)
                .WithParameter("profile", preset);

            // Debug View 逕ｨ縺ｫ蜿ら・繧貞叙蠕・
            builder.RegisterBuildCallback(container =>
            {
                _controllerRef = container.Resolve<StateAnimationController>();
                if (debugViewer != null && container.TryResolve<IStateAnimationTelemetry>(out var telemetry) && telemetry != null)
                    debugViewer.Bind(telemetry);
            });
        }

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武
        //  Public API - Profile Hot-Swap
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武

        /// <summary>
        /// Profile 繧貞虚逧・↓蟾ｮ縺玲崛縺医ｋ縲・
        /// </summary>
        /// <param name="profile">譁ｰ縺励＞繝励Ο繝輔ぃ繧､繝ｫ</param>
        /// <param name="restartImmediately">true 縺ｮ蝣ｴ蜷医∫樟蝨ｨ縺ｮ StateMachine 迥ｶ諷九↓蟇ｾ縺励※蜊ｳ蠎ｧ縺ｫ蜀咲函繧帝幕蟋・/param>
        public void SetProfile(StateAnimationProfileSO profile, bool restartImmediately = true)
        {
            _profile = profile;
            EnsurePresetMigrated();
            SetProfile(profile != null ? profile.Preset : null, restartImmediately);
        }

        public void SetProfile(StateAnimationPreset profile, bool restartImmediately = true)
        {
            _preset = profile != null
                ? DynamicValue<StateAnimationPreset>.FromSource(new LiteralStateAnimationPresetSource(profile))
                : default;

            if (_controllerRef != null)
            {
                _controllerRef.SetProfile(profile, restartImmediately);
            }
        }

        void OnValidate()
        {
            EnsurePresetMigrated();
        }

        void EnsurePresetMigrated()
        {
            if (_preset.HasSource || _profile == null)
                return;

            _preset = DynamicValue<StateAnimationPreset>.FromSource(AssetStateAnimationPresetSource.FromAsset(_profile));
        }
    }
}

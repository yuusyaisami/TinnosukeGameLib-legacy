// Game.StateMachine.StateAnimationMB.cs

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StateMachine
{
    /// <summary>
    /// StateAnimationController の FeatureInstaller + Debug Viewer。
    /// LifetimeScope 配下に配置して使用する。
    /// </summary>
    [RequireComponent(typeof(StateMachineMB))]
    public sealed class StateAnimationMB : MonoBehaviour, IFeatureInstaller
    {
        // ════════════════════════════════════════════════════════════════
        //  Inspector Fields
        // ════════════════════════════════════════════════════════════════

        [Header("Profile")]
        [Tooltip("StateAnimationPreset。Inline か Asset のどちらでも指定できます。")]
        [SerializeField, InlineProperty, HideLabel]
        DynamicValue<StateAnimationPreset> _preset;

        [SerializeField, HideInInspector]
        StateAnimationProfileSO _profile;

        [BoxGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        StateAnimationDebugViewer debugViewer = new();

        // ════════════════════════════════════════════════════════════════
        //  Runtime References
        // ════════════════════════════════════════════════════════════════

        StateAnimationController _controllerRef;

        // ════════════════════════════════════════════════════════════════
        //  IFeatureInstaller
        // ════════════════════════════════════════════════════════════════

        public void InstallFeature(IContainerBuilder builder, IScopeNode baseLTS)
        {
            EnsurePresetMigrated();
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, baseLTS);
            var preset = _preset.GetOrDefault(dynamicContext, null);

            // StateAnimationController 登録
            builder.Register<StateAnimationController>(Lifetime.Singleton)
                .As<ITickable>()
                .As<IStateAnimationTelemetry>()
                .AsSelf()
                .WithParameter("ownerScope", baseLTS)
                .WithParameter("profile", preset);

            // Debug View 用に参照を取得
            builder.RegisterBuildCallback(container =>
            {
                _controllerRef = container.Resolve<StateAnimationController>();
                if (debugViewer != null && container.TryResolve<IStateAnimationTelemetry>(out var telemetry) && telemetry != null)
                    debugViewer.Bind(telemetry);
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  Public API - Profile Hot-Swap
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Profile を動的に差し替える。
        /// </summary>
        /// <param name="profile">新しいプロファイル</param>
        /// <param name="restartImmediately">true の場合、現在の StateMachine 状態に対して即座に再生を開始</param>
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

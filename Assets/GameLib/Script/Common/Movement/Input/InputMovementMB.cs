#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands;
using Game.Common;
using Game.DI;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Movement
{
    /// <summary>
    /// Feature installer that registers <see cref="InputMovementService"/>.
    /// v0.2: Homing/Motion モジュールに対応。
    /// </summary>
    public sealed class InputMovementMB : MonoBehaviour, IFeatureInstaller
    {
        // ================================================================
        // General Settings
        // ================================================================

        [FoldoutGroup("General")]
        [SerializeField]
        [Tooltip("Movement channel tag used for user input")]
        string _channelTag = InputMovementOptions.DefaultChannelKey;

        [FoldoutGroup("General")]
        [SerializeField]
        [Tooltip("移動方向に応じて、DirectionHubに打つかどうか")]
        bool _outputDirection = false;

        [FoldoutGroup("General")]
        [SerializeField, ShowIf("_outputDirection")]
        [Tooltip("移動方向に応じて、Directionhub に打つオプションキー")]
        string _directionKey = InputMovementOptions.DefaultDirectionKey;

        [FoldoutGroup("General")]
        [SerializeField]
        [Tooltip("Movement チャネルの速度を滑らかに遷移させるラムダ（0 なら即時切り替え）")]
        float _smoothingLambda = 0f;

        // ================================================================
        // Acceleration
        // ================================================================

        [FoldoutGroup("Acceleration")]
        [SerializeField]
        [Tooltip("加速/減速を有効化")]
        bool _enableAcceleration = false;

        [FoldoutGroup("Acceleration")]
        [SerializeField, ShowIf("_enableAcceleration"), Min(0f)]
        [Tooltip("加速レート")]
        float _accel = 0f;

        [FoldoutGroup("Acceleration")]
        [SerializeField, ShowIf("_enableAcceleration"), Min(0f)]
        [Tooltip("減速レート")]
        float _decel = 0f;

        // ================================================================
        // Speed-Based Commands
        // ================================================================

        [FoldoutGroup("Speed Commands")]
        [SerializeField]
        [Tooltip("移動速度に応じて、打つコマンドを変えられます")]
        bool _useSpeedBasedCommands = false;

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [Tooltip("MonitorChannelHub に登録するルール群。速度キーを参照する式にすること")]
        MonitorRule[] _monitorRules = Array.Empty<MonitorRule>();

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [LabelText("Shared Expression Variables")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        List<ExpressionVariable> _sharedExpressionVariables = new();

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [Tooltip("MonitorChannelHub の評価モード。EventDriven 推奨")]
        MonitorEvaluationMode _evaluationMode = MonitorEvaluationMode.EventDriven;


        // ================================================================
        // Homing Settings
        // ================================================================

        [FoldoutGroup("Homing")]
        [SerializeField]
        [Tooltip("ホーミング（ターゲット追尾）機能を有効化")]
        bool _enableHoming = false;

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("ターゲット取得に使用するチャンネルタグ")]
        string _targetChannelTag = "default";

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("ホーミングのブレンドパラメータ")]
        HomingBlendParams _homingBlendParams = HomingBlendParams.Default;

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("初期状態でホーミングを有効化するか")]
        bool _homingEnabledByDefault = true;

        // ================================================================
        // Motion Settings
        // ================================================================

        [FoldoutGroup("Motion")]
        [SerializeField]
        [Tooltip("モーション（進行方向変調）機能を有効化")]
        bool _enableMotion = false;

        [FoldoutGroup("Motion")]
        [SerializeField, ShowIf("_enableMotion")]
        [Tooltip("初期モーション（null の場合は空でスタート）")]
        DynamicValue<MotionPreset> _initialMotion = new();

        [FoldoutGroup("Debug")]
        [ShowInInspector, ReadOnly, InlineProperty, HideLabel]
        InputMovementDebugView _debugView = new InputMovementDebugView();

        // ================================================================
        // IFeatureInstaller
        // ================================================================

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (_debugView == null)
                _debugView = new InputMovementDebugView();

            // Options
            builder.RegisterInstance(new InputMovementOptions
            {
                ChannelKey = _channelTag,
                OutputDirection = _outputDirection,
                DirectionKey = _directionKey,
                UseSpeedBasedCommands = _useSpeedBasedCommands,
                MonitorRules = _monitorRules,
                SharedExpressionVariables = _sharedExpressionVariables,
                MonitorEvaluationMode = _evaluationMode,
                SmoothingLambda = _smoothingLambda,
                Acceleration = new InputAccelerationSettings
                {
                    Enabled = _enableAcceleration,
                    Accel = _accel,
                    Decel = _decel,
                },
            });

            // Homing
            if (_enableHoming)
            {
                var homingOptions = new HomingMovementOptions(
                    targetChannelTag: _targetChannelTag,
                    blendParams: _homingBlendParams,
                    enabledByDefault: _homingEnabledByDefault
                );
                builder.RegisterInstance(homingOptions);
                builder.Register<HomingMovementService>(Lifetime.Singleton)
                    .WithParameter(typeof(Game.Targeting.ITargetChannelHub), _ => (object?)null)
                    .As<IHomingMovement>()
                    .As<IHomingMovementConfigurable>()
                    .As<IEnabledService>()
                    .As<IResettableService>();
            }

            // Motion
            if (_enableMotion)
            {
                var motionContext = new SimpleDynamicContext(NullVarStore.Instance, scope);
                _initialMotion.TryGet(motionContext, out var initialMotion);

                var motionOptions = new MotionMovementOptions(
                    initialMotion: initialMotion
                );
                builder.RegisterInstance(motionOptions);
                builder.Register<MotionMovementService>(Lifetime.Singleton)
                    .As<IMotionMovement>()
                    .As<IEnabledService>()
                    .As<IResettableService>();
            }

            // InputMovementService
            builder.Register<InputMovementService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>()
                .As<IInputMovementService>()
                .As<IInputMovementTelemetry>()
                .As<IEnabledService>()
                .As<ITickable>()
                .WithParameter(scope);

            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (_debugView != null && container.TryResolve<IInputMovementTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });

            // NOTE:
            // UserInputAdapter is now installed via UserInputAdapterMB (MB/Service pattern)
            // and is activated via IScopeAcquireHandler/IScopeReleaseHandler.
        }
    }
}

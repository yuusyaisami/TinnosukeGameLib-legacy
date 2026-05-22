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
    /// v0.2: Homing/Motion 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｫ蟇ｾ蠢懊・
    /// </summary>
    public sealed class InputMovementMB : MonoBehaviour, IScopeInstaller
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
        [Tooltip("遘ｻ蜍墓婿蜷代↓蠢懊§縺ｦ縲．irectionHub縺ｫ謇薙▽縺九←縺・°")]
        bool _outputDirection = false;

        [FoldoutGroup("General")]
        [SerializeField, ShowIf("_outputDirection")]
        [Tooltip("遘ｻ蜍墓婿蜷代↓蠢懊§縺ｦ縲．irectionhub 縺ｫ謇薙▽繧ｪ繝励す繝ｧ繝ｳ繧ｭ繝ｼ")]
        string _directionKey = InputMovementOptions.DefaultDirectionKey;

        [FoldoutGroup("General")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        float _smoothingLambda = 0f;

        // ================================================================
        // Acceleration
        // ================================================================

        [FoldoutGroup("Acceleration")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _enableAcceleration = false;

        [FoldoutGroup("Acceleration")]
        [SerializeField, ShowIf("_enableAcceleration"), Min(0f)]
        [Tooltip("Inspector setting.")]
        float _accel = 0f;

        [FoldoutGroup("Acceleration")]
        [SerializeField, ShowIf("_enableAcceleration"), Min(0f)]
        [Tooltip("Inspector setting.")]
        float _decel = 0f;

        // ================================================================
        // Speed-Based Commands
        // ================================================================

        [FoldoutGroup("Speed Commands")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _useSpeedBasedCommands = false;

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [Tooltip("MonitorChannelHub 縺ｫ逋ｻ骭ｲ縺吶ｋ繝ｫ繝ｼ繝ｫ鄒､縲る溷ｺｦ繧ｭ繝ｼ繧貞盾辣ｧ縺吶ｋ蠑上↓縺吶ｋ縺薙→")]
        MonitorRule[] _monitorRules = Array.Empty<MonitorRule>();

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [LabelText("Shared Expression Variables")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        List<ExpressionVariable> _sharedExpressionVariables = new();

        [FoldoutGroup("Speed Commands")]
        [SerializeField, ShowIf("_useSpeedBasedCommands")]
        [Tooltip("MonitorChannelHub 縺ｮ隧穂ｾ｡繝｢繝ｼ繝峨・ventDriven 謗ｨ螂ｨ")]
        MonitorEvaluationMode _evaluationMode = MonitorEvaluationMode.EventDriven;


        // ================================================================
        // Homing Settings
        // ================================================================

        [FoldoutGroup("Homing")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _enableHoming = false;

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("繧ｿ繝ｼ繧ｲ繝・ヨ蜿門ｾ励↓菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν繧ｿ繧ｰ")]
        string _targetChannelTag = "default";

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("繝帙・繝溘Φ繧ｰ縺ｮ繝悶Ξ繝ｳ繝峨ヱ繝ｩ繝｡繝ｼ繧ｿ")]
        HomingBlendParams _homingBlendParams = HomingBlendParams.Default;

        [FoldoutGroup("Homing")]
        [SerializeField, ShowIf("_enableHoming")]
        [Tooltip("蛻晄悄迥ｶ諷九〒繝帙・繝溘Φ繧ｰ繧呈怏蜉ｹ蛹悶☆繧九°")]
        bool _homingEnabledByDefault = true;

        // ================================================================
        // Motion Settings
        // ================================================================

        [FoldoutGroup("Motion")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _enableMotion = false;

        [FoldoutGroup("Motion")]
        [SerializeField, ShowIf("_enableMotion")]
        [Tooltip("Inspector setting.")]
        DynamicValue<MotionPreset> _initialMotion = new();

        [FoldoutGroup("Debug")]
        [ShowInInspector, ReadOnly, InlineProperty, HideLabel]
        InputMovementDebugView _debugView = new InputMovementDebugView();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_monitorRules == null || _monitorRules.Length == 0)
                return;

            for (int i = 0; i < _monitorRules.Length; i++)
            {
                var rule = _monitorRules[i];
                rule.EnsureDefaults();
                _monitorRules[i] = rule;
            }
        }
#endif

        // ================================================================
        // IScopeInstaller
        // ================================================================

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
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
                builder.Register<HomingMovementService>(RuntimeLifetime.Singleton)
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
                builder.Register<MotionMovementService>(RuntimeLifetime.Singleton)
                    .As<IMotionMovement>()
                    .As<IEnabledService>()
                    .As<IResettableService>();
            }

            // InputMovementService
            builder.Register<InputMovementService>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>()
                .As<IInputMovementService>()
                .As<IInputMovementTelemetry>()
                .As<IEnabledService>()
                .As<IScopeTickHandler>()
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


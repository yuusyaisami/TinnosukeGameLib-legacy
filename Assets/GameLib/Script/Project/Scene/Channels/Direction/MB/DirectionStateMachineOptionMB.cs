using Game.StateMachine.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Direction
{
    [DisallowMultipleComponent]
    public sealed class DirectionStateMachineOptionMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("Thresholds")]
        [SerializeField, Min(0f), LabelText("Activation Threshold")]
        float activationThreshold = 0.55f;

        [BoxGroup("Thresholds")]
        [SerializeField, Min(0f), LabelText("Hold Threshold")]
        float holdThreshold = 0.35f;

        [BoxGroup("Thresholds")]
        [SerializeField, Min(0f), LabelText("Zero Hold Threshold")]
        float zeroHoldThreshold = 0.1f;

        [BoxGroup("Behavior")]
        [SerializeField, LabelText("Zero Speed Policy")]
        ZeroSpeedOptionPolicy zeroSpeedPolicy = ZeroSpeedOptionPolicy.KeepLast;

        [BoxGroup("Behavior")]
        [SerializeField, LabelText("Diagonal Policy")]
        DiagonalOptionPolicy diagonalOptionPolicy = DiagonalOptionPolicy.SinglePreferPrevious;

        [BoxGroup("Output")]
        [SerializeField, LabelText("Output To Global")]
        bool outputToGlobal = false;

        [BoxGroup("Cardinal Angle Mapping")]
        [SerializeField, LabelText("Use Custom Cardinal Angles")]
        bool useCustomCardinalAngles = false;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Up Center (deg)"), Range(-180f, 180f)]
        float upCenterDeg = 90f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Up Half Range (deg)"), Range(0f, 180f)]
        float upHalfRangeDeg = 45f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Left Center (deg)"), Range(-180f, 180f)]
        float leftCenterDeg = 180f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Left Half Range (deg)"), Range(0f, 180f)]
        float leftHalfRangeDeg = 45f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Right Center (deg)"), Range(-180f, 180f)]
        float rightCenterDeg = 0f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Right Half Range (deg)"), Range(0f, 180f)]
        float rightHalfRangeDeg = 45f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Down Center (deg)"), Range(-180f, 180f)]
        float downCenterDeg = -90f;

        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [SerializeField, LabelText("Down Half Range (deg)"), Range(0f, 180f)]
        float downHalfRangeDeg = 45f;

#if UNITY_EDITOR
        [BoxGroup("Cardinal Angle Mapping"), ShowIf(nameof(useCustomCardinalAngles))]
        [Button("Preview Angle Ranges")]
        void OpenAnglePreview()
        {
            System.Type type = null;
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType("Game.Direction.Editor.DirectionStateMachineAnglePreviewWindow", throwOnError: false);
                if (type != null)
                    break;
            }

            if (type == null)
            {
                Debug.LogWarning("[DirectionStateMachineOptionMB] DirectionStateMachineAnglePreviewWindow was not found.");
                return;
            }

            var method = type.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { this });
        }
#endif

        internal DirectionCardinalAngleConfig BuildCardinalAngleConfig()
        {
            return new DirectionCardinalAngleConfig(
                enabled: useCustomCardinalAngles,
                upCenterDeg: upCenterDeg,
                upHalfRangeDeg: upHalfRangeDeg,
                leftCenterDeg: leftCenterDeg,
                leftHalfRangeDeg: leftHalfRangeDeg,
                rightCenterDeg: rightCenterDeg,
                rightHalfRangeDeg: rightHalfRangeDeg,
                downCenterDeg: downCenterDeg,
                downHalfRangeDeg: downHalfRangeDeg);
        }

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var config = new DirectionStateMachineOptionConfig(
                activationThreshold: activationThreshold,
                holdThreshold: holdThreshold,
                zeroHoldThreshold: zeroHoldThreshold,
                zeroSpeedPolicy: zeroSpeedPolicy,
                diagonalPolicy: diagonalOptionPolicy,
                outputToGlobal: outputToGlobal,
                cardinalAngleConfig: BuildCardinalAngleConfig());

            builder.RegisterInstance(config);
            builder.Register<DirectionStateMachineOptionService>(RuntimeLifetime.Singleton)
                .AsSelf()
                .As<IScopeTickHandler>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}


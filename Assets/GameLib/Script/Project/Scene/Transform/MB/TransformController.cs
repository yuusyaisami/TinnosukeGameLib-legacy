#nullable enable
// Game.Transform.TransformController.cs
//
// Movement / Rotation チャネルを統合し、Transform 等へ出力する制御。
// MB は設定と DI 登録のみを担当し、処理は Service 側で行う。

using System;
using Game;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    /// <summary>
    /// Transform の出力ターゲット。
    /// </summary>
    public enum TransformOutputTarget
    {
        None,
        Transform,
        RectTransform,
        BulkTransform,
        Rigidbody2D,
        CharacterController,
    }

    /// <summary>
    /// Movement と Rotation のチャネルシステムを統合管理する MB。
    /// 設定値を保持し、Service を登録する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransformController : MonoBehaviour, IFeatureInstaller
    {
        // ================================================================
        // Inspector 設定
        // ================================================================

        [BoxGroup("Output")]
        [LabelText("Output Target")]
        [SerializeField] TransformOutputTarget outputTarget = TransformOutputTarget.Transform;

        [BoxGroup("Output")]
        [LabelText("Target Transform")]
        [ShowIf("@outputTarget == TransformOutputTarget.Transform || outputTarget == TransformOutputTarget.BulkTransform")]
        [SerializeField] UnityEngine.Transform? targetTransform;

        [BoxGroup("Output")]
        [LabelText("Target RectTransform")]
        [ShowIf("@outputTarget == TransformOutputTarget.RectTransform")]
        [SerializeField] RectTransform? targetRectTransform;

        [BoxGroup("Output")]
        [LabelText("Target Rigidbody2D")]
        [ShowIf("@outputTarget == TransformOutputTarget.Rigidbody2D")]
        [SerializeField] Rigidbody2D? targetRigidbody;

        [BoxGroup("Output")]
        [LabelText("Rigidbody2D Velocity Mode")]
        [ShowIf("@outputTarget == TransformOutputTarget.Rigidbody2D")]
        [SerializeField] Rigidbody2DVelocityApplyMode rigidbody2DVelocityMode = Rigidbody2DVelocityApplyMode.Override;

        [BoxGroup("Output/Additive Control")]
        [ShowIf("@outputTarget == TransformOutputTarget.Rigidbody2D && rigidbody2DVelocityMode == Rigidbody2DVelocityApplyMode.Additive")]
        [SerializeField, InlineProperty]
        Rigidbody2DAdditiveControlSettings rigidbody2DAdditiveControl = Rigidbody2DAdditiveControlSettings.Default;

        [BoxGroup("Output/Gravity Clamp")]
        [ShowIf("@outputTarget == TransformOutputTarget.Rigidbody2D")]
        [SerializeField, InlineProperty]
        Rigidbody2DGravityClampSettings rigidbody2DGravityClamp = Rigidbody2DGravityClampSettings.Default;

        [BoxGroup("Output")]
        [LabelText("Target CharacterController")]
        [ShowIf("@outputTarget == TransformOutputTarget.CharacterController")]
        [SerializeField] CharacterController? targetCharacterController;

        [BoxGroup("Features")]
        [LabelText("Enable Movement")]
        [SerializeField] bool enableMovement = true;

        [BoxGroup("Features")]
        [LabelText("Enable Rotation")]
        [SerializeField] bool enableRotation = false;

        [BoxGroup("Debug")]
        [SerializeField]
        TransformControllerDebugView _debugView = new TransformControllerDebugView();

        // ================================================================
        // IFeatureInstaller
        // ================================================================

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var config = new TransformControllerConfig
            {
                OutputTarget = outputTarget,
                TargetTransform = targetTransform,
                TargetRectTransform = targetRectTransform,
                TargetRigidbody2D = targetRigidbody,
                TargetCharacterController = targetCharacterController,
                Rigidbody2DVelocityMode = rigidbody2DVelocityMode,
                Rigidbody2DAdditiveControl = rigidbody2DAdditiveControl,
                Rigidbody2DGravityClamp = rigidbody2DGravityClamp,
                EnableMovement = enableMovement,
                EnableRotation = enableRotation,
            };

            builder.RegisterInstance(config);

            builder.Register<TransformControllerService>(Lifetime.Singleton)
                .As<ITickable>()
                .As<ITransformTeleportService>()
                .As<ITransformControllerPoseReader>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>()
                .As<ITransformControllerTelemetry>()
                .WithParameter(transform);

            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (_debugView != null && container.TryResolve<ITransformControllerTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });
        }
    }
}

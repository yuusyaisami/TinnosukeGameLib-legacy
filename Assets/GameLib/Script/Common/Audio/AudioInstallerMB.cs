using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game;
using Game.Times;
using Game.Commands.VNext;
using Sirenix.OdinInspector;

namespace Game.Audio
{
    // ================================================================
    // AudioInstallerMB - AudioService の DI 登録
    // ================================================================
    //
    // ## 概要
    //
    // IFeatureInstaller として AudioService を DI コンテナに登録する。
    // Inspector でバス設定とボリュームプロバイダを設定可能。
    //
    // ## 配置
    //
    // 通常は GameLifetimeScope に配置。
    //
    // ================================================================

    [DisallowMultipleComponent]
    public sealed class AudioInstallerMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector 設定
        // ----------------------------------------------------------------

        [BoxGroup("Buses")]
        [LabelText("Bus Configs")]
        [Tooltip("バス設定。未設定のバスはデフォルト値で生成される。")]
        [SerializeField]
        List<AudioBusConfig> busConfigs = new();

        [BoxGroup("Volume")]
        [LabelText("Use Constant Volume Provider")]
        [Tooltip("Scalar 連携を使用しない場合は true")]
        [SerializeField]
        bool useConstantVolumeProvider = true;

        [BoxGroup("Volume")]
        [EnableIf(nameof(useConstantVolumeProvider))]
        [LabelText("Constant Master")]
        [SerializeField, Range(0f, 1f)]
        float constantMaster = 1f;

        [BoxGroup("Volume")]
        [EnableIf(nameof(useConstantVolumeProvider))]
        [LabelText("Constant Bus")]
        [SerializeField, Range(0f, 1f)]
        float constantBus = 1f;

        [BoxGroup("Listener")]
        [LabelText("Use Custom Listener Target")]
        [Tooltip("OFF の場合はシーン上の有効な AudioListener を使用する。")]
        [SerializeField]
        bool useCustomListenerTarget = false;

        [BoxGroup("Listener")]
        [EnableIf(nameof(useCustomListenerTarget))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(customListenerTarget)")]
        [Tooltip("距離減衰の基準にする対象。Player などへ差し替え可能。")]
        [SerializeField]
        ActorSource customListenerTarget = new() { Kind = ActorSourceKind.Player };

        [BoxGroup("Listener")]
        [LabelText("Use 2D Distance (XY)")]
        [Tooltip("ON の場合は XY 距離のみで減衰する。3D 運用時は OFF にする。")]
        [SerializeField]
        bool use2DDistanceAttenuation = true;

        // ----------------------------------------------------------------
        // IFeatureInstaller
        // ----------------------------------------------------------------

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // ボリュームプロバイダの登録
            if (useConstantVolumeProvider)
            {
                builder.RegisterInstance<IAudioVolumeProvider>(
                    new ConstantAudioVolumeProvider(constantMaster, constantBus));
            }
            else
            {
                builder.Register<ScalarAudioVolumeProvider>(Lifetime.Singleton)
                       .As<IAudioVolumeProvider>();
            }
            // Scalar 連携を使う場合は ScalarAudioVolumeProvider を別途登録

            // AudioService の登録
            builder.Register<AudioService>(Lifetime.Singleton)
                   .As<IAudioService>()
                   .As<ITickable>()
                   .As<IDisposable>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>()
                   .AsSelf();

            // バス設定の適用（Build 後）
            builder.RegisterBuildCallback(resolver =>
            {
                var svc = resolver.Resolve<AudioService>();
                svc.ConfigureBuses(busConfigs);
                svc.ConfigureListenerTarget(scope, useCustomListenerTarget, customListenerTarget, use2DDistanceAttenuation);

                if (resolver.TryResolve<ITimeService>(out var timeService) && timeService != null)
                    svc.BindTimeService(timeService);
            });
        }
    }
}

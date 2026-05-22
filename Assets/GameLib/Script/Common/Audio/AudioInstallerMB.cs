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
    // AudioInstallerMB - AudioService 縺ｮ DI 逋ｻ骭ｲ
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // IScopeInstaller 縺ｨ縺励※ AudioService 繧・DI 繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
    // Inspector 縺ｧ繝舌せ險ｭ螳壹→繝懊Μ繝･繝ｼ繝繝励Ο繝舌う繝繧定ｨｭ螳壼庄閭ｽ縲・
    //
    // ## 驟咲ｽｮ
    //
    // 騾壼ｸｸ縺ｯ GameLifetimeScope 縺ｫ驟咲ｽｮ縲・
    //
    // ================================================================

    [DisallowMultipleComponent]
    public sealed class AudioInstallerMB : MonoBehaviour, IScopeInstaller
    {
        // ----------------------------------------------------------------
        // Inspector 險ｭ螳・
        // ----------------------------------------------------------------

        [BoxGroup("Buses")]
        [LabelText("Bus Configs")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<AudioBusConfig> busConfigs = new();

        [BoxGroup("Volume")]
        [LabelText("Use Constant Volume Provider")]
        [Tooltip("Scalar 騾｣謳ｺ繧剃ｽｿ逕ｨ縺励↑縺・ｴ蜷医・ true")]
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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool useCustomListenerTarget = false;

        [BoxGroup("Listener")]
        [EnableIf(nameof(useCustomListenerTarget))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(customListenerTarget)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource customListenerTarget = new() { Kind = ActorSourceKind.Player };

        [BoxGroup("Listener")]
        [LabelText("Use 2D Distance (XY)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool use2DDistanceAttenuation = true;

        // ----------------------------------------------------------------
        // IScopeInstaller
        // ----------------------------------------------------------------

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // 繝懊Μ繝･繝ｼ繝繝励Ο繝舌う繝縺ｮ逋ｻ骭ｲ
            if (useConstantVolumeProvider)
            {
                builder.RegisterInstance<IAudioVolumeProvider>(
                    new ConstantAudioVolumeProvider(constantMaster, constantBus));
            }
            else
            {
                builder.Register<ScalarAudioVolumeProvider>(RuntimeLifetime.Singleton)
                       .As<IAudioVolumeProvider>();
            }
            // Scalar 騾｣謳ｺ繧剃ｽｿ縺・ｴ蜷医・ ScalarAudioVolumeProvider 繧貞挨騾皮匳骭ｲ

            // AudioService 縺ｮ逋ｻ骭ｲ
            builder.Register<AudioService>(RuntimeLifetime.Singleton)
                   .As<IAudioService>()
                   .As<IScopeTickHandler>()
                   .As<IDisposable>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>()
                   .AsSelf();

            // 繝舌せ險ｭ螳壹・驕ｩ逕ｨ・・uild 蠕鯉ｼ・
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


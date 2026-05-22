#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Times
{
    [DisallowMultipleComponent]
    public sealed class TimerHubMB : MonoBehaviour, IScopeInstaller, ITimerHubSettings
    {
        [BoxGroup("Setup")]
        [LabelText("Auto Initialize")]
        [SerializeField] bool autoInitializeOnStart = true;

        [BoxGroup("Setup")]
        [LabelText("Enable Debug Log")]
        [SerializeField] bool enableDebugLog = false;

        [BoxGroup("Setup")]
        [LabelText("Timers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] TimerChannelDef[] timers = Array.Empty<TimerChannelDef>();

        public TimerChannelDef[] Timers => timers;
        public bool AutoInitializeOnStart => autoInitializeOnStart;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance<ITimerHubSettings>(this);

            builder.Register<TimerHubService>(RuntimeLifetime.Singleton)
                .As<ITimerHubService>()
                .As<IScopeTickHandler>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(enableDebugLog)
                .WithParameter(owner);
        }
    }
}


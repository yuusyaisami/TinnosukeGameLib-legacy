#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UnityRoom
{
    [DisallowMultipleComponent]
    public sealed class UnityRoomMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Scoreboard")]
        [LabelText("HMAC Key")]
        [SerializeField]
        string hmacKey = string.Empty;

        [BoxGroup("Scoreboard")]
        [LabelText("Scoreboard Id")]
        [MinValue(1)]
        [SerializeField]
        int scoreboardId = 1;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;

            builder.RegisterInstance(new UnityRoomSettings
            {
                HmacKey = hmacKey?.Trim() ?? string.Empty,
                ScoreboardId = Mathf.Max(0, scoreboardId),
            });

            builder.Register<UnityRoomService>(RuntimeLifetime.Singleton)
                .As<IUnityRoomService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>();
        }
    }
}

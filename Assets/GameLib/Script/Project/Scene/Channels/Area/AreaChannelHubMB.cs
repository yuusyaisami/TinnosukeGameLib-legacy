#nullable enable
using System;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class AreaChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Hub")]
        [LabelText("Channels")]
        [SerializeField] AreaChannelDefinition[] channels = Array.Empty<AreaChannelDefinition>();

        [BoxGroup("Debug")]
        [LabelText("Show Gizmos")]
        [SerializeField] bool showAreaGizmo = true;

        [BoxGroup("Debug")]
        [ShowIf(nameof(showAreaGizmo))]
        [LabelText("Only Selected")]
        [SerializeField] bool selectedOnly = true;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            if (channels == null)
                channels = Array.Empty<AreaChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }

            builder.Register<AreaChannelHubService>(Lifetime.Singleton)
                .As<IAreaChannelHubService>()
                .As<IChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf()
                .WithParameter(channels);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<AreaChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }
        }
#endif

        void OnDrawGizmos()
        {
            if (selectedOnly)
                return;

            DrawGizmosCore();
        }

        void OnDrawGizmosSelected()
        {
            if (!selectedOnly)
                return;

            DrawGizmosCore();
        }

        void DrawGizmosCore()
        {
            if (!showAreaGizmo || channels == null)
                return;

            var prev = Gizmos.color;

            for (int i = 0; i < channels.Length; i++)
            {
                var def = channels[i];
                if (def == null || !def.Enabled || def.Shape == null)
                    continue;

                Gizmos.color = ColorFromTag(def.Tag);
                var anchor = def.Anchor != null ? def.Anchor : transform;
                var center = anchor.position + def.CenterOffset;
                def.Shape.DrawGizmo(center, def.Plane);
            }

            Gizmos.color = prev;
        }

        static Color ColorFromTag(string? tag)
        {
            var normalized = string.IsNullOrWhiteSpace(tag) ? "default" : tag;
            var hue = Mathf.Abs(normalized.GetHashCode() % 1000) / 1000f;
            return Color.HSVToRGB(hue, 0.75f, 0.95f);
        }
    }
}

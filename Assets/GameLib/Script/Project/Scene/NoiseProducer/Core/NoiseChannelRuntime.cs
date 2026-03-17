#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.NoiseProducer
{
    /// <summary>
    /// 1 channel のランタイム状態を保持する。
    /// </summary>
    public sealed class NoiseChannelRuntime
    {
        public NoiseChannelDefinition Definition { get; private set; }
        public string ChannelId { get; private set; } = string.Empty;
        public string ProducerTag { get; private set; } = string.Empty;

        // ── Time ────────────────────────────────────────────────
        public float RawTime { get; set; }
        public float ChannelTime { get; set; }
        public float DeltaTime { get; set; }

        // ── Dirty tracking ──────────────────────────────────────
        public NoiseChannelRefreshFlags PendingRefreshFlags { get; set; }
        public bool ContentDirty { get; set; }
        public bool HasTimeReactiveStage { get; set; }

        // ── Render tracking ─────────────────────────────────────
        public int LastRenderedFrame { get; set; }
        public int LastPublishedFrame { get; set; }

        // ── Resources ───────────────────────────────────────────
        public RenderTexture? OutputRT { get; set; }
        public RenderTexture? PingRT { get; set; }
        public RenderTexture? PongRT { get; set; }
        public Material? BlitMaterial { get; set; }

        // ── Stage slots ─────────────────────────────────────────
        public readonly Dictionary<string, RenderTexture> SlotTextures = new(System.StringComparer.Ordinal);

        // ── Parameter runtime ───────────────────────────────────
        public readonly NoiseParameterRuntimeTable Parameters = new();

        // ── Init / Reset ────────────────────────────────────────

        public NoiseChannelRuntime(string channelId, NoiseChannelDefinition definition, string producerTag)
        {
            Definition = definition;
            ChannelId = channelId;
            ProducerTag = producerTag;
            HasTimeReactiveStage = definition.HasTimeReactiveStage;
            Parameters.Initialize(definition.Parameters);
            PendingRefreshFlags = NoiseChannelRefreshFlags.Full;
            ContentDirty = true;
        }

        public bool NeedsRender
            => PendingRefreshFlags != NoiseChannelRefreshFlags.None
               || ContentDirty
               || HasTimeReactiveStage
               || Parameters.HasAnimating;

        public void ReleaseResources()
        {
            Parameters.Clear();
            OutputRT = ReleaseRT(OutputRT);
            PingRT = ReleaseRT(PingRT);
            PongRT = ReleaseRT(PongRT);

            foreach (var rt in SlotTextures.Values)
            {
                if (rt != null && rt.IsCreated())
                    rt.Release();
                if (rt != null)
                    Object.Destroy(rt);
            }
            SlotTextures.Clear();

            if (BlitMaterial != null)
            {
                Object.Destroy(BlitMaterial);
                BlitMaterial = null;
            }
        }

        static RenderTexture? ReleaseRT(RenderTexture? rt)
        {
            if (rt == null) return null;
            if (rt.IsCreated()) rt.Release();
            Object.Destroy(rt);
            return null;
        }
    }
}

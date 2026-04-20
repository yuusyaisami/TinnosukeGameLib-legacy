#nullable enable
using System;
using System.Collections.Generic;
using Game.SharedTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VContainer.Unity;

namespace Game.NoiseProducer
{
    public sealed class NoiseProducerService
        : INoiseProducerService,
          IScopeAcquireHandler,
          IScopeReleaseHandler,
          IScopeTickHandler,
          IDisposable
    {
        const int MaxActiveChannels = 8;
        const int MaxResolution = 2048;

        static readonly int s_Time = Shader.PropertyToID("_NoiseTime");
        static readonly int s_Seed = Shader.PropertyToID("_NoiseSeed");
        static readonly int s_Scale = Shader.PropertyToID("_NoiseScale");
        static readonly int s_Offset = Shader.PropertyToID("_NoiseOffset");
        static readonly int s_Scroll = Shader.PropertyToID("_NoiseScroll");
        static readonly int s_Rotation = Shader.PropertyToID("_NoiseRotation");
        static readonly int s_Center = Shader.PropertyToID("_NoiseCenter");
        static readonly int s_Strength = Shader.PropertyToID("_NoiseStrength");
        static readonly int s_GradientA = Shader.PropertyToID("_NoiseGradientA");
        static readonly int s_GradientB = Shader.PropertyToID("_NoiseGradientB");
        static readonly int s_Threshold = Shader.PropertyToID("_NoiseThreshold");
        static readonly int s_Softness = Shader.PropertyToID("_NoiseSoftness");
        static readonly int s_Blend = Shader.PropertyToID("_NoiseBlend");
        static readonly int s_Opacity = Shader.PropertyToID("_NoiseOpacity");
        static readonly int s_ClearColor = Shader.PropertyToID("_NoiseClearColor");
        static readonly int s_Octaves = Shader.PropertyToID("_NoiseOctaves");
        static readonly int s_Lacunarity = Shader.PropertyToID("_NoiseLacunarity");
        static readonly int s_Gain = Shader.PropertyToID("_NoiseGain");
        static readonly int s_InputTex = Shader.PropertyToID("_NoiseInputTex");
        static readonly int s_SecondaryTex = Shader.PropertyToID("_NoiseSecondaryTex");
        static readonly int s_MaskTex = Shader.PropertyToID("_NoiseMaskTex");
        static readonly int s_StageOp = Shader.PropertyToID("_NoiseStageOp");

        readonly ISharedTextureChannelHub _hub;
        readonly Dictionary<string, NoiseChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly List<NoiseChannelDefinition>? _initialDefinitions;

        Shader? _noiseShader;
        bool _acquired;
        string _producerTagPrefix = "noise-producer";

        // ── Constructor ─────────────────────────────────────────

        public NoiseProducerService(ISharedTextureChannelHub hub, List<NoiseChannelDefinition>? initialDefinitions = null)
        {
            _hub = hub;
            _initialDefinitions = initialDefinitions;
        }

        // ── Lifecycle ───────────────────────────────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
            _noiseShader = Shader.Find("Hidden/GameLib/NoiseGenerator");

            if (_noiseShader == null)
                Debug.LogWarning("[NoiseProducerService] NoiseGenerator shader not found.");

            var identity = scope.Identity;
            if (identity != null)
            {
                var scopeKey = string.IsNullOrEmpty(identity.Id)
                    ? identity.SelfTransform != null ? identity.SelfTransform.name : string.Empty
                    : identity.Id;
                if (!string.IsNullOrEmpty(scopeKey))
                    _producerTagPrefix = $"noise-producer/{scopeKey}";
            }

            // Register initial definitions (after tag prefix is set)
            if (_initialDefinitions != null)
            {
                foreach (var def in _initialDefinitions)
                {
                    if (!string.IsNullOrEmpty(def?.ChannelId))
                        RegisterChannel(def.ChannelId, def);
                }
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
            ReleaseAllChannels();
        }

        public void Dispose()
        {
            ReleaseAllChannels();
        }

        // ── INoiseProducerService ───────────────────────────────

        public bool ContainsChannel(string channelId)
            => _channels.ContainsKey(channelId);

        public bool TryGetChannelState(string channelId, out NoiseChannelState state)
        {
            if (!_channels.TryGetValue(channelId, out var runtime))
            {
                state = default;
                return false;
            }

            state = new NoiseChannelState(
                isActive: _acquired,
                isTemporalActive: runtime.HasTimeReactiveStage || runtime.Parameters.HasAnimating,
                channelTime: runtime.ChannelTime,
                lastRenderedFrame: runtime.LastRenderedFrame,
                parameterCount: runtime.Parameters.Count,
                stageCount: runtime.Definition.Stages.Count);
            return true;
        }

        public bool RegisterChannel(string channelId, NoiseChannelDefinition definition)
        {
            if (string.IsNullOrEmpty(channelId) || definition == null)
            {
                Debug.LogWarning($"[NoiseProducerService] RegisterChannel failed: invalid args (channelId={channelId})");
                return false;
            }

            if (_channels.Count >= MaxActiveChannels && !_channels.ContainsKey(channelId))
            {
                Debug.LogWarning($"[NoiseProducerService] RegisterChannel failed: max channel limit ({MaxActiveChannels}) reached.");
                return false;
            }

            if (_channels.TryGetValue(channelId, out var existing))
            {
                existing.ReleaseResources();
                _hub.ClearByProducer(existing.ProducerTag);
            }

            var producerTag = $"{_producerTagPrefix}/{channelId}";
            var runtime = new NoiseChannelRuntime(channelId, definition, producerTag);
            _channels[channelId] = runtime;
            return true;
        }

        public bool UnregisterChannel(string channelId)
        {
            if (!_channels.TryGetValue(channelId, out var runtime))
            {
                Debug.LogWarning($"[NoiseProducerService] UnregisterChannel: channel '{channelId}' not found.");
                return false;
            }

            _hub.ClearByProducer(runtime.ProducerTag);
            runtime.ReleaseResources();
            _channels.Remove(channelId);
            return true;
        }

        public bool TryWriteParameter(in NoiseParameterWriteRequest request)
        {
            if (!_channels.TryGetValue(request.Address.ChannelId, out var runtime))
            {
                Debug.LogWarning($"[NoiseProducerService] TryWriteParameter: channel '{request.Address.ChannelId}' not found.");
                return false;
            }

            var result = runtime.Parameters.TryWrite(request);
            if (result)
                runtime.ContentDirty = true;
            return result;
        }

        public bool ClearParameterLayer(in NoiseParameterAddress address)
        {
            if (!_channels.TryGetValue(address.ChannelId, out var runtime))
            {
                Debug.LogWarning($"[NoiseProducerService] ClearParameterLayer: channel '{address.ChannelId}' not found.");
                return false;
            }

            var result = runtime.Parameters.ClearLayer(address);
            if (result)
                runtime.ContentDirty = true;
            return result;
        }

        public bool RequestRefresh(string channelId, NoiseChannelRefreshFlags flags)
        {
            if (!_channels.TryGetValue(channelId, out var runtime))
            {
                Debug.LogWarning($"[NoiseProducerService] RequestRefresh: channel '{channelId}' not found.");
                return false;
            }

            runtime.PendingRefreshFlags |= flags;
            return true;
        }

        // ── Tick ────────────────────────────────────────────────

        public void Tick()
        {
            if (!_acquired) return;

            float dt = Time.deltaTime;
            int frame = Time.frameCount;

            foreach (var kv in _channels)
            {
                var runtime = kv.Value;
                TickChannel(runtime, dt, frame);
            }
        }

        void TickChannel(NoiseChannelRuntime runtime, float dt, int frame)
        {
            var def = runtime.Definition;

            // time update
            runtime.DeltaTime = dt * def.TimeScale;
            runtime.RawTime += runtime.DeltaTime;

            if (def.LoopSeconds > 0f)
                runtime.ChannelTime = runtime.RawTime % def.LoopSeconds;
            else
                runtime.ChannelTime = runtime.RawTime;

            // consume parameter dirty
            if (runtime.Parameters.ConsumeAnyDirty())
                runtime.ContentDirty = true;

            // Process refresh flags
            var flags = runtime.PendingRefreshFlags;
            if (flags != NoiseChannelRefreshFlags.None)
            {
                if ((flags & NoiseChannelRefreshFlags.ResolveParameters) != 0)
                    runtime.Parameters.RebindDefinitions(def.Parameters);

                if ((flags & NoiseChannelRefreshFlags.ReloadDefinition) != 0)
                {
                    runtime.Parameters.RebindDefinitions(def.Parameters);
                    runtime.HasTimeReactiveStage = def.HasTimeReactiveStage;
                }

                if ((flags & NoiseChannelRefreshFlags.RecreateTargets) != 0)
                    EnsureOutputRT(runtime);

                if ((flags & NoiseChannelRefreshFlags.RebuildMaterials) != 0)
                    EnsureBlitMaterial(runtime);

                runtime.PendingRefreshFlags = NoiseChannelRefreshFlags.None;
                runtime.ContentDirty = true;
            }

            // check if render needed
            if (!runtime.NeedsRender) return;

            // Ensure resources
            EnsureOutputRT(runtime);
            EnsureBlitMaterial(runtime);
            if (runtime.OutputRT == null || runtime.BlitMaterial == null) return;

            // Render
            RenderChannel(runtime);
            runtime.LastRenderedFrame = frame;
            runtime.ContentDirty = false;

            // Publish
            if (def.AutoPublish && !string.IsNullOrEmpty(def.OutputTag))
                PublishChannel(runtime);
        }

        // ── Render ──────────────────────────────────────────────

        void RenderChannel(NoiseChannelRuntime runtime)
        {
            var def = runtime.Definition;
            var mat = runtime.BlitMaterial!;
            var outputRT = runtime.OutputRT!;

            // Clear
            var prevActive = RenderTexture.active;
            RenderTexture.active = outputRT;
            GL.Clear(true, true, def.ClearColor);
            RenderTexture.active = prevActive;

            var stages = def.Stages;
            if (stages.Count == 0) return;

            runtime.SlotTextures.Clear();

            // Find last enabled stage index
            int lastEnabledIndex = -1;
            int enabledCount = 0;
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (!stages[i].Enabled)
                    continue;
                enabledCount++;
                if (lastEnabledIndex < 0)
                    lastEnabledIndex = i;
            }
            if (lastEnabledIndex < 0) return;

            // ping-pong for multi-enabled-stage
            if (enabledCount > 1)
                EnsurePingPongRT(runtime);

            RenderTexture? currentInput = null;
            int enabledStep = 0;

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (!stage.Enabled) continue;

                var target = (i == lastEnabledIndex) ? outputRT : GetPingPong(runtime, enabledStep);
                ApplyStageToMaterial(mat, stage, runtime, currentInput);
                Graphics.Blit(currentInput, target, mat, GetPassIndex(stage));

                // Store slot if named
                if (!string.IsNullOrEmpty(stage.OutputSlot))
                    runtime.SlotTextures[stage.OutputSlot] = target;

                currentInput = target;
                enabledStep++;
            }
        }

        void ApplyStageToMaterial(Material mat, NoiseStageDefinition stage, NoiseChannelRuntime runtime, RenderTexture? input)
        {
            ResetStageMaterialState(mat, input);

            float sourceTime = stage.UseGlobalTime ? Time.time : runtime.ChannelTime;
            mat.SetFloat(s_Time, sourceTime * stage.Speed + stage.Phase);
            mat.SetFloat(s_Seed, runtime.Definition.Seed + stage.Seed);
            mat.SetInt(s_StageOp, GetOpIndex(stage));

            switch (stage.StageKind)
            {
                case NoiseStageKind.Generator:
                    mat.SetVector(s_Scale, stage.Scale);
                    mat.SetVector(s_Offset, stage.Offset);
                    mat.SetColor(s_GradientA, stage.GradientA);
                    mat.SetColor(s_GradientB, stage.GradientB);
                    mat.SetInt(s_Octaves, stage.Octaves);
                    mat.SetFloat(s_Lacunarity, stage.Lacunarity);
                    mat.SetFloat(s_Gain, stage.Gain);
                    break;

                case NoiseStageKind.Uv:
                    mat.SetVector(s_Scroll, new Vector4(stage.Scroll.x, stage.Scroll.y, 0, 0));
                    mat.SetFloat(s_Strength, stage.FlowStrength);
                    mat.SetFloat(s_Rotation, stage.UvRotation);
                    mat.SetVector(s_Center, stage.PolarCenter);
                    break;

                case NoiseStageKind.Filter:
                    if (!string.IsNullOrEmpty(stage.PrimaryInput)
                        && runtime.SlotTextures.TryGetValue(stage.PrimaryInput, out var primaryRT))
                    {
                        mat.SetTexture(s_InputTex, primaryRT);
                    }

                    mat.SetFloat(s_Threshold, stage.Threshold);
                    mat.SetFloat(s_Softness, stage.Softness);
                    mat.SetFloat(s_Strength, stage.Strength);
                    break;

                case NoiseStageKind.Composite:
                    mat.SetFloat(s_Blend, stage.Blend);
                    mat.SetFloat(s_Opacity, stage.Opacity);

                    var primaryTexture = input != null ? (Texture)input : Texture2D.blackTexture;
                    if (!string.IsNullOrEmpty(stage.CompositePrimaryInput)
                        && runtime.SlotTextures.TryGetValue(stage.CompositePrimaryInput, out var priRT))
                    {
                        primaryTexture = priRT;
                    }

                    Texture secondaryTexture = primaryTexture;
                    if (!string.IsNullOrEmpty(stage.SecondaryInput)
                        && runtime.SlotTextures.TryGetValue(stage.SecondaryInput, out var secRT))
                    {
                        secondaryTexture = secRT;
                    }

                    mat.SetTexture(s_InputTex, primaryTexture);
                    mat.SetTexture(s_SecondaryTex, secondaryTexture);

                    if (!string.IsNullOrEmpty(stage.MaskInput)
                        && runtime.SlotTextures.TryGetValue(stage.MaskInput, out var maskRT))
                    {
                        mat.SetTexture(s_MaskTex, maskRT);
                    }
                    break;
            }

            // Apply parameter overrides
            ApplyParameterOverrides(mat, stage, runtime);
        }

        void ApplyParameterOverrides(Material mat, NoiseStageDefinition stage, NoiseChannelRuntime runtime)
        {
            var defs = runtime.Definition.Parameters;
            for (int i = 0; i < defs.Count; i++)
            {
                var paramDef = defs[i];
                if (!paramDef.Exposed) continue;
                if (paramDef.AffectsStageId != stage.StageId) continue;

                if (!runtime.Parameters.TryGetValue(paramDef.ParameterKey, out var val)) continue;

                switch (paramDef.AffectsField)
                {
                    case "Scale":
                        if (val.Kind == NoiseParameterValueKind.Vector2)
                            mat.SetVector(s_Scale, val.Vector2Value);
                        break;
                    case "Offset":
                        if (val.Kind == NoiseParameterValueKind.Vector2)
                            mat.SetVector(s_Offset, val.Vector2Value);
                        break;
                    case "Scroll":
                        if (val.Kind == NoiseParameterValueKind.Vector2)
                            mat.SetVector(s_Scroll, new Vector4(val.Vector2Value.x, val.Vector2Value.y, 0, 0));
                        break;
                    case "FlowStrength":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Strength, val.FloatValue);
                        break;
                    case "UvRotation":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Rotation, val.FloatValue);
                        break;
                    case "PolarCenter":
                        if (val.Kind == NoiseParameterValueKind.Vector2)
                            mat.SetVector(s_Center, val.Vector2Value);
                        break;
                    case "Threshold":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Threshold, val.FloatValue);
                        break;
                    case "Softness":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Softness, val.FloatValue);
                        break;
                    case "Strength":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Strength, val.FloatValue);
                        break;
                    case "Blend":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Blend, val.FloatValue);
                        break;
                    case "Opacity":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Opacity, val.FloatValue);
                        break;
                    case "Seed":
                        if (val.Kind == NoiseParameterValueKind.Int)
                            mat.SetFloat(s_Seed, val.IntValue);
                        break;
                    case "Octaves":
                        if (val.Kind == NoiseParameterValueKind.Int)
                            mat.SetInt(s_Octaves, val.IntValue);
                        break;
                    case "Lacunarity":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Lacunarity, val.FloatValue);
                        break;
                    case "Gain":
                        if (val.Kind == NoiseParameterValueKind.Float)
                            mat.SetFloat(s_Gain, val.FloatValue);
                        break;
                    case "GradientA":
                        if (val.Kind == NoiseParameterValueKind.Color)
                            mat.SetColor(s_GradientA, val.ColorValue);
                        break;
                    case "GradientB":
                        if (val.Kind == NoiseParameterValueKind.Color)
                            mat.SetColor(s_GradientB, val.ColorValue);
                        break;
                }
            }
        }

        static void ResetStageMaterialState(Material mat, Texture? input)
        {
            var primaryTexture = input != null ? input : Texture2D.blackTexture;
            mat.SetTexture(s_InputTex, primaryTexture);
            mat.SetTexture(s_SecondaryTex, primaryTexture);
            mat.SetTexture(s_MaskTex, Texture2D.blackTexture);
            mat.SetVector(s_Scale, Vector2.one);
            mat.SetVector(s_Offset, Vector2.zero);
            mat.SetVector(s_Scroll, Vector4.zero);
            mat.SetFloat(s_Rotation, 0f);
            mat.SetVector(s_Center, new Vector4(0.5f, 0.5f, 0f, 0f));
            mat.SetFloat(s_Strength, 1f);
            mat.SetColor(s_GradientA, Color.black);
            mat.SetColor(s_GradientB, Color.white);
            mat.SetFloat(s_Threshold, 0.5f);
            mat.SetFloat(s_Softness, 0.1f);
            mat.SetFloat(s_Blend, 0.5f);
            mat.SetFloat(s_Opacity, 1f);
            mat.SetInt(s_Octaves, 4);
            mat.SetFloat(s_Lacunarity, 2f);
            mat.SetFloat(s_Gain, 0.5f);
        }

        // ── Publish ─────────────────────────────────────────────

        void PublishChannel(NoiseChannelRuntime runtime)
        {
            if (runtime.OutputRT == null) return;

            var def = runtime.Definition;
            var res = def.ClampedResolution;
            var desc = new SharedTextureDescriptor(
                res.x, res.y, def.Format, def.FilterMode, def.WrapMode);
            var options = SharedTexturePublishOptions.ForNoiseProducer(runtime.ProducerTag);
            _hub.Publish(def.OutputTag, runtime.OutputRT, desc, options);
            runtime.LastPublishedFrame = Time.frameCount;
        }

        // ── Resource Management ─────────────────────────────────

        void EnsureOutputRT(NoiseChannelRuntime runtime)
        {
            var res = runtime.Definition.ClampedResolution;
            runtime.OutputRT = EnsureRT(runtime.OutputRT, res.x, res.y,
                $"Noise_{runtime.ChannelId}_Output", runtime.Definition);
        }

        void EnsurePingPongRT(NoiseChannelRuntime runtime)
        {
            if (runtime.Definition.Stages.Count <= 1) return;

            var res = runtime.Definition.ClampedResolution;
            runtime.PingRT = EnsureRT(runtime.PingRT, res.x, res.y,
                $"Noise_{runtime.ChannelId}_Ping", runtime.Definition);
            runtime.PongRT = EnsureRT(runtime.PongRT, res.x, res.y,
                $"Noise_{runtime.ChannelId}_Pong", runtime.Definition);
        }

        void EnsureBlitMaterial(NoiseChannelRuntime runtime)
        {
            if (runtime.BlitMaterial != null) return;
            if (_noiseShader == null) return;

            runtime.BlitMaterial = new Material(_noiseShader)
            {
                name = $"Noise_{runtime.ChannelId}_Mat",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        static RenderTexture? EnsureRT(RenderTexture? rt, int width, int height, string name, NoiseChannelDefinition def)
        {
            width = Mathf.Clamp(width, 1, MaxResolution);
            height = Mathf.Clamp(height, 1, MaxResolution);

            if (rt != null && rt.width == width && rt.height == height)
                return rt;

            if (rt != null)
            {
                if (rt.IsCreated()) rt.Release();
                UnityEngine.Object.Destroy(rt);
            }

            rt = new RenderTexture(width, height, 0, def.Format)
            {
                name = name,
                filterMode = def.FilterMode,
                wrapMode = def.WrapMode,
            };
            rt.Create();
            return rt;
        }

        RenderTexture GetPingPong(NoiseChannelRuntime runtime, int enabledStepIndex)
        {
            var rt = (enabledStepIndex % 2 == 0) ? runtime.PingRT : runtime.PongRT;
            if (rt != null) return rt;
            // Fallback: ensure creation (should not normally happen)
            EnsurePingPongRT(runtime);
            return ((enabledStepIndex % 2 == 0) ? runtime.PingRT : runtime.PongRT)!;
        }

        static int GetPassIndex(NoiseStageDefinition stage)
        {
            return stage.StageKind switch
            {
                NoiseStageKind.Generator => 0,
                NoiseStageKind.Uv => 1,
                NoiseStageKind.Filter => 2,
                NoiseStageKind.Composite => 3,
                _ => 0,
            };
        }

        static int GetOpIndex(NoiseStageDefinition stage)
        {
            return stage.StageKind switch
            {
                NoiseStageKind.Generator => (int)stage.GeneratorOp,
                NoiseStageKind.Uv => (int)stage.UvOp,
                NoiseStageKind.Filter => (int)stage.FilterOp,
                NoiseStageKind.Composite => (int)stage.CompositeOp,
                _ => 0,
            };
        }

        // ── Cleanup ─────────────────────────────────────────────

        void ReleaseAllChannels()
        {
            foreach (var kv in _channels)
            {
                _hub.ClearByProducer(kv.Value.ProducerTag);
                kv.Value.ReleaseResources();
            }
            _channels.Clear();
        }
    }
}

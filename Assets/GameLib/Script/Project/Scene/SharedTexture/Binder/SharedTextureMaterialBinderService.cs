#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.MaterialFx;
using UnityEngine;
using VContainer.Unity;

namespace Game.SharedTexture
{
    /// <summary>
    /// SharedTextureChannelHub の Texture を MaterialFx 経由で BaseShader に流す binder。
    /// ITaggedMaterialFxProvider から対象 Player を tag で解決し、
    /// ISharedTextureChannelHub から Texture を取得して MaterialFx キーへ流す。
    /// </summary>
    public sealed class SharedTextureMaterialBinderService
        : IScopeAcquireHandler,
          IScopeReleaseHandler,
          ITickable,
          IDisposable
    {
        // MaterialFx key constants (BaseShader/ExternalTextures)
        const string KeyExtTexA = "BaseShader/ExternalTextures/ExtTexA";
        const string KeyExtTexB = "BaseShader/ExternalTextures/ExtTexB";
        const string KeyCustomRT = "BaseShader/ExternalTextures/CustomRT";

        readonly ISharedTextureChannelHub _hub;
        readonly ITaggedMaterialFxProvider? _materialFxProvider;
        readonly SharedTextureBinderOptions _options;

        bool _acquired;

        public SharedTextureMaterialBinderService(
            ISharedTextureChannelHub hub,
            SharedTextureBinderOptions options,
            ITaggedMaterialFxProvider? materialFxProvider = null)
        {
            _hub = hub;
            _materialFxProvider = materialFxProvider;
            _options = options;
        }

        // ── Lifecycle ───────────────────────────────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
            ClearAllBindings();
        }

        public void Dispose()
        {
            ClearAllBindings();
        }

        // ── Tick ────────────────────────────────────────────────

        public void Tick()
        {
            if (!_acquired || _materialFxProvider == null)
                return;

            var bindings = _options.Bindings;
            if (bindings == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                ProcessBinding(bindings[i]);
            }
        }

        void ProcessBinding(in SharedTextureBindingDef binding)
        {
            // 1. Resolve MaterialFx from provider
            if (_materialFxProvider == null)
                return;

            if (!_materialFxProvider.TryGetMaterialFx(binding.TargetPlayerTag, out var materialFx))
                return;

            if (materialFx == null)
                return;

            // 2. Resolve texture from hub
            if (!_hub.TryGet(binding.SharedTextureTag, out var frame) || frame.Texture == null)
            {
                if (binding.ClearWhenMissing)
                    ClearBinding(materialFx, binding);
                return;
            }

            // 3. Apply texture to MaterialFx
            var key = GetMaterialFxKey(binding.BindSlot);
            var contextTag = string.IsNullOrEmpty(binding.ContextTag) ? "shared-tex-binder" : binding.ContextTag;
            var typedValue = MaterialFxTypedValue.FromTexture(frame.Texture);

            materialFx.SetLayer(
                key,
                contextTag,
                typedValue,
                MaterialFxBlendMode.Override,
                binding.Priority);
        }

        void ClearBinding(IMaterialFxService materialFx, in SharedTextureBindingDef binding)
        {
            var key = GetMaterialFxKey(binding.BindSlot);
            var contextTag = string.IsNullOrEmpty(binding.ContextTag) ? "shared-tex-binder" : binding.ContextTag;
            materialFx.RemoveLayer(key, contextTag);
        }

        void ClearAllBindings()
        {
            // Best-effort clear; provider may no longer be available
        }

        static string GetMaterialFxKey(SharedTextureBindSlot slot) => slot switch
        {
            SharedTextureBindSlot.ExternalA => KeyExtTexA,
            SharedTextureBindSlot.ExternalB => KeyExtTexB,
            SharedTextureBindSlot.CustomRT => KeyCustomRT,
            _ => KeyExtTexA,
        };
    }

    // ── Options ─────────────────────────────────────────────────

    public sealed class SharedTextureBinderOptions
    {
        public List<SharedTextureBindingDef> Bindings { get; }

        public SharedTextureBinderOptions(List<SharedTextureBindingDef> bindings)
        {
            Bindings = bindings;
        }
    }
}

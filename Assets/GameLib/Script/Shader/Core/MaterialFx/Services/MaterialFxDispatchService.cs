#nullable enable
using System;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// IMaterialFxDispatchService の実装。
    /// FinalValue を Sink 経由で GPU に送信する。
    /// </summary>
    public sealed class MaterialFxDispatchService : IMaterialFxDispatchService
    {
        readonly IMaterialFxPropertyRegistry _registry;
        readonly IMaterialFxTargetAdapter _adapter;

        public MaterialFxDispatchService(
            IMaterialFxPropertyRegistry registry,
            IMaterialFxTargetAdapter adapter)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public void Dispatch(string stableKey, MaterialFxTypedValue value)
        {

            if (!_registry.TryGet(stableKey, out var meta))
            {
                Debug.LogWarning($"[MaterialFxDispatchService] Unknown StableKey: '{stableKey}'");
                return;
            }

            // BaseShader は Sink 経由で送信
            DispatchToBaseShader(meta, value);
        }

        void DispatchToBaseShader(MaterialFxPropertyMeta meta, MaterialFxTypedValue value)
        {
            var propertyId = meta.ShaderPropertyId != 0 ? meta.ShaderPropertyId : Shader.PropertyToID(meta.ShaderPropertyName);
            if (propertyId == 0)
            {
                Debug.LogWarning($"[MaterialFxDispatchService] Invalid property name: '{meta.ShaderPropertyName}'");
                return;
            }

            // Adapter から Sink を取得して送信
            _adapter.DispatchValue(propertyId, value);
        }

        public void FlushAll(IMaterialFxLayerService layerService)
        {
            var dirtyKeys = layerService.GetDirtyKeys();
            for (int i = dirtyKeys.Count - 1; i >= 0; i--)
            {
                var key = dirtyKeys[i];
                var finalValue = layerService.ComputeFinalValue(key);
                Dispatch(key, finalValue);
                layerService.ClearDirty(key);
            }
            Apply();
        }

        public void Apply()
        {
            _adapter.Apply();
        }

        public void Dispose()
        {
            // 特にリソース解放は不要
        }
    }
}

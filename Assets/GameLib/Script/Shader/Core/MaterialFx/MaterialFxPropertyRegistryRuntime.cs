using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// ランタイム用の高速プロパティレジストリ。
    /// SO から辞書を構築し、StableKey で O(1) 参照する。
    /// </summary>
    public sealed class MaterialFxPropertyRegistryRuntime : IMaterialFxPropertyRegistry
    {
        readonly Dictionary<string, MaterialFxPropertyMeta> _map;

        public MaterialFxPropertyRegistryRuntime(MaterialFxPropertyRegistrySO so)
        {
            _map = new Dictionary<string, MaterialFxPropertyMeta>(StringComparer.Ordinal);

            if (so == null || so.Nodes == null) return;

            foreach (var n in so.Nodes)
            {
                if (n == null || n.IsFolder) continue;
                var key = n.StableKey ?? string.Empty;
                if (string.IsNullOrEmpty(key)) continue;
                if (n.Sender != MaterialFxSenderKind.BaseShader) continue;

                var meta = new MaterialFxPropertyMeta
                {
                    StableKey = key,
                    Sender = n.Sender,
                    ValueType = n.ValueType,
                    ShaderPropertyName = n.ShaderPropertyName ?? string.Empty,
                    Description = n.Description ?? string.Empty,
                    EnumDefinition = n.EnumDefinition,
                    RangeEnabled = n.RangeEnabled,
                    RangeMinMax = n.RangeMinMax,
                    ShaderPropertyId = 0
                };

                // Cache PropertyId if we have a shader property name
                if (!string.IsNullOrEmpty(meta.ShaderPropertyName))
                {
                    meta.ShaderPropertyId = Shader.PropertyToID(meta.ShaderPropertyName);
                }

                _map[key] = meta;
            }

        }

        public bool TryGet(string stableKey, out MaterialFxPropertyMeta meta)
        {
            if (string.IsNullOrEmpty(stableKey))
            {
                meta = default;
                return false;
            }
            return _map.TryGetValue(stableKey, out meta);
        }

        public bool TryGetValueType(string stableKey, out ValueKind type)
        {
            if (TryGet(stableKey, out var meta))
            {
                type = meta.ValueType;
                return true;
            }
            type = default;
            return false;
        }

        public bool TryGetSender(string stableKey, out MaterialFxSenderKind sender)
        {
            if (TryGet(stableKey, out var meta))
            {
                sender = meta.Sender;
                return true;
            }
            sender = default;
            return false;
        }
    }
}

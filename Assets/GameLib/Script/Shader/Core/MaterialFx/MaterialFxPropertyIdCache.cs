using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// ShaderPropertyName → int ID のキャッシュ。
    /// Shader.PropertyToID の呼び出しを最小化する。
    /// </summary>
    public static class MaterialFxPropertyIdCache
    {
        static readonly Dictionary<string, int> _cache = new(StringComparer.Ordinal);

        public static int GetId(string shaderPropertyName)
        {
            shaderPropertyName ??= string.Empty;
            if (shaderPropertyName.Length == 0) return 0;

            if (_cache.TryGetValue(shaderPropertyName, out var id))
                return id;

            id = Shader.PropertyToID(shaderPropertyName);
            _cache[shaderPropertyName] = id;
            return id;
        }
    }
}

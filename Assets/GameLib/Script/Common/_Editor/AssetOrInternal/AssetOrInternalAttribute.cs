using System;
using UnityEngine;

namespace Game
{
    /// <summary>SO フィールドに「内部作成/抽出/削除」ボタンを生やす Odin 用属性。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AssetOrInternalAttribute : PropertyAttribute
    {
        public Type[] AllowedTypes { get; }

        /// <summary>
        /// Allows any <see cref="ScriptableObject"/> when no types are provided.
        /// </summary>
        public AssetOrInternalAttribute() { }

        /// <summary>
        /// Restrict selectable/creatable assets to the provided base types.
        /// </summary>
        public AssetOrInternalAttribute(params Type[] allowedTypes)
        {
            AllowedTypes = allowedTypes;
        }

        public bool IsTypeAllowed(Type type)
        {
            if (type == null) return false;
            if (AllowedTypes == null || AllowedTypes.Length == 0) return true;
            for (int i = 0; i < AllowedTypes.Length; i++)
            {
                var allowed = AllowedTypes[i];
                if (allowed == null) continue;
                if (allowed.IsAssignableFrom(type))
                    return true;
            }
            return false;
        }
    }
}

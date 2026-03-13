#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// CameraOutputOverrideService と Render Pipeline を橋渡しする static レジストリ。
    /// Service がフレームごとに override Texture を登録し、
    /// CameraOutputOverrideRenderPass が参照する。
    /// </summary>
    public static class CameraOutputOverrideRegistry
    {
        static readonly Dictionary<Camera, Texture> s_Overrides = new();

        public static void SetOverride(Camera camera, Texture? texture)
        {
            if (texture == null)
            {
                s_Overrides.Remove(camera);
                return;
            }
            s_Overrides[camera] = texture;
        }

        public static Texture? GetOverrideTexture(Camera camera)
        {
            return s_Overrides.TryGetValue(camera, out var tex) ? tex : null;
        }

        public static bool HasOverride(Camera camera)
        {
            return s_Overrides.ContainsKey(camera);
        }

        public static void Clear(Camera camera)
        {
            s_Overrides.Remove(camera);
        }

        public static void ClearAll()
        {
            s_Overrides.Clear();
        }
    }
}

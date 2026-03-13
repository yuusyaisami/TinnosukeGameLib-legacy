#nullable enable
using UnityEngine;

namespace Game.CameraSystem
{
    public enum CameraOutputOverrideMode
    {
        None = 0,
        SharedTexture = 10,
    }

    public interface ICameraOutputOverrideService
    {
        bool Enabled { get; set; }
        CameraOutputOverrideMode Mode { get; set; }
        string SharedTextureTag { get; set; }

        /// <summary>
        /// 現在の override 設定に基づいて、使用すべき Texture を返す。
        /// override 無効時や Texture が見つからない場合は null。
        /// </summary>
        Texture? ResolveOverrideTexture();
    }
}

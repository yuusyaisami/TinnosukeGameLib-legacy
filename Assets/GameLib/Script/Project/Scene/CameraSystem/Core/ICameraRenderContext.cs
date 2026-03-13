#nullable enable
using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Camera の描画文脈を外部へ公開する interface。
    /// CameraCapture や PostProcess など、Camera 関連サービスが参照する。
    /// </summary>
    public interface ICameraRenderContext
    {
        Camera Camera { get; }
        Transform CameraTransform { get; }
        Transform FxTransform { get; }
        string CameraTag { get; }
    }
}

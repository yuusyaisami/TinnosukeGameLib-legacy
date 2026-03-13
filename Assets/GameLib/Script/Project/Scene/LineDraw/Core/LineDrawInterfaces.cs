using System.Collections.Generic;
using Game.MaterialFx;
using UnityEngine;

namespace Game.LineDraw
{
    public interface ILineDrawService
    {
        /// <summary>
        /// 現在アクティブな全ての線ハンドルを取得する。
        /// </summary>
        IReadOnlyList<LineHandle> ActiveHandles { get; }

        /// <summary>
        /// アクティブな線の数を取得する。
        /// </summary>
        int ActiveCount { get; }

        LineHandle CreateSegment(LineSegmentRequest request);
        LineHandle CreatePath(LinePathRequest request);

        bool UpdateSegment(LineHandle handle, LineSegmentRequest request);
        bool UpdatePath(LineHandle handle, LinePathRequest request);

        /// <summary>
        /// 線のスタイルを更新する（太さ、色、パターン等）。
        /// </summary>
        bool UpdateStyle(LineHandle handle, LineStyle style);

        /// <summary>
        /// 線のパターンオフセットを更新する。
        /// </summary>
        bool UpdatePatternOffset(LineHandle handle, float offset);

        /// <summary>
        /// 線のパターンオフセット速度を更新する。
        /// </summary>
        bool UpdatePatternOffsetVelocity(LineHandle handle, float velocity);

        /// <summary>
        /// 線の基準幅を更新する。
        /// </summary>
        bool UpdateBaseWidth(LineHandle handle, float width);

        /// <summary>
        /// 指定したハンドルのMaterialFxServiceを取得する。
        /// </summary>
        IMaterialFxService TryGetMaterialFx(LineHandle handle);

        bool Release(LineHandle handle);
        void ClearAll();
    }

    public interface ILineDrawSettings
    {
        LineSpace DefaultSpace { get; }
        LineStyle DefaultStyle { get; }
        int MaxLineCount { get; }
        int MaxVertexCount { get; }
        float MinSegmentLength { get; }
        float GeometryQuality { get; }
        float AdaptiveQualityScale { get; }
        bool UseUnscaledTime { get; }
        bool AutoDrawOnAcquire { get; }
        LineSpace AutoDrawSpace { get; }
        LineStyle AutoDrawStyle { get; }
        bool AutoDrawClosed { get; }
        IReadOnlyList<Vector3> AutoDrawPoints { get; }
    }

    public interface ILineDrawMaterialSettings
    {
        Material WorldMaterial { get; }
        Material UiMaterial { get; }
    }
}

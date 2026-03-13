#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Commands.VNext;
using Game.MaterialFx;
using Game.Visual;
using UnityEngine;

namespace Game.Background
{
    public interface IBackgroundSystem
    {
        int LayerCount { get; }
        bool TryGetLayerState(int index, out BackgroundLayerState state);
        void SetLayerOffset(int index, Vector2 offset);
        void AddLayerOffset(int index, Vector2 delta);
        void SetLayerScrollSpeed(int index, Vector2 speed);
        void MarkDirty();

        /// <summary>Layer を名前で検索し、見つかった場合は index を返す。</summary>
        bool TryGetLayerIndexByName(string name, out int index);

        /// <summary>Layer の一時停止状態を設定する。</summary>
        void SetLayerPaused(int index, bool paused);

        /// <summary>Layer の有効 / 無効を設定する（無効時はタイルを非表示にする）。</summary>
        void SetLayerEnabled(int index, bool enabled);

        /// <summary>Layer 内の各要素の LTS にアクセスしてコマンドを実行する。</summary>
        UniTask ExecuteOnLayerElementsAsync(int index, CommandListData commands, IVarStore vars, CancellationToken ct);

        /// <summary>Layer 内の各要素に対して VisualSystem 経由で MaterialFx を適用する。</summary>
        void SetLayerMaterialFx(int index, VisualTargetSelector selector, IReadOnlyList<MaterialFxPresetEntry> entries, bool clearMissingKeys, int basePriority);
    }

    public interface IBackgroundElementAdapter
    {
        void Initialize(in BackgroundElementContext context);
        void Apply(in BackgroundElementContext context);
    }

    public interface IBackgroundElementAdapterOptions
    {
        RectTransform? RectTransform { get; }
        SpriteRenderer? SpriteRenderer { get; }
        bool ApplyRectTransformSize { get; }
        bool ApplySpriteRendererSize { get; }
        bool ApplySortingOrder { get; }
        int SortingOrderOffset { get; }
    }
}

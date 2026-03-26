#nullable enable
using Game.Channel;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI.TraitList
{
    [CreateAssetMenu(
        fileName = "UITraitListLayoutProfile",
        menuName = "Game/UI/Trait List/Layout Profile")]
    public sealed class UITraitListLayoutProfileSO : ScriptableObject
    {
        [BoxGroup("Grid")]
        [MinValue(1)]
        [Tooltip("1 ページ内で使用する行数。Order と合わせて、Trait を何行に分けて配置するかを決めます。")]
        public int Rows = 1;

        [BoxGroup("Grid")]
        [MinValue(1)]
        [Tooltip("1 ページ内で使用する列数。Rows と組み合わせてグリッドの最大表示数が決まります。")]
        public int Columns = 1;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        [Tooltip("行どうしの間隔。RectTransform 上の anchored position を計算するときに縦方向へ加算されます。")]
        public float RowSpacing = 0f;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        [Tooltip("列どうしの間隔。RectTransform 上の anchored position を計算するときに横方向へ加算されます。")]
        public float ColumnSpacing = 0f;

        [BoxGroup("Order")]
        [Tooltip("Trait を row 優先で並べるか、column 優先で並べるかを決めます。表示順と row/column の割り当てに影響します。")]
        public UITraitListOrder Order = UITraitListOrder.RowMajor;

        [BoxGroup("Origin")]
        [LabelText("Item Offset")]
        [Tooltip("各アイテム配置の基準点オフセット。Area 側の開始位置を求めたあとに、この値を全スロットへ加算します。")]
        public Vector2 Offset = Vector2.zero;

        [BoxGroup("Origin")]
        [LabelText("Item Horizontal Origin")]
        [Tooltip("各アイテム自身の横方向基準。Left/Center/Right のどこを起点に座標を計算するかを決めます。")]
        public UITraitListHorizontalAlignment HorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Origin")]
        [LabelText("Item Vertical Origin")]
        [Tooltip("各アイテム自身の縦方向基準。Top/Middle/Bottom のどこを起点に座標を計算するかを決めます。")]
        public UITraitListVerticalAlignment VerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Placement Area")]
        [LabelText("Area Horizontal Start")]
        [Tooltip("配置領域全体の横方向開始位置。Layout Rect の left/center/right のどこからグリッドを敷き始めるかを決めます。")]
        public UITraitListHorizontalAlignment AreaHorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Placement Area")]
        [LabelText("Area Vertical Start")]
        [Tooltip("配置領域全体の縦方向開始位置。Layout Rect の top/middle/bottom のどこからグリッドを敷き始めるかを決めます。")]
        public UITraitListVerticalAlignment AreaVerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Relayout Animation")]
        [ToggleLeft]
        [Tooltip("再レイアウト時に TransformAnimationChannel を使ってスロット移動をアニメーションさせます。false の場合は即座に位置更新します。")]
        public bool UseTransformAnimation = false;

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        [Tooltip("再レイアウト時に使う TransformAnimationChannel のタグ。対象 Runtime / UI 側で同じ tag の channel が必要です。")]
        public string ChannelTag = "default";

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        [Tooltip("true の場合、再レイアウト完了まで待ってから後続処理へ進みます。false の場合はアニメーション開始だけ行って即 return します。")]
        public bool WaitForCompletion = false;
    }
}

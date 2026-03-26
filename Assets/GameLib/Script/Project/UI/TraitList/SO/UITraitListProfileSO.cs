#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI.TraitList
{
    [CreateAssetMenu(
        fileName = "UITraitListProfile",
        menuName = "Game/UI/Trait List/Profile")]
    public sealed class UITraitListProfileSO : ScriptableObject
    {
        [BoxGroup("Profile")]
        [AssetOrInternal]
        [Tooltip("並べ方・座標計算・アニメーション設定をまとめた Layout Profile。スロットの row/column と anchored position の決定に使います。")]
        public UITraitListLayoutProfileSO? LayoutProfile;

        [BoxGroup("Profile")]
        [AssetOrInternal]
        [Tooltip("見た目の生成方法をまとめた Visualizer Profile。Prefab / RuntimeTemplate のどちらで作るか、spawn 後コマンドなどを定義します。")]
        public UITraitListVisualizerProfileSO? VisualizerProfile;

        [BoxGroup("Profile")]
        [InlineProperty]
        [Tooltip("この Profile を使って Build したときの既定表示範囲。コマンドや Runtime 側で明示指定されない場合に使われます。")]
        public UITraitListRange DefaultRange;
    }
}

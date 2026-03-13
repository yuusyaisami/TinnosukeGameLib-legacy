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
        public int Rows = 1;

        [BoxGroup("Grid")]
        [MinValue(1)]
        public int Columns = 1;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        public float RowSpacing = 0f;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        public float ColumnSpacing = 0f;

        [BoxGroup("Order")]
        public UITraitListOrder Order = UITraitListOrder.RowMajor;

        [BoxGroup("Origin")]
        public Vector2 Offset = Vector2.zero;

        [BoxGroup("Relayout Animation")]
        [ToggleLeft]
        public bool UseTransformAnimation = false;

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        public string ChannelTag = "default";

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        public TransformAnimationPreset? MovePreset;

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        public bool WaitForCompletion = false;
    }
}

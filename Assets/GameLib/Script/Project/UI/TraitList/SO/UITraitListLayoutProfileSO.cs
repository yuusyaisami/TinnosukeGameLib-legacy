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
        [LabelText("Item Offset")]
        public Vector2 Offset = Vector2.zero;

        [BoxGroup("Origin")]
        [LabelText("Item Horizontal Origin")]
        public UITraitListHorizontalAlignment HorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Origin")]
        [LabelText("Item Vertical Origin")]
        public UITraitListVerticalAlignment VerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Placement Area")]
        [LabelText("Area Horizontal Start")]
        public UITraitListHorizontalAlignment AreaHorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Placement Area")]
        [LabelText("Area Vertical Start")]
        public UITraitListVerticalAlignment AreaVerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Relayout Animation")]
        [ToggleLeft]
        public bool UseTransformAnimation = false;

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        public string ChannelTag = "default";

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        [HideInInspector]
        public TransformAnimationPreset? MovePreset; // Legacy: trait-specific preset is resolved from TraitDefinitionSO.

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        public bool WaitForCompletion = false;
    }
}

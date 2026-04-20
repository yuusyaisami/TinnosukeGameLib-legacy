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
        [Tooltip("Inspector setting.")]
        public int Rows = 1;

        [BoxGroup("Grid")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        public int Columns = 1;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        [Tooltip("Inspector setting.")]
        public float RowSpacing = 0f;

        [BoxGroup("Spacing")]
        [MinValue(0)]
        [Tooltip("Inspector setting.")]
        public float ColumnSpacing = 0f;

        [BoxGroup("Order")]
        [Tooltip("Inspector setting.")]
        public UITraitListOrder Order = UITraitListOrder.RowMajor;

        [BoxGroup("Origin")]
        [LabelText("Item Offset")]
        [Tooltip("Inspector setting.")]
        public Vector2 Offset = Vector2.zero;

        [BoxGroup("Origin")]
        [LabelText("Item Horizontal Origin")]
        [Tooltip("Inspector setting.")]
        public UITraitListHorizontalAlignment HorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Origin")]
        [LabelText("Item Vertical Origin")]
        [Tooltip("Inspector setting.")]
        public UITraitListVerticalAlignment VerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Placement Area")]
        [LabelText("Area Horizontal Start")]
        [Tooltip("Inspector setting.")]
        public UITraitListHorizontalAlignment AreaHorizontalAlignment = UITraitListHorizontalAlignment.Left;

        [BoxGroup("Placement Area")]
        [LabelText("Area Vertical Start")]
        [Tooltip("Inspector setting.")]
        public UITraitListVerticalAlignment AreaVerticalAlignment = UITraitListVerticalAlignment.Top;

        [BoxGroup("Relayout Animation")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool UseTransformAnimation = false;

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Relayout Animation")]
        [ShowIf(nameof(UseTransformAnimation))]
        [Tooltip("Inspector setting.")]
        public bool WaitForCompletion = false;
    }
}

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
        [Tooltip("Inspector setting.")]
        public UITraitListLayoutProfileSO? LayoutProfile;

        [BoxGroup("Profile")]
        [AssetOrInternal]
        [Tooltip("Inspector setting.")]
        public UITraitListVisualizerProfileSO? VisualizerProfile;

        [BoxGroup("Profile")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public UITraitListRange DefaultRange;
    }
}

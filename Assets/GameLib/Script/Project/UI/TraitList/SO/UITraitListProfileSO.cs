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
        public UITraitListLayoutProfileSO? LayoutProfile;

        [BoxGroup("Profile")]
        [AssetOrInternal]
        public UITraitListVisualizerProfileSO? VisualizerProfile;

        [BoxGroup("Profile")]
        [InlineProperty]
        public UITraitListRange DefaultRange;
    }
}

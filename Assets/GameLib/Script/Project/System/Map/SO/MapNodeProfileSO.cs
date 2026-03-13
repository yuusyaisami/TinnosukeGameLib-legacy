#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MapNode
{
    [CreateAssetMenu(
        fileName = "MapNodeProfile",
        menuName = "Game/MapNode/Profile")]
    public sealed class MapNodeProfileSO : ScriptableObject
    {
        [BoxGroup("Settings")]
        [AssetOrInternal]
        public MapNodeGenerateSettingsSO? Generate;

        [BoxGroup("Settings")]
        [AssetOrInternal]
        public MapNodeVisualizeSettingsSO? Visualize;

        [BoxGroup("Settings")]
        public MapNodeFailurePolicy FailurePolicy = MapNodeFailurePolicy.FailFast;
    }
}

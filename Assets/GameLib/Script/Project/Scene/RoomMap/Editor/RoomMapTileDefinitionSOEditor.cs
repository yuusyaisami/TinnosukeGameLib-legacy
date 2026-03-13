#nullable enable
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Game.RoomMap.Editor
{
    [CustomEditor(typeof(RoomMapTileDefinitionSO))]
    public sealed class RoomMapTileDefinitionSOEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            // UI一貫性のため Odin 標準描画に統一。
            base.OnInspectorGUI();
        }
    }
}

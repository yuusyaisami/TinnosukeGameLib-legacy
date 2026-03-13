#if UNITY_EDITOR
#nullable enable
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Channel.Editor
{
    [CustomEditor(typeof(TransformAnimationHubMB))]
    public sealed class TransformAnimationHubMBEditor : OdinEditor
    {
        void OnSceneGUI()
        {
            if (target is not TransformAnimationHubMB hub || hub == null)
                return;

            hub.DrawSceneHandles();
            if (GUI.changed)
                Repaint();
        }
    }
}
#endif

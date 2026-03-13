#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.Foundation
{
    /// <summary>ツリー描画のための共通状態（選択、検索、折りたたみ）。</summary>
    [Serializable]
    public sealed class TreeExplorerState
    {
        public string SelectedPath;
        public string SearchText;
        public Vector2 Scroll;
        public string PendingDragPath;
        public double PendingDragStartTime;
        public string DraggingPath;
        public string DropHoverPath;
        public TreeDropPosition DropHoverPosition;
        public Vector2 MouseDownPosition;

        readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);

        public bool IsCollapsed(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return _collapsed.Contains(path);
        }

        public void SetCollapsed(string path, bool collapsed)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (collapsed) _collapsed.Add(path);
            else _collapsed.Remove(path);
        }

        public void Toggle(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!_collapsed.Add(path))
                _collapsed.Remove(path);
        }

        public void ClearCollapse() => _collapsed.Clear();

        public void ClearDrag()
        {
            PendingDragPath = null;
            PendingDragStartTime = 0;
            DraggingPath = null;
            DropHoverPath = null;
            DropHoverPosition = TreeDropPosition.Inside;
        }
    }
}
#endif

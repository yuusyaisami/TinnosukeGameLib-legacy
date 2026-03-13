#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Small IMGUI grid paint helper (mouse down / drag).</summary>
    public static class GridPaintIMGUI
    {
        public enum Tool
        {
            Pen = 0,
            Eraser = 1,
            Bucket = 2,
        }

        public static void ForEachCell(Rect gridRect, int width, int height, Action<int, int, Rect> draw)
        {
            if (width <= 0 || height <= 0)
                return;

            var cellW = gridRect.width / width;
            var cellH = gridRect.height / height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var r = new Rect(gridRect.x + x * cellW, gridRect.y + y * cellH, cellW, cellH);
                    draw(x, y, r);
                }
            }
        }

        public static Vector2Int MouseToCell(Vector2 mouse, Rect gridRect, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return new Vector2Int(-1, -1);

            var local = mouse - new Vector2(gridRect.x, gridRect.y);
            var x = Mathf.FloorToInt(local.x / (gridRect.width / width));
            var y = Mathf.FloorToInt(local.y / (gridRect.height / height));
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        /// <summary>
        /// Handles left-click painting on a grid rect.
        /// Returns true when paint action was invoked.
        /// </summary>
        public static bool HandlePaintEvents(
            Rect gridRect,
            int width,
            int height,
            ref Vector2Int lastPainted,
            Action<int, int> paint)
        {
            var e = Event.current;
            if (e == null)
                return false;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)
                return false;

            if (e.button != 0)
                return false;

            if (!gridRect.Contains(e.mousePosition))
                return false;

            var cell = MouseToCell(e.mousePosition, gridRect, width, height);
            if ((uint)cell.x >= (uint)width || (uint)cell.y >= (uint)height)
                return false;

            if (e.type == EventType.MouseDrag && cell == lastPainted)
                return false;

            lastPainted = cell;
            paint(cell.x, cell.y);
            e.Use();
            return true;
        }

        public static void ApplyTool<TCell>(
            Tool tool,
            int startX,
            int startY,
            int width,
            int height,
            Func<int, int, TCell> get,
            Action<int, int, TCell> set,
            Func<TCell, TCell, bool> equals,
            TCell paintValue)
        {
            if (width <= 0 || height <= 0)
                return;
            if ((uint)startX >= (uint)width || (uint)startY >= (uint)height)
                return;

            if (tool != Tool.Bucket)
            {
                set(startX, startY, paintValue);
                return;
            }

            // Flood fill (4-neighbor)
            var target = get(startX, startY);
            if (equals(target, paintValue))
                return;

            var visited = new bool[width * height];
            var q = new Queue<Vector2Int>(Mathf.Min(width * height, 32));
            q.Enqueue(new Vector2Int(startX, startY));

            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if ((uint)p.x >= (uint)width || (uint)p.y >= (uint)height)
                    continue;

                var idx = p.y * width + p.x;
                if ((uint)idx >= (uint)visited.Length)
                    continue;

                if (visited[idx])
                    continue;
                visited[idx] = true;

                var cur = get(p.x, p.y);
                if (!equals(cur, target))
                    continue;

                set(p.x, p.y, paintValue);

                q.Enqueue(new Vector2Int(p.x + 1, p.y));
                q.Enqueue(new Vector2Int(p.x - 1, p.y));
                q.Enqueue(new Vector2Int(p.x, p.y + 1));
                q.Enqueue(new Vector2Int(p.x, p.y - 1));
            }
        }
    }
}

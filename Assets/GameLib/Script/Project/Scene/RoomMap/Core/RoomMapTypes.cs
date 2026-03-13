#nullable enable
using System;
using UnityEngine;

namespace Game.RoomMap
{
    public enum RoomMapOriginMode
    {
        TopLeft = 0,
        BottomLeft = 1,
    }

    public enum RoomMapVisualOrder
    {
        RowMajor_TopLeft = 0,
        RowMajor_TopRight = 1,
        RowMajor_BottomLeft = 2,
        RowMajor_BottomRight = 3,
        Diagonal_TopLeft = 10,
        Diagonal_BottomLeft = 11,
    }

    public enum RoomMapFailurePolicy
    {
        ContinueOnError = 0,
        FailFast = 1,
    }

    public static class RoomMapTransformUtility
    {
        public static Vector3 CellToWorld(RoomMapProfileSO profile, int x, int y)
        {
            var cellSize = profile.CellSize;
            var rot = Quaternion.Euler(0f, 0f, profile.BaseRotationDegZ);
            var cellOffset = profile.LocalCellOffset;

            var local = profile.OriginMode == RoomMapOriginMode.BottomLeft
                ? new Vector3(x * cellSize.x, y * cellSize.y, 0f)
                : new Vector3(x * cellSize.x, -y * cellSize.y, 0f);

            local += new Vector3(cellOffset.x, cellOffset.y, 0f);

            return profile.BasePosition + (rot * local);
        }
    }

    public static class RoomMapHashUtility
    {
        // FNV-1a 64
        public static ulong Hash64(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 14695981039346656037UL;

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            var hash = offsetBasis;
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash;
        }

        public static ulong HashSchemaStableKeys(string[] stableKeys)
        {
            if (stableKeys == null || stableKeys.Length == 0)
                return Hash64(string.Empty);

            // Avoid allocations of string.Join on hot path; OnValidate only so simple join is fine.
            return Hash64(string.Join("|", stableKeys));
        }
    }
}

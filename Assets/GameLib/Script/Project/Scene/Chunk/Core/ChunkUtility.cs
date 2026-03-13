#nullable enable
using UnityEngine;

namespace Game.Chunk
{
    public static class ChunkCoordUtility
    {
        public static Vector2Int WorldToCell(Vector2 world, ChunkOriginSettings origin)
        {
            var local = world - origin.WorldOriginPosition;
            var size = origin.CellSize;
            if (origin.CenterAligned)
            {
                var x = Mathf.FloorToInt(local.x / size.x + 0.5f) + origin.WorldOriginCell.x;
                var y = Mathf.FloorToInt(local.y / size.y + 0.5f) + origin.WorldOriginCell.y;
                return new Vector2Int(x, y);
            }

            var nx = Mathf.FloorToInt(local.x / size.x) + origin.WorldOriginCell.x;
            var ny = Mathf.FloorToInt(local.y / size.y) + origin.WorldOriginCell.y;
            return new Vector2Int(nx, ny);
        }

        public static Vector2 CellToWorldCenter(Vector2Int cell, ChunkOriginSettings origin)
        {
            var size = origin.CellSize;
            var local = new Vector2(
                (cell.x - origin.WorldOriginCell.x) * size.x,
                (cell.y - origin.WorldOriginCell.y) * size.y);

            if (!origin.CenterAligned)
                local += new Vector2(size.x * 0.5f, size.y * 0.5f);

            return local + origin.WorldOriginPosition;
        }

        public static RectInt CalcChunkCellRect(ChunkCoord coord, ChunkOriginSettings origin, Vector2Int chunkSize)
        {
            var min = new Vector2Int(
                origin.ChunkZeroCell.x + coord.X * chunkSize.x,
                origin.ChunkZeroCell.y + coord.Y * chunkSize.y);
            return new RectInt(min, chunkSize);
        }

        public static ChunkCoord CellToChunkCoord(Vector2Int cell, ChunkOriginSettings origin, Vector2Int chunkSize)
        {
            var local = cell - origin.ChunkZeroCell;
            var cx = FloorDiv(local.x, chunkSize.x);
            var cy = FloorDiv(local.y, chunkSize.y);
            return new ChunkCoord(cx, cy);
        }

        public static Bounds CalcChunkWorldBounds(RectInt cellRect, ChunkOriginSettings origin)
        {
            var size = origin.CellSize;
            Vector2 min;
            Vector2 max;

            if (origin.CenterAligned)
            {
                var minCenter = CellToWorldCenter(new Vector2Int(cellRect.xMin, cellRect.yMin), origin);
                var maxCenter = CellToWorldCenter(new Vector2Int(cellRect.xMax - 1, cellRect.yMax - 1), origin);
                var half = new Vector2(size.x * 0.5f, size.y * 0.5f);
                min = minCenter - half;
                max = maxCenter + half;
            }
            else
            {
                min = new Vector2(
                    (cellRect.xMin - origin.WorldOriginCell.x) * size.x,
                    (cellRect.yMin - origin.WorldOriginCell.y) * size.y) + origin.WorldOriginPosition;
                max = new Vector2(
                    (cellRect.xMax - origin.WorldOriginCell.x) * size.x,
                    (cellRect.yMax - origin.WorldOriginCell.y) * size.y) + origin.WorldOriginPosition;
            }

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
            return bounds;
        }

        public static int ComputeChunkSeed(int baseSeed, ChunkCoord coord)
        {
            unchecked
            {
                var hash = baseSeed;
                hash = (hash * 397) ^ coord.X;
                hash = (hash * 397) ^ coord.Y;
                return hash;
            }
        }

        static int FloorDiv(int value, int divisor)
        {
            if (divisor == 0)
                return 0;

            var q = value / divisor;
            var r = value % divisor;
            if (r != 0 && value < 0)
                q--;
            return q;
        }
    }
}

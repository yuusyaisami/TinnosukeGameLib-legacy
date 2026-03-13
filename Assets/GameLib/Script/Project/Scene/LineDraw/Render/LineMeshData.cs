using System.Collections.Generic;
using UnityEngine;

namespace Game.LineDraw
{
    public sealed class LineMeshData
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector2> UVs = new();
        public readonly List<Color> Colors = new();
        public readonly List<int> Indices = new();

        public int VertexCount => Vertices.Count;

        public void Clear()
        {
            Vertices.Clear();
            UVs.Clear();
            Colors.Clear();
            Indices.Clear();
        }

        public void EnsureCapacity(int vertexCount, int indexCount)
        {
            if (vertexCount > 0)
            {
                if (Vertices.Capacity < vertexCount) Vertices.Capacity = vertexCount;
                if (UVs.Capacity < vertexCount) UVs.Capacity = vertexCount;
                if (Colors.Capacity < vertexCount) Colors.Capacity = vertexCount;
            }

            if (indexCount > 0 && Indices.Capacity < indexCount)
                Indices.Capacity = indexCount;
        }
    }
}

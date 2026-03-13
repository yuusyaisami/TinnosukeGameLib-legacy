using UnityEngine;

namespace Game.LineDraw
{
    public enum LineRenderBackendKind
    {
        World,
        UI
    }

    public interface ILineRenderBackend
    {
        LineRenderBackendKind Kind { get; }
        Transform RootTransform { get; }

        void SetActive(bool active);
        void ApplyMesh(LineMeshData data);
        void ApplyMaterial(Material material);
        void Dispose();
    }
}

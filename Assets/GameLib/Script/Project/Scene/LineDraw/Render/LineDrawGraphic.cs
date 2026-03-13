using UnityEngine;
using UnityEngine.UI;

namespace Game.LineDraw
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LineDrawGraphic : MaskableGraphic, ILineRenderBackend
    {
        LineMeshData _meshData;
        bool _needsRebuild;

        public LineRenderBackendKind Kind => LineRenderBackendKind.UI;
        public Transform RootTransform => transform;

        public override Texture mainTexture => Graphic.defaultGraphicMaterial?.mainTexture ?? Texture2D.whiteTexture;

        public void SetMeshData(LineMeshData data)
        {
            _meshData = data;
            _needsRebuild = !isActiveAndEnabled;
            SetVerticesDirty();
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);

            // IsActiveAndEnabled이 false인 경우、SetVerticesDirty가 효과 없으므로、
            // SetActive(true)時に SetAllDirty()를 호출할 필요가 있다
            if (active && _meshData != null)
            {
                SetAllDirty();
            }
        }

        public void ApplyMesh(LineMeshData data)
        {
            SetMeshData(data);
        }

        public void ApplyMaterial(Material material)
        {
            if (material != null)
                this.material = material;
        }

        public void Dispose()
        {
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_meshData == null)
                return;

            int vertexCount = _meshData.Vertices.Count;
            if (_meshData.UVs.Count < vertexCount)
                vertexCount = _meshData.UVs.Count;
            if (_meshData.Colors.Count < vertexCount)
                vertexCount = _meshData.Colors.Count;

            if (vertexCount <= 0)
                return;

            for (int i = 0; i < vertexCount; i++)
            {
                var vert = UIVertex.simpleVert;
                vert.position = _meshData.Vertices[i];
                vert.uv0 = _meshData.UVs[i];
                vert.color = _meshData.Colors[i];
                vh.AddVert(vert);
            }

            int indexCount = _meshData.Indices.Count;
            for (int i = 0; i + 2 < indexCount; i += 3)
            {
                int a = _meshData.Indices[i];
                int b = _meshData.Indices[i + 1];
                int c = _meshData.Indices[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
                    continue;
                vh.AddTriangle(a, b, c);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_needsRebuild && _meshData != null)
            {
                _needsRebuild = false;
                SetAllDirty();
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (_meshData != null)
                SetAllDirty();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            if (_meshData != null)
                SetAllDirty();
        }
    }
}

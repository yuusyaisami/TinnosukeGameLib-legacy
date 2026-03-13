using UnityEngine;

namespace Game.LineDraw
{
    public sealed class LineMeshRendererBackend : ILineRenderBackend
    {
        readonly GameObject _gameObject;
        readonly MeshFilter _meshFilter;
        readonly MeshRenderer _meshRenderer;
        readonly Mesh _mesh;

        public LineRenderBackendKind Kind => LineRenderBackendKind.World;
        public Transform RootTransform => _gameObject != null ? _gameObject.transform : null;
        public MeshRenderer Renderer => _meshRenderer;

        public LineMeshRendererBackend(Transform parent, string name)
        {
            _gameObject = new GameObject(name);
            var t = _gameObject.transform;
            if (parent != null)
                t.SetParent(parent, false);

            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            _meshRenderer = _gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = "LineMesh"
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        public void SetActive(bool active)
        {
            if (_gameObject != null)
                _gameObject.SetActive(active);
        }

        public void ApplyMesh(LineMeshData data)
        {
            if (_mesh == null)
                return;

            if (data == null || data.Vertices.Count == 0)
            {
                _mesh.Clear();
                return;
            }

            if (data.UVs.Count != data.Vertices.Count || data.Colors.Count != data.Vertices.Count)
            {
                _mesh.Clear();
                return;
            }

            _mesh.Clear();
            _mesh.SetVertices(data.Vertices);
            _mesh.SetUVs(0, data.UVs);
            _mesh.SetColors(data.Colors);
            _mesh.SetTriangles(data.Indices, 0, true);
        }

        public void ApplyMaterial(Material material)
        {
            if (_meshRenderer == null)
                return;

            if (material != null)
                _meshRenderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                Object.Destroy(_mesh);
            }

            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
            }
        }
    }
}

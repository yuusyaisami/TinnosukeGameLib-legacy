#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.VariableLayer;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshCompositeVisualObject : IDisposable
    {
        static readonly IMeshMaterialFxServiceFactory MaterialFxFactory = new MeshMaterialFxServiceFactory();
        static readonly MeshPolygonSyncSettings DefaultContourSyncSettings = new();

        readonly Transform _ownerTransform;
        readonly GameObject _rootObject;
        readonly MeshFilter _meshFilter;
        readonly MeshRenderer _meshRenderer;
        readonly PolygonCollider2D _polygonCollider;
        readonly MeshChannelColliderRelay _hitRelay;
        readonly Mesh _mesh;
        readonly IMeshMaterialFxService _materialFx;
        readonly List<Vector2[]> _lastPaths = new();

        int _lastSyncFrame = int.MinValue;

        public MeshCompositeVisualObject(string name, Transform ownerTransform)
        {
            _ownerTransform = ownerTransform;
            _rootObject = new GameObject(name);
            _rootObject.transform.SetParent(ownerTransform, false);

            _meshFilter = _rootObject.AddComponent<MeshFilter>();
            _meshRenderer = _rootObject.AddComponent<MeshRenderer>();
            _polygonCollider = _rootObject.AddComponent<PolygonCollider2D>();
            _polygonCollider.pathCount = 0;
            _polygonCollider.enabled = false;
            _hitRelay = _rootObject.AddComponent<MeshChannelColliderRelay>();

            _mesh = new Mesh
            {
                name = $"{name}.Mesh",
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
            _materialFx = MaterialFxFactory.CreateForMeshRenderer(_meshRenderer);
        }

        public void CaptureHits(List<MeshHitContactInfo> output)
        {
            _hitRelay.CaptureHits(output);
        }

        public void Apply(
            MeshCompositeDraft draft,
            MeshRenderPipelinePreset pipeline,
            Material fallbackMaterial,
            float deltaTime,
            int frameIndex)
        {
            if (draft.Paths.Count == 0)
            {
                Clear();
                return;
            }

            var colliderPreset = draft.ColliderPreset;
            var materialPreset = draft.MaterialPreset;
            var localPaths = MeshChannelGeometryUtility.ConvertWorldPathsToLocal(_ownerTransform, draft.Paths);
            var syncSettings = colliderPreset?.Sync ?? DefaultContourSyncSettings;
            var simplifiedPaths = MeshChannelGeometryUtility.SimplifyPaths(localPaths, syncSettings);

            if (colliderPreset != null)
            {
                var shouldSync = MeshChannelGeometryUtility.ShouldSyncPaths(
                    _lastPaths,
                    simplifiedPaths,
                    colliderPreset.Sync,
                    frameIndex,
                    _lastSyncFrame);

                if (shouldSync)
                {
                    SyncColliderPaths(simplifiedPaths);
                    _lastSyncFrame = frameIndex;
                }

                _polygonCollider.enabled = colliderPreset.SyncPolygonToCollider || colliderPreset.EnableHitCapture;
            }
            else
            {
                DisableCollider();
            }

            _meshRenderer.enabled = pipeline.EnableVisual;
            _materialFx.Update(
                materialPreset,
                materialPreset.Material != null ? materialPreset.Material : fallbackMaterial,
                pipeline.SortingOrder + materialPreset.SortingOrderOffset,
                simplifiedPaths,
                deltaTime);

            if (pipeline.EnableVisual)
                RebuildMesh(simplifiedPaths);
            else
                _mesh.Clear();
        }

        public void AdvanceMaterial(float deltaTime)
        {
            _materialFx.Advance(deltaTime);
        }

        public bool SetMaterialEntry(int nodeId, string layerTag, VariableLayerValue value, float lifetimeSeconds = -1f)
        {
            return _materialFx.SetEntry(nodeId, layerTag, value, lifetimeSeconds);
        }

        public bool SetMaterialEntryFade(int nodeId, string layerTag, VariableLayerValue value, float durationSeconds, Ease ease, float lifetimeSeconds = -1f)
        {
            return _materialFx.SetEntryFade(nodeId, layerTag, value, durationSeconds, ease, lifetimeSeconds);
        }

        public bool RemoveMaterialTag(int nodeId, string layerTag)
        {
            return _materialFx.RemoveTag(nodeId, layerTag);
        }

        public bool ClearMaterialContext(string layerTag)
        {
            return _materialFx.ClearContext(layerTag);
        }

        public bool ClearMaterialNode(int nodeId)
        {
            return _materialFx.ClearNode(nodeId);
        }

        public void ResetMaterialDefaults()
        {
            _materialFx.ResetDefaults();
        }

        public void Clear()
        {
            _mesh.Clear();
            DisableCollider();
            _meshRenderer.enabled = false;
            _hitRelay.ClearAll();
        }

        public void Dispose()
        {
            _materialFx.Dispose();
            if (_mesh != null)
                UnityEngine.Object.Destroy(_mesh);
            if (_rootObject != null)
                UnityEngine.Object.Destroy(_rootObject);
        }

        void SyncColliderPaths(List<Vector2[]> paths)
        {
            _polygonCollider.pathCount = paths.Count;
            _lastPaths.Clear();

            for (var i = 0; i < paths.Count; i++)
            {
                _polygonCollider.SetPath(i, paths[i]);
                var copy = new Vector2[paths[i].Length];
                Array.Copy(paths[i], copy, copy.Length);
                _lastPaths.Add(copy);
            }
        }

        void RebuildMesh(List<Vector2[]> paths)
        {
            MeshChannelGeometryUtility.BuildFallbackMesh(paths, _mesh);
        }

        void DisableCollider()
        {
            _polygonCollider.pathCount = 0;
            _polygonCollider.enabled = false;
            _hitRelay.ClearAll();
            _lastPaths.Clear();
            _lastSyncFrame = int.MinValue;
        }
    }
}

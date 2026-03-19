#nullable enable
using DG.Tweening;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.MaterialFx;
using Game.MaterialFx.Generated;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    interface IMeshFxVisualService
    {
        void OnAcquire();
        void OnRelease();
        bool ApplyGeometry(MeshFxGeometryFrame frame);
        void SetVisible(bool visible);
    }

    sealed class MeshFxVisualService : IMeshFxVisualService, System.IDisposable
    {
        const string BasePresetContext = "MeshFx.BaseShaderPreset";
        const string EntryPresetContext = "MeshFx.ChannelPreset";
        const string DefaultRenderStateContext = "MeshFx.RenderStateDefault";

        readonly MeshFxChannelDef _def;
        readonly Transform? _ownerTransform;
        readonly IMaterialFxServiceFactory? _materialFxFactory;

        readonly List<Vector3> _localVertexScratch = new(512);

        GameObject? _root;
        MeshFilter? _meshFilter;
        MeshRenderer? _meshRenderer;
        Mesh? _mesh;
        IMaterialFxService? _materialFx;
        Material? _ownedFallbackMaterial;

        bool _acquired;
        bool _disposed;
        bool _visible;

        public MeshFxVisualService(
            MeshFxChannelDef def,
            Transform? ownerTransform,
            IMaterialFxServiceFactory? materialFxFactory)
        {
            _def = def;
            _ownerTransform = ownerTransform;
            _materialFxFactory = materialFxFactory;
        }

        public void OnAcquire()
        {
            if (_disposed)
                return;

            _acquired = true;
            EnsureCreated();
            ApplyMaterialConfiguration();
            SetVisible(true);
        }

        public void OnRelease()
        {
            if (_disposed)
                return;

            _acquired = false;
            SetVisible(false);
            if (_mesh != null)
                _mesh.Clear();

            if (_materialFx != null)
            {
                _materialFx.Dispose();
                _materialFx = null;
            }
        }

        public bool ApplyGeometry(MeshFxGeometryFrame frame)
        {
            if (_disposed)
                return false;
            if (!_acquired)
                return false;

            EnsureCreated();
            if (_mesh == null)
                return false;

            if (frame == null || !frame.HasMesh)
            {
                _mesh.Clear();
                SetVisible(false);
                return false;
            }

            _localVertexScratch.Clear();
            if (_ownerTransform != null)
            {
                for (int i = 0; i < frame.Vertices.Count; i++)
                {
                    _localVertexScratch.Add(_ownerTransform.InverseTransformPoint(frame.Vertices[i]));
                }
            }
            else
            {
                _localVertexScratch.AddRange(frame.Vertices);
            }

            _mesh.Clear();
            _mesh.SetVertices(_localVertexScratch);
            _mesh.SetUVs(0, frame.UV);
            _mesh.SetTriangles(frame.Triangles, 0, true);
            _mesh.RecalculateBounds();

            SetVisible(true);
            return true;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed)
                return;
            if (_root == null)
                return;
            if (_visible == visible)
                return;

            _visible = visible;
            _root.SetActive(visible);
        }

        public bool SetMaterialLayer(
            string stableKey,
            string contextTag,
            MaterialFxTypedValue value,
            MaterialFxBlendMode blendMode,
            float durationSeconds,
            Ease easing,
            int priority = 0,
            float lifetimeSeconds = -1f)
        {
            if (_disposed)
                return false;
            if (!_acquired)
                return false;
            if (string.IsNullOrWhiteSpace(stableKey))
                return false;
            if (_materialFx == null)
                return false;

            if (durationSeconds > 0f)
            {
                _materialFx.SetLayerFade(
                    stableKey,
                    contextTag,
                    value,
                    durationSeconds,
                    easing,
                    blendMode,
                    priority,
                    lifetimeSeconds);
            }
            else
            {
                _materialFx.SetLayer(
                    stableKey,
                    contextTag,
                    value,
                    blendMode,
                    priority,
                    lifetimeSeconds);
            }

            return true;
        }

        public bool ClearMaterialContext(string contextTag)
        {
            if (_disposed)
                return false;
            if (_materialFx == null)
                return false;

            _materialFx.ClearContext(contextTag);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            OnRelease();
            _disposed = true;

            if (_mesh != null)
            {
                Object.Destroy(_mesh);
                _mesh = null;
            }

            if (_ownedFallbackMaterial != null)
            {
                Object.Destroy(_ownedFallbackMaterial);
                _ownedFallbackMaterial = null;
            }

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _meshFilter = null;
            _meshRenderer = null;
        }

        void EnsureCreated()
        {
            if (_root != null)
                return;

            _root = new GameObject($"MeshFx[{_def.Tag}]");
            var rootTransform = _root.transform;
            if (_ownerTransform != null)
                rootTransform.SetParent(_ownerTransform, false);

            _meshFilter = _root.AddComponent<MeshFilter>();
            _meshRenderer = _root.AddComponent<MeshRenderer>();

            _mesh = new Mesh
            {
                name = $"MeshFxMesh[{_def.Tag}]"
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;

            EnsureRendererMaterial();
        }

        void EnsureRendererMaterial()
        {
            if (_meshRenderer == null)
                return;

            if (_meshRenderer.sharedMaterial != null)
                return;

            if (MaterialFxService.BaseMaterial != null)
            {
                _meshRenderer.sharedMaterial = MaterialFxService.BaseMaterial;
                return;
            }

            var fallbackShader = Shader.Find("Sprites/Default");
            if (fallbackShader == null)
                return;

            _ownedFallbackMaterial = new Material(fallbackShader)
            {
                name = $"MeshFxFallbackMat[{_def.Tag}]"
            };
            _meshRenderer.sharedMaterial = _ownedFallbackMaterial;
        }

        void ApplyMaterialConfiguration()
        {
            if (_meshRenderer == null)
                return;

            BindSourceTextureIfNeeded();

            if (_materialFx != null)
            {
                _materialFx.Dispose();
                _materialFx = null;
            }

            if (_def.ApplyMaterialFx && _materialFxFactory != null)
            {
                var materialInstance = _meshRenderer.material;
                if (materialInstance != null)
                {
                    _materialFx = _materialFxFactory.CreateForMaterial(materialInstance);
                }
            }

            if (_materialFx != null)
            {
                ApplyDefaultRenderStateLayer();

                var basePreset = _def.BaseShaderPreset;
                if (basePreset != null)
                {
                    basePreset.RefreshEntries();
                    _materialFx.ApplyPreset(BasePresetContext, ResolveMaterialFxEntries(basePreset.Entries));
                }

                if (_def.MaterialFxPresetEntries != null && _def.MaterialFxPresetEntries.Count > 0)
                {
                    _materialFx.ApplyPreset(EntryPresetContext, ResolveMaterialFxEntries(_def.MaterialFxPresetEntries));
                }
                return;
            }

            ApplyFallbackRenderStateDirect();
        }

        void ApplyDefaultRenderStateLayer()
        {
            if (_materialFx == null)
                return;

            const int defaultPriority = -100;
            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.BlendPreset,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromInt(2), // AdditiveAlpha
                MaterialFxBlendMode.Override,
                defaultPriority);

            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.SrcBlend,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromInt(5), // SrcAlpha
                MaterialFxBlendMode.Override,
                defaultPriority);

            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.DstBlend,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromInt(1), // One
                MaterialFxBlendMode.Override,
                defaultPriority);

            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.ZWrite,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromBool(false),
                MaterialFxBlendMode.Override,
                defaultPriority);

            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.Cull,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromInt(0), // Off
                MaterialFxBlendMode.Override,
                defaultPriority);

            _materialFx.SetLayer(
                MaterialFxKeys.BaseShader.RenderState.QueueOffset,
                DefaultRenderStateContext,
                MaterialFxTypedValue.FromInt(_def.QueueOffset),
                MaterialFxBlendMode.Override,
                defaultPriority);
        }

        IReadOnlyList<MaterialFxPresetEntry> ResolveMaterialFxEntries(IReadOnlyList<MaterialFxPresetEntry> entries)
        {
            var ownerScope = ResolveOwnerScope();
            if (ownerScope == null)
                return entries;

            var resolver = ownerScope.Resolver;
            var vars = resolver != null && resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null
                ? resolvedVars
                : NullVarStore.Instance;
            var context = new SimpleDynamicContext(vars, ownerScope);
            var resolved = new MaterialFxPresetEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                resolved[i] = entries[i].Resolve(context);
            return resolved;
        }

        IScopeNode? ResolveOwnerScope()
        {
            if (_ownerTransform == null)
                return null;

            for (var current = _ownerTransform; current != null; current = current.parent)
            {
                if (current.TryGetComponent<BaseLifetimeScope>(out var baseScope) && baseScope != null)
                    return baseScope;

                if (current.TryGetComponent<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                    return runtimeScope;

                var components = current.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IScopeNode scopeNode)
                        return scopeNode;
                }
            }

            return null;
        }

        void ApplyFallbackRenderStateDirect()
        {
            if (_meshRenderer == null)
                return;

            var material = _meshRenderer.material;
            if (material == null)
                return;

            if (material.HasProperty("_FxBlendPreset"))
                material.SetInt("_FxBlendPreset", 1); // Additive
            if (material.HasProperty("_FxSrcBlend"))
                material.SetInt("_FxSrcBlend", 1); // One
            if (material.HasProperty("_FxDstBlend"))
                material.SetInt("_FxDstBlend", 1); // One
            if (material.HasProperty("_FxZWrite"))
                material.SetInt("_FxZWrite", 0);
            if (material.HasProperty("_FxCull"))
                material.SetInt("_FxCull", 0);
            if (material.HasProperty("_FxQueueOffset"))
                material.SetInt("_FxQueueOffset", _def.QueueOffset);
        }

        void BindSourceTextureIfNeeded()
        {
            if (_meshRenderer == null)
                return;

            var material = _meshRenderer.material;
            if (material == null)
                return;

            if (material.HasProperty("_MainTex"))
            {
                var sourceTexture = ResolveSourceTexture();
                if (sourceTexture != null)
                    material.SetTexture("_MainTex", sourceTexture);
            }

            if (material.HasProperty("_MaskTex") && material.GetTexture("_MaskTex") == null)
            {
                var sourceTexture = ResolveSourceTexture();
                if (sourceTexture != null)
                    material.SetTexture("_MaskTex", sourceTexture);
            }
        }

        Texture? ResolveSourceTexture()
        {
            if (_ownerTransform == null)
                return null;

            if (_ownerTransform.TryGetComponent<SpriteRenderer>(out var spriteRenderer) &&
                spriteRenderer != null &&
                spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite.texture;
            }

            if (_ownerTransform.TryGetComponent<Renderer>(out var renderer) &&
                renderer != null &&
                renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;
                if (mat.HasProperty("_MainTex"))
                    return mat.GetTexture("_MainTex");
            }

            return null;
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.SharedTexture;
using UnityEngine;
using VContainer.Unity;

namespace Game.TextureEffect
{
    public sealed class TextureEffectPipelineService
        : ITextureEffectPipeline,
          ITextureEffectLayerRegistry,
          ITextureEffectMaskRegistry,
          IScopeAcquireHandler,
          IScopeReleaseHandler,
          IScopeTickHandler,
          IDisposable
    {
        readonly ISharedTextureChannelHub _hub;
        readonly Dictionary<TextureEffectKind, ITextureEffect> _effects;

        readonly Dictionary<string, TextureEffectLayerDef> _layers = new();
        readonly List<TextureEffectLayerDef> _sortedLayers = new();
        readonly Dictionary<int, TextureEffectMaskEntry> _masks = new();
        readonly Dictionary<string, List<int>> _layerToMaskIds = new();

        // Mask RT pool (reused per frame)
        RenderTexture? _maskRT;
        RenderTexture? _tempRT;
        RenderTexture? _pingRT;
        RenderTexture? _pongRT;

        int _nextMaskId = 1;
        bool _layersDirty = true;
        bool _acquired;

        // Mask rendering material
        static Material? s_MaskMaterial;

        public int LayerCount => _layers.Count;

        public TextureEffectPipelineService(
            ISharedTextureChannelHub hub,
            IEnumerable<ITextureEffect> effects)
        {
            _hub = hub;
            _effects = new Dictionary<TextureEffectKind, ITextureEffect>();
            foreach (var e in effects)
                _effects[e.Kind] = e;
        }

        // ── Lifecycle ───────────────────────────────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
            ClearAll();
            ReleaseRTs();
        }

        public void Dispose()
        {
            ClearAll();
            ReleaseRTs();
        }

        // ── Layer Registry ──────────────────────────────────────

        public void RegisterLayer(in TextureEffectLayerDef layer)
        {
            _layers[layer.LayerTag] = layer;
            _layersDirty = true;
            if (!_layerToMaskIds.ContainsKey(layer.LayerTag))
                _layerToMaskIds[layer.LayerTag] = new List<int>();
        }

        public void UpdateLayer(string layerTag, in TextureEffectLayerDef layer)
        {
            if (!_layers.ContainsKey(layerTag))
                return;
            _layers[layerTag] = layer;
            _layersDirty = true;
        }

        public bool UnregisterLayer(string layerTag)
        {
            if (!_layers.Remove(layerTag))
                return false;

            // Remove associated masks
            if (_layerToMaskIds.TryGetValue(layerTag, out var maskIds))
            {
                foreach (var id in maskIds)
                    _masks.Remove(id);
                _layerToMaskIds.Remove(layerTag);
            }

            _layersDirty = true;
            return true;
        }

        public bool TryGetLayer(string layerTag, out TextureEffectLayerDef layer)
        {
            return _layers.TryGetValue(layerTag, out layer);
        }

        // ── Mask Registry ───────────────────────────────────────

        public int RegisterMask(in TextureEffectMaskEntry entry)
        {
            var id = _nextMaskId++;
            var entryWithId = entry;
            entryWithId.RegistrationId = id;

            _masks[id] = entryWithId;

            if (!_layerToMaskIds.TryGetValue(entry.LayerTag, out var list))
            {
                list = new List<int>();
                _layerToMaskIds[entry.LayerTag] = list;
            }
            list.Add(id);

            return id;
        }

        public void UpdateMask(int registrationId, in TextureEffectMaskEntry entry)
        {
            if (!_masks.ContainsKey(registrationId))
                return;
            var updated = entry;
            updated.RegistrationId = registrationId;
            _masks[registrationId] = updated;
        }

        public bool UnregisterMask(int registrationId)
        {
            if (!_masks.TryGetValue(registrationId, out var entry))
                return false;

            _masks.Remove(registrationId);
            if (_layerToMaskIds.TryGetValue(entry.LayerTag, out var list))
                list.Remove(registrationId);

            return true;
        }

        // ── Pipeline Execution ──────────────────────────────────

        public void Tick()
        {
            if (!_acquired || _layers.Count == 0)
                return;
            Process();
        }

        public void Process()
        {
            if (_layersDirty)
            {
                RebuildSortedLayers();
                _layersDirty = false;
            }

            for (int i = 0; i < _sortedLayers.Count; i++)
            {
                var layer = _sortedLayers[i];
                if (!layer.Enabled)
                    continue;

                ProcessLayer(layer);
            }
        }

        void ProcessLayer(in TextureEffectLayerDef layer)
        {
            // 1. Resolve input texture
            if (!_hub.TryGet(layer.InputTag, out var inputFrame))
                return;
            if (inputFrame.Texture == null)
                return;

            // 2. Find the effect handler
            if (!_effects.TryGetValue(layer.EffectKind, out var effect))
                return;

            // 3. Determine output size
            int outW = Mathf.Max(1, (int)(inputFrame.Width * layer.ResolutionScale));
            int outH = Mathf.Max(1, (int)(inputFrame.Height * layer.ResolutionScale));

            // 4. Ensure output RT
            var outputRT = EnsureRT(ref _tempRT, outW, outH, "EffectOutput");

            // 5. Build mask RT (if masks exist for this layer)
            RenderTexture? maskRT = null;
            if (_layerToMaskIds.TryGetValue(layer.LayerTag, out var maskIds) && maskIds.Count > 0)
            {
                maskRT = BuildMaskRT(maskIds, inputFrame, outW, outH);
            }

            // 6. Execute effect
            effect.Execute(inputFrame.Texture, outputRT, maskRT, layer.Params, layer.ResolutionScale);

            // 7. Publish output
            if (!string.IsNullOrEmpty(layer.OutputTag))
            {
                var desc = new SharedTextureDescriptor(outW, outH);
                var options = SharedTexturePublishOptions.ForProcessor($"effect/{layer.LayerTag}");
                _hub.Publish(layer.OutputTag, outputRT, desc, options);
            }

            effect.ReleaseTemporaryResources();
        }

        RenderTexture? BuildMaskRT(List<int> maskIds, in SharedTextureFrame inputFrame, int width, int height)
        {
            var maskRT = EnsureRT(ref _maskRT, width, height, "MaskRT");

            // Clear mask to black (nothing masked)
            var prevActive = RenderTexture.active;
            RenderTexture.active = maskRT;
            GL.Clear(true, true, Color.clear);

            // For each mask entry, render mask shape
            foreach (var maskId in maskIds)
            {
                if (!_masks.TryGetValue(maskId, out var maskEntry))
                    continue;
                if (!maskEntry.Enabled)
                    continue;

                RenderMaskEntry(maskEntry, inputFrame, maskRT);
            }

            RenderTexture.active = prevActive;
            return maskRT;
        }

        void RenderMaskEntry(in TextureEffectMaskEntry entry, in SharedTextureFrame inputFrame, RenderTexture maskRT)
        {
            switch (entry.ShapeKind)
            {
                case MaskShapeKind.RendererShape:
                    RenderRendererMask(entry, inputFrame, maskRT);
                    break;
                case MaskShapeKind.BoundsRect:
                    RenderBoundsMask(entry, inputFrame, maskRT);
                    break;
                case MaskShapeKind.Circle:
                    RenderCircleMask(entry, maskRT);
                    break;
            }
        }

        void RenderRendererMask(in TextureEffectMaskEntry entry, in SharedTextureFrame inputFrame, RenderTexture maskRT)
        {
            if (entry.MaskRenderer == null || !entry.MaskRenderer.enabled)
                return;

            // Use capture camera's VP matrix if available
            if (!inputFrame.CameraCapture.HasValue)
                return;

            var camInfo = inputFrame.CameraCapture.Value;
            if (camInfo.CaptureCamera == null)
                return;

            // Renderer 形状ベ�Eスの mask 描画:
            // capture camera と同じ projection で対象 Renderer だけを mask RT へ描く
            EnsureMaskMaterial();
            if (s_MaskMaterial == null)
                return;

            var prevRT = RenderTexture.active;
            RenderTexture.active = maskRT;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(camInfo.ProjectionMatrix);

            // View matrix from capture time
            s_MaskMaterial.SetPass(0);

            var meshFilter = entry.MaskRenderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var worldMatrix = entry.MaskRenderer.localToWorldMatrix;
                var mvMatrix = camInfo.ViewMatrix * worldMatrix;
                Graphics.DrawMeshNow(meshFilter.sharedMesh, mvMatrix);
            }
            else if (entry.MaskRenderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
            {
                // For SpriteRenderers, draw their bounds as a quad
                DrawSpriteMaskQuad(spriteRenderer, camInfo);
            }

            GL.PopMatrix();
            RenderTexture.active = prevRT;
        }

        void DrawSpriteMaskQuad(SpriteRenderer spriteRenderer, in SharedTextureCameraCaptureInfo camInfo)
        {
            var bounds = spriteRenderer.bounds;
            var viewProj = camInfo.ViewProjectionMatrix;

            GL.Begin(GL.QUADS);
            GL.Color(Color.white);

            var min = bounds.min;
            var max = bounds.max;
            GL.Vertex(viewProj.MultiplyPoint(new Vector3(min.x, min.y, min.z)));
            GL.Vertex(viewProj.MultiplyPoint(new Vector3(max.x, min.y, min.z)));
            GL.Vertex(viewProj.MultiplyPoint(new Vector3(max.x, max.y, min.z)));
            GL.Vertex(viewProj.MultiplyPoint(new Vector3(min.x, max.y, min.z)));

            GL.End();
        }

        void RenderBoundsMask(in TextureEffectMaskEntry entry, in SharedTextureFrame inputFrame, RenderTexture maskRT)
        {
            if (entry.MaskRenderer == null)
                return;
            if (!inputFrame.CameraCapture.HasValue)
                return;

            var camInfo = inputFrame.CameraCapture.Value;
            var bounds = entry.MaskRenderer.bounds;
            var viewProj = camInfo.ViewProjectionMatrix;

            var prevRT = RenderTexture.active;
            RenderTexture.active = maskRT;

            EnsureMaskMaterial();
            if (s_MaskMaterial != null)
                s_MaskMaterial.SetPass(0);

            GL.PushMatrix();
            GL.LoadOrtho();

            // Project bounds corners to screen space
            var min = bounds.min;
            var max = bounds.max;
            var screenMin = ViewportFromWorld(viewProj, min);
            var screenMax = ViewportFromWorld(viewProj, max);

            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            GL.Vertex3(screenMin.x, screenMin.y, 0);
            GL.Vertex3(screenMax.x, screenMin.y, 0);
            GL.Vertex3(screenMax.x, screenMax.y, 0);
            GL.Vertex3(screenMin.x, screenMax.y, 0);
            GL.End();

            GL.PopMatrix();
            RenderTexture.active = prevRT;
        }

        void RenderCircleMask(in TextureEffectMaskEntry entry, RenderTexture maskRT)
        {
            var prevRT = RenderTexture.active;
            RenderTexture.active = maskRT;

            EnsureMaskMaterial();
            if (s_MaskMaterial != null)
                s_MaskMaterial.SetPass(0);

            GL.PushMatrix();
            GL.LoadOrtho();

            // Approximate circle with a triangle fan
            GL.Begin(GL.TRIANGLES);
            GL.Color(Color.white);

            int segments = 32;
            var center = entry.CircleCenter;
            float radius = entry.CircleRadius;

            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;

                GL.Vertex3(center.x, center.y, 0);
                GL.Vertex3(center.x + Mathf.Cos(a0) * radius, center.y + Mathf.Sin(a0) * radius, 0);
                GL.Vertex3(center.x + Mathf.Cos(a1) * radius, center.y + Mathf.Sin(a1) * radius, 0);
            }

            GL.End();
            GL.PopMatrix();
            RenderTexture.active = prevRT;
        }

        static Vector2 ViewportFromWorld(Matrix4x4 viewProj, Vector3 worldPos)
        {
            var clip = viewProj.MultiplyPoint(worldPos);
            return new Vector2(clip.x * 0.5f + 0.5f, clip.y * 0.5f + 0.5f);
        }

        // ── Helpers ─────────────────────────────────────────────

        void RebuildSortedLayers()
        {
            _sortedLayers.Clear();
            _sortedLayers.AddRange(_layers.Values);
            _sortedLayers.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        static RenderTexture EnsureRT(ref RenderTexture? rt, int width, int height, string name)
        {
            if (rt != null && rt.width == width && rt.height == height)
                return rt;

            if (rt != null)
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }

            rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();
            return rt;
        }

        static void EnsureMaskMaterial()
        {
            if (s_MaskMaterial != null)
                return;

            var shader = Shader.Find("Hidden/TextureEffect/MaskWrite");
            if (shader != null)
                s_MaskMaterial = new Material(shader);
            else
                s_MaskMaterial = new Material(Shader.Find("Sprites/Default")!);
        }

        void ClearAll()
        {
            _layers.Clear();
            _sortedLayers.Clear();
            _masks.Clear();
            _layerToMaskIds.Clear();
            _layersDirty = true;
            _nextMaskId = 1;
        }

        void ReleaseRTs()
        {
            ReleaseRT(ref _maskRT);
            ReleaseRT(ref _tempRT);
            ReleaseRT(ref _pingRT);
            ReleaseRT(ref _pongRT);

            foreach (var effect in _effects.Values)
                effect.ReleaseTemporaryResources();
        }

        static void ReleaseRT(ref RenderTexture? rt)
        {
            if (rt == null)
                return;
            if (rt.IsCreated())
                rt.Release();
            UnityEngine.Object.Destroy(rt);
            rt = null;
        }
    }
}

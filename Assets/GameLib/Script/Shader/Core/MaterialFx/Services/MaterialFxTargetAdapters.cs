#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.MaterialFx.Generated;

namespace Game.MaterialFx
{
    static class SpriteMetaUtils
    {
        public static bool TryComputeForSpriteRenderer(Sprite? sprite, out Vector4 uvRect, out float invPixelsPerUnit)
        {
            if (sprite == null || sprite.texture == null)
            {
                uvRect = default;
                invPixelsPerUnit = 0f;
                return false;
            }

            // SpriteRenderer 側は従来どおり textureRect ベースで UVRect を算出する。
            // （sprite.uv を使うと、Shader 側の UVLocal/Flip 表現が崩れるケースがあったため）
            var tex = sprite.texture;
            var r = sprite.textureRect;
            float tw = tex.width;
            float th = tex.height;
            uvRect = (tw > 0f && th > 0f)
                ? new Vector4(r.xMin / tw, r.yMin / th, r.xMax / tw, r.yMax / th)
                : new Vector4(0f, 0f, 1f, 1f);

            float ppu = Mathf.Max(sprite.pixelsPerUnit, 1e-5f);
            invPixelsPerUnit = 1f / ppu;
            return true;
        }

        public static bool TryComputeForGraphic(Sprite? sprite, out Vector4 uvRect, out float invPixelsPerUnit)
        {
            if (sprite == null || sprite.texture == null)
            {
                uvRect = default;
                invPixelsPerUnit = 0f;
                return false;
            }

            // uGUI では atlasUV を含むケースがあるため sprite.uv 優先。
            var uvs = sprite.uv;
            if (uvs != null && uvs.Length > 0)
            {
                float minU = uvs[0].x, maxU = uvs[0].x;
                float minV = uvs[0].y, maxV = uvs[0].y;
                for (int i = 1; i < uvs.Length; i++)
                {
                    var uv = uvs[i];
                    if (uv.x < minU) minU = uv.x;
                    if (uv.x > maxU) maxU = uv.x;
                    if (uv.y < minV) minV = uv.y;
                    if (uv.y > maxV) maxV = uv.y;
                }
                uvRect = new Vector4(minU, minV, maxU, maxV);
            }
            else
            {
                var tex = sprite.texture;
                var r = sprite.textureRect;
                float tw = tex.width;
                float th = tex.height;
                uvRect = (tw > 0f && th > 0f)
                    ? new Vector4(r.xMin / tw, r.yMin / th, r.xMax / tw, r.yMax / th)
                    : new Vector4(0f, 0f, 1f, 1f);
            }

            float ppu = Mathf.Max(sprite.pixelsPerUnit, 1e-5f);
            invPixelsPerUnit = 1f / ppu;
            return true;
        }
    }

    /// <summary>
    /// ターゲット種別を抽象化するアダプタ。
    /// SpriteRenderer, Graphic, TMP_Text などの差異を吸収する。
    /// </summary>
    public interface IMaterialFxTargetAdapter : IDisposable
    {
        /// <summary>ターゲットが有効か</summary>
        bool IsValid { get; }

        /// <summary>Material を確保（必要なら）してプロパティを読み取る</summary>
        bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value);

        /// <summary>プロパティ値を送信</summary>
        void DispatchValue(int propertyId, MaterialFxTypedValue value);

        /// <summary>変更を適用（Renderer.SetPropertyBlock 等）</summary>
        void Apply();
    }

    /// <summary>
    /// SpriteRenderer 用アダプタ。MPB を使用。
    /// 同一フレーム内で GetPropertyBlock は1回のみ呼ばれ、Apply でリセット。
    /// </summary>
    public sealed class SpriteRendererAdapter : IMaterialFxTargetAdapter
    {
        static readonly int FxSrcBlendId = Shader.PropertyToID("_FxSrcBlend");
        static readonly int FxDstBlendId = Shader.PropertyToID("_FxDstBlend");
        static readonly int FxZWriteId = Shader.PropertyToID("_FxZWrite");
        static readonly int FxCullId = Shader.PropertyToID("_FxCull");
        static readonly int FxBlendPresetId = Shader.PropertyToID("_FxBlendPreset");
        static readonly int FxQueueOffsetId = Shader.PropertyToID("_FxQueueOffset");

        readonly SpriteRenderer _renderer;
        readonly MaterialPropertyBlock _mpb;
        Material? _materialInstance;
        int _baseRenderQueue = -1;
        int _queueOffset;
        bool _queueOffsetDirty;

        readonly int _spriteUVRectId;
        readonly int _spriteTexelSizeLocalId;
        readonly int _flipId;

        Sprite? _lastSprite;
        bool _lastFlipX;
        bool _lastFlipY;
        float _lastInvPpu = float.NaN;

        public bool IsValid => _renderer != null;

        public SpriteRendererAdapter(SpriteRenderer renderer, IMaterialFxPropertyRegistry registry)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _spriteUVRectId = ResolveShaderPropertyId(registry, MaterialFxKeys.BaseShader.Common.SpriteUVRect);
            _spriteTexelSizeLocalId = ResolveShaderPropertyId(registry, MaterialFxKeys.BaseShader.Common.SpriteTexelSizeLocal);
            _flipId = ResolveShaderPropertyId(registry, MaterialFxKeys.BaseShader.Common.Flip);

            // _RendererがBaseShader(BaseMaterial)を持っていない場合、一度マテリアルをすり替える
            // BaseMaterialをリソースからロードしておくことで、MaterialFxPreset適用時にプロパティが存在しない問題を回避する
            Material baseMaterial = MaterialFxService.BaseMaterial ?? throw new InvalidOperationException("[SpriteRendererAdapter] MaterialFxService.BaseMaterial is not set. Ensure MaterialFxInstaller sets BaseMaterial before creating adapters.");

            if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != baseMaterial.shader)
            {
                _renderer.sharedMaterial = baseMaterial;
            }

            _mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);

            NotifySpriteChanged(_renderer.sprite);
            NotifyFlipChanged(_renderer.flipX, _renderer.flipY);

            // シンプルに必ず適用する（MPB が外部要因で消える/上書きされるケースに強くする）
            _renderer.SetPropertyBlock(_mpb);
        }

        public bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value)
        {
            var mat = _renderer.sharedMaterial;
            if (mat == null || !mat.HasProperty(propertyId))
            {
                value = default;
                return false;
            }

            value = ReadFromMaterial(mat, propertyId, type);
            return true;
        }

        public void DispatchValue(int propertyId, MaterialFxTypedValue value)
        {
            if (IsRenderStatePropertyId(propertyId))
            {
                DispatchRenderStateValue(propertyId, value);
                return;
            }

            WriteToMpb(_mpb, propertyId, value);
        }

        public void Apply()
        {
            if (_renderer == null)
                return;

            // 毎フレーム SetPropertyBlock を行い、値が戻る（0/1化する）現象を抑止する。
            _renderer.SetPropertyBlock(_mpb);

            if (_queueOffsetDirty)
                ApplyQueueOffset();
        }

        public void Dispose() { }

        bool IsRenderStatePropertyId(int propertyId)
        {
            return propertyId == FxSrcBlendId ||
                   propertyId == FxDstBlendId ||
                   propertyId == FxZWriteId ||
                   propertyId == FxCullId ||
                   propertyId == FxBlendPresetId ||
                   propertyId == FxQueueOffsetId;
        }

        void DispatchRenderStateValue(int propertyId, MaterialFxTypedValue value)
        {
            var material = GetOrCreateMaterialInstance();
            if (material == null)
                return;

            WriteToMaterial(material, propertyId, value);

            if (propertyId == FxQueueOffsetId)
            {
                _queueOffset = value.Type == ValueKind.Float
                    ? Mathf.RoundToInt(value.Float)
                    : value.Int;
                _queueOffsetDirty = true;
            }
        }

        Material? GetOrCreateMaterialInstance()
        {
            if (_materialInstance != null)
                return _materialInstance;

            if (_renderer == null)
                return null;

            _materialInstance = _renderer.material;
            if (_materialInstance == null)
                return null;

            var queue = _materialInstance.renderQueue;
            if (queue < 0)
                queue = _materialInstance.shader != null ? _materialInstance.shader.renderQueue : 3000;
            _baseRenderQueue = queue;
            return _materialInstance;
        }

        void ApplyQueueOffset()
        {
            var material = GetOrCreateMaterialInstance();
            if (material == null)
                return;

            if (_baseRenderQueue < 0)
                _baseRenderQueue = material.shader != null ? material.shader.renderQueue : 3000;

            var queue = Mathf.Clamp(_baseRenderQueue + _queueOffset, 0, 5000);
            if (material.renderQueue != queue)
                material.renderQueue = queue;

            _queueOffsetDirty = false;
        }

        public void NotifySpriteChanged(Sprite? sprite)
        {
            if (ReferenceEquals(sprite, _lastSprite))
                return;

            var uvRect = new Vector4(0f, 0f, 1f, 1f);
            if (SpriteMetaUtils.TryComputeForSpriteRenderer(sprite, out var computedUvRect, out var invPpu))
            {
                uvRect = computedUvRect;
                if (invPpu != _lastInvPpu)
                {
                    _mpb.SetVector(_spriteTexelSizeLocalId, new Vector4(invPpu, invPpu, 0f, 0f));
                    _lastInvPpu = invPpu;
                }
            }

            _mpb.SetVector(_spriteUVRectId, uvRect);
            _lastSprite = sprite;
        }

        public void NotifyFlipChanged(bool flipX, bool flipY)
        {
            if (flipX == _lastFlipX && flipY == _lastFlipY)
                return;

            float fx = flipX ? -1f : 1f;
            float fy = flipY ? -1f : 1f;
            _mpb.SetVector(_flipId, new Vector4(fx, fy, 1f, 1f));

            _lastFlipX = flipX;
            _lastFlipY = flipY;
        }

        static int ResolveShaderPropertyId(IMaterialFxPropertyRegistry registry, string stableKey)
        {
            if (!registry.TryGet(stableKey, out var meta) || string.IsNullOrEmpty(meta.ShaderPropertyName))
                throw new InvalidOperationException($"[SpriteRendererAdapter] StableKey is missing from registry or has empty ShaderPropertyName: '{stableKey}'");

            return Shader.PropertyToID(meta.ShaderPropertyName);
        }

        static MaterialFxTypedValue ReadFromMaterial(Material mat, int propertyId, ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(mat.GetFloat(propertyId)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(mat.GetInt(propertyId)),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(mat.GetInt(propertyId) != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2((Vector2)mat.GetVector(propertyId)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3((Vector3)mat.GetVector(propertyId)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(mat.GetVector(propertyId)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(mat.GetColor(propertyId)),
                ValueKind.Matrix4x4 => MaterialFxTypedValue.FromMatrix(mat.GetMatrix(propertyId)),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(mat.GetTexture(propertyId)),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(mat.GetTexture(propertyId)),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }

        static void WriteToMpb(MaterialPropertyBlock mpb, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mpb.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mpb.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mpb.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mpb.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mpb.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mpb.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mpb.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mpb.SetTexture(propertyId, value.Texture!); // ★ null でクリア
                    break;
            }
        }

        static void WriteToMaterial(Material mat, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mat.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mat.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mat.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mat.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mat.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mat.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mat.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mat.SetTexture(propertyId, value.Texture);
                    break;
            }
        }
    }

    /// <summary>
    /// 汎用 Renderer 用アダプタ。MPB を使用。
    /// 同一フレーム内で GetPropertyBlock は1回のみ呼ばれ、Apply でリセット。
    /// </summary>
    public sealed class RendererAdapter : IMaterialFxTargetAdapter
    {
        static readonly int FxSrcBlendId = Shader.PropertyToID("_FxSrcBlend");
        static readonly int FxDstBlendId = Shader.PropertyToID("_FxDstBlend");
        static readonly int FxZWriteId = Shader.PropertyToID("_FxZWrite");
        static readonly int FxCullId = Shader.PropertyToID("_FxCull");
        static readonly int FxBlendPresetId = Shader.PropertyToID("_FxBlendPreset");
        static readonly int FxQueueOffsetId = Shader.PropertyToID("_FxQueueOffset");

        readonly Renderer _renderer;
        readonly MaterialPropertyBlock _mpb;
        Material? _materialInstance;
        int _baseRenderQueue = -1;
        int _queueOffset;
        bool _queueOffsetDirty;
        bool _mpbFetched;

        public bool IsValid => _renderer != null;

        public RendererAdapter(Renderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _mpb = new MaterialPropertyBlock();
        }

        public bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value)
        {
            var mat = _renderer.sharedMaterial;
            if (mat == null || !mat.HasProperty(propertyId))
            {
                value = default;
                return false;
            }

            value = ReadFromMaterial(mat, propertyId, type);
            return true;
        }

        public void DispatchValue(int propertyId, MaterialFxTypedValue value)
        {
            if (IsRenderStatePropertyId(propertyId))
            {
                DispatchRenderStateValue(propertyId, value);
                return;
            }

            // 同一フレームで最初の DispatchValue 呼び出し時のみ GetPropertyBlock
            if (!_mpbFetched)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpbFetched = true;
            }
            WriteToMpb(_mpb, propertyId, value);
        }

        public void Apply()
        {
            // GetPropertyBlock していた場合のみ SetPropertyBlock
            if (_mpbFetched)
            {
                _renderer.SetPropertyBlock(_mpb);
            }

            if (_queueOffsetDirty)
            {
                ApplyQueueOffset();
            }

            _mpbFetched = false;
        }

        public void Dispose() { }

        bool IsRenderStatePropertyId(int propertyId)
        {
            return propertyId == FxSrcBlendId ||
                   propertyId == FxDstBlendId ||
                   propertyId == FxZWriteId ||
                   propertyId == FxCullId ||
                   propertyId == FxBlendPresetId ||
                   propertyId == FxQueueOffsetId;
        }

        void DispatchRenderStateValue(int propertyId, MaterialFxTypedValue value)
        {
            var material = GetOrCreateMaterialInstance();
            if (material == null)
                return;

            WriteToMaterial(material, propertyId, value);

            if (propertyId == FxQueueOffsetId)
            {
                _queueOffset = value.Type == ValueKind.Float
                    ? Mathf.RoundToInt(value.Float)
                    : value.Int;
                _queueOffsetDirty = true;
            }
        }

        Material? GetOrCreateMaterialInstance()
        {
            if (_materialInstance != null)
                return _materialInstance;

            if (_renderer == null)
                return null;

            var material = _renderer.material;
            _materialInstance = material;
            if (material == null)
                return null;

            var queue = material.renderQueue;
            if (queue < 0)
                queue = material.shader != null ? material.shader.renderQueue : 3000;

            _baseRenderQueue = queue;
            return _materialInstance;
        }

        void ApplyQueueOffset()
        {
            var material = GetOrCreateMaterialInstance();
            if (material == null)
                return;

            if (_baseRenderQueue < 0)
            {
                _baseRenderQueue = material.shader != null ? material.shader.renderQueue : 3000;
            }

            var queue = Mathf.Clamp(_baseRenderQueue + _queueOffset, 0, 5000);
            if (material.renderQueue != queue)
                material.renderQueue = queue;

            _queueOffsetDirty = false;
        }

        static MaterialFxTypedValue ReadFromMaterial(Material mat, int propertyId, ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(mat.GetFloat(propertyId)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(mat.GetInt(propertyId)),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(mat.GetInt(propertyId) != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2((Vector2)mat.GetVector(propertyId)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3((Vector3)mat.GetVector(propertyId)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(mat.GetVector(propertyId)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(mat.GetColor(propertyId)),
                ValueKind.Matrix4x4 => MaterialFxTypedValue.FromMatrix(mat.GetMatrix(propertyId)),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(mat.GetTexture(propertyId)),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(mat.GetTexture(propertyId)),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }

        static void WriteToMpb(MaterialPropertyBlock mpb, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mpb.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mpb.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mpb.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mpb.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mpb.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mpb.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mpb.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mpb.SetTexture(propertyId, value.Texture!); // ★ null でクリア
                    break;
            }
        }

        static void WriteToMaterial(Material mat, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mat.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mat.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mat.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mat.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mat.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mat.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mat.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mat.SetTexture(propertyId, value.Texture);
                    break;
            }
        }
    }

    /// <summary>
    /// Material インスタンスを使用するアダプタ（Graphic/TMP 用基底）。
    /// </summary>
    public sealed class MaterialInstanceAdapter : IMaterialFxTargetAdapter
    {
        readonly Material _material;
        bool _disposed;

        public bool IsValid => _material != null && !_disposed;

        /// <summary>
        /// 既存の Material インスタンスを使用。
        /// </summary>
        public MaterialInstanceAdapter(Material materialInstance)
        {
            _material = materialInstance ?? throw new ArgumentNullException(nameof(materialInstance));
        }

        public bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value)
        {
            if (_material == null || !_material.HasProperty(propertyId))
            {
                value = default;
                return false;
            }

            value = ReadFromMaterial(_material, propertyId, type);
            return true;
        }

        public void DispatchValue(int propertyId, MaterialFxTypedValue value)
        {
            if (_material == null) return;
            WriteToMaterial(_material, propertyId, value);
        }

        public void Apply()
        {
            // Material は直接変更されるので Apply 不要
        }

        public void Dispose()
        {
            _disposed = true;
            // Material の破棄は呼び出し側の責任
        }

        static MaterialFxTypedValue ReadFromMaterial(Material mat, int propertyId, ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(mat.GetFloat(propertyId)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(mat.GetInt(propertyId)),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(mat.GetInt(propertyId) != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2((Vector2)mat.GetVector(propertyId)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3((Vector3)mat.GetVector(propertyId)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(mat.GetVector(propertyId)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(mat.GetColor(propertyId)),
                ValueKind.Matrix4x4 => MaterialFxTypedValue.FromMatrix(mat.GetMatrix(propertyId)),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(mat.GetTexture(propertyId)),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(mat.GetTexture(propertyId)),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }

        static void WriteToMaterial(Material mat, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mat.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mat.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mat.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mat.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mat.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mat.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mat.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mat.SetTexture(propertyId, value.Texture);
                    break;
            }
        }
    }

    /// <summary>
    /// uGUI Graphic 用アダプタ。IMaterialModifier パイプラインを通じてマテリアルを変更し、
    /// Mask/RectMask2D のステンシル処理と MaterialFx の変更を両立させる。
    /// </summary>
    public sealed class GraphicAdapter : IMaterialFxTargetAdapter
    {
        readonly Graphic _graphic;
        MaterialFxGraphicModifier? _modifier;
        bool _disposed;

        readonly int _spriteUVRectId;
        readonly int _spriteTexelSizeLocalId;

        public bool IsValid => _graphic != null && !_disposed;

        public GraphicAdapter(Graphic graphic, IMaterialFxPropertyRegistry registry)
        {
            _graphic = graphic ?? throw new ArgumentNullException(nameof(graphic));

            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _spriteUVRectId = ResolveShaderPropertyId(registry, MaterialFxKeys.BaseShader.Common.SpriteUVRect);
            _spriteTexelSizeLocalId = ResolveShaderPropertyId(registry, MaterialFxKeys.BaseShader.Common.SpriteTexelSizeLocal);

            EnsureModifier();
        }

        void EnsureModifier()
        {
            if (_modifier != null) return;
            if (_graphic == null) return;

            // MaterialFxGraphicModifier を追加または取得
            _modifier = _graphic.GetComponent<MaterialFxGraphicModifier>();
            if (_modifier == null)
            {
                _modifier = _graphic.gameObject.AddComponent<MaterialFxGraphicModifier>();
            }

            // 初回は SetMaterialDirty を呼んでマテリアルインスタンスを生成させる
            _modifier.SetMaterialDirty();
        }

        Material? GetMaterialInstance()
        {
            EnsureModifier();
            if (_modifier == null) return null;

            // MaterialInstance がまだ null の場合は、EnsureMaterialInstance で即座に作成
            return _modifier.EnsureMaterialInstance();
        }

        public bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value)
        {
            var mat = GetMaterialInstance();
            if (mat == null || !mat.HasProperty(propertyId))
            {
                value = default;
                return false;
            }

            value = ReadFromMaterial(mat, propertyId, type);
            return true;
        }

        public void DispatchValue(int propertyId, MaterialFxTypedValue value)
        {
            var mat = GetMaterialInstance();
            if (mat == null) return;
            WriteToMaterial(mat, propertyId, value);
        }

        public void Apply()
        {
            if (_graphic == null)
                return;

            var mat = GetMaterialInstance();
            if (mat != null)
            {
                // uGUI Image の sprite は atlasUV を持つため、Shader 側で uvLocal を作れるように
                // _SpriteUVRect を毎フレーム同期する。
                if (_graphic is Image img && img.sprite != null && img.sprite.texture != null)
                {
                    var sprite = img.sprite;
                    if (SpriteMetaUtils.TryComputeForGraphic(sprite, out var uvRect, out var invPpu))
                    {
                        if (mat.HasProperty(_spriteUVRectId))
                            mat.SetVector(_spriteUVRectId, uvRect);

                        if (mat.HasProperty(_spriteTexelSizeLocalId))
                        {
                            mat.SetVector(_spriteTexelSizeLocalId, new Vector4(invPpu, invPpu, 0f, 0f));
                        }
                    }
                }
            }

            // Material への直接書き込みは即時反映される。
            // MaterialPropertyBlock と異なり、Material への SetFloat/SetVector は
            // Canvas の再構築なしで GPU 側に反映されるため、Dirty フラグは不要。
        }

        public void Dispose()
        {
            _disposed = true;
            // MaterialFxGraphicModifier は Graphic と共に破棄されるため、ここでは何もしない
        }

        static int ResolveShaderPropertyId(IMaterialFxPropertyRegistry registry, string stableKey)
        {
            if (!registry.TryGet(stableKey, out var meta) || string.IsNullOrEmpty(meta.ShaderPropertyName))
                throw new InvalidOperationException($"[GraphicAdapter] StableKey is missing from registry or has empty ShaderPropertyName: '{stableKey}'");

            return Shader.PropertyToID(meta.ShaderPropertyName);
        }

        static MaterialFxTypedValue ReadFromMaterial(Material mat, int propertyId, ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(mat.GetFloat(propertyId)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(mat.GetInt(propertyId)),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(mat.GetInt(propertyId) != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2((Vector2)mat.GetVector(propertyId)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3((Vector3)mat.GetVector(propertyId)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(mat.GetVector(propertyId)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(mat.GetColor(propertyId)),
                ValueKind.Matrix4x4 => MaterialFxTypedValue.FromMatrix(mat.GetMatrix(propertyId)),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(mat.GetTexture(propertyId)),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(mat.GetTexture(propertyId)),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }

        static void WriteToMaterial(Material mat, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mat.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mat.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mat.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mat.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mat.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mat.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mat.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mat.SetTexture(propertyId, value.Texture);
                    break;
            }
        }
    }

    /// <summary>
    /// TextMeshPro (TMP_Text) 用アダプタ。fontMaterial を使用。
    /// </summary>
    public sealed class TmpTextAdapter : IMaterialFxTargetAdapter
    {
        readonly TMP_Text _tmpText;
        Material? _materialInstance;
        bool _disposed;
        static readonly int TextModeId = Shader.PropertyToID("_TextMode");
        const float TmpAlphaTextMode = 2f;

        public bool IsValid => _tmpText != null && !_disposed;

        public TmpTextAdapter(TMP_Text tmpText)
        {
            _tmpText = tmpText ?? throw new ArgumentNullException(nameof(tmpText));
            EnsureMaterialInstance();
        }

        void EnsureMaterialInstance()
        {
            if (_materialInstance != null) return;
            var src = _tmpText.fontMaterial;
            if (src == null) return;

            // Clone fontMaterial to ensure unique material for this TMP object.
            var inst = new Material(src)
            {
                name = src.name + " (MaterialFxInstance)"
            };

            // Assign the cloned instance to the TMP object (fontMaterial setter updates internal state)
            _tmpText.fontMaterial = inst;
            _materialInstance = inst;

            // TMP運用では常に TMP Alpha モードを前提にする。
            if (inst.HasProperty(TextModeId))
                inst.SetFloat(TextModeId, TmpAlphaTextMode);
        }

        public bool TryReadPropertyValue(int propertyId, ValueKind type, out MaterialFxTypedValue value)
        {
            EnsureMaterialInstance();
            if (_materialInstance == null || !_materialInstance.HasProperty(propertyId))
            {
                value = default;
                return false;
            }

            value = ReadFromMaterial(_materialInstance, propertyId, type);
            return true;
        }

        public void DispatchValue(int propertyId, MaterialFxTypedValue value)
        {
            EnsureMaterialInstance();
            if (_materialInstance == null) return;
            WriteToMaterial(_materialInstance, propertyId, value);
        }

        public void Apply()
        {
            // TMP は fontMaterial 変更後に SetMaterialDirty を呼ぶ
            if (_tmpText != null)
            {
                _tmpText.SetMaterialDirty();
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }

        static MaterialFxTypedValue ReadFromMaterial(Material mat, int propertyId, ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(mat.GetFloat(propertyId)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(mat.GetInt(propertyId)),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(mat.GetInt(propertyId) != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2((Vector2)mat.GetVector(propertyId)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3((Vector3)mat.GetVector(propertyId)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(mat.GetVector(propertyId)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(mat.GetColor(propertyId)),
                ValueKind.Matrix4x4 => MaterialFxTypedValue.FromMatrix(mat.GetMatrix(propertyId)),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(mat.GetTexture(propertyId)),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(mat.GetTexture(propertyId)),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }

        static void WriteToMaterial(Material mat, int propertyId, MaterialFxTypedValue value)
        {
            switch (value.Type)
            {
                case ValueKind.Float:
                    mat.SetFloat(propertyId, value.Float);
                    break;
                case ValueKind.Int:
                case ValueKind.Bool:
                    mat.SetInt(propertyId, value.Int);
                    break;
                case ValueKind.Float2:
                    mat.SetVector(propertyId, new Vector4(value.Float2.x, value.Float2.y, 0f, 0f));
                    break;
                case ValueKind.Float3:
                    mat.SetVector(propertyId, new Vector4(value.Float3.x, value.Float3.y, value.Float3.z, 0f));
                    break;
                case ValueKind.Float4:
                    mat.SetVector(propertyId, value.Float4);
                    break;
                case ValueKind.Color:
                    mat.SetColor(propertyId, value.Color);
                    break;
                case ValueKind.Matrix4x4:
                    mat.SetMatrix(propertyId, value.Matrix);
                    break;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    mat.SetTexture(propertyId, value.Texture);
                    break;
            }
        }
    }
}

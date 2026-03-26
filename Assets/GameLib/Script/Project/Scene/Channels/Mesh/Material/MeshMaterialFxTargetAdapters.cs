#nullable enable
using System;
using Game.VariableLayer;
using UnityEngine;

namespace Game.Channel
{
    interface IMeshMaterialFxTargetAdapter : IDisposable
    {
        bool IsValid { get; }
        bool SupportsContourEffects { get; }
        void BindMaterial(Material material);
        void SetSortingOrder(int sortingOrder);
        void SetValue(int propertyId, VariableLayerValue value);
        void SetVectorArray(int propertyId, Vector4[] values, int count);
        void Apply();
    }

    sealed class MeshRendererMaterialFxTargetAdapter : IMeshMaterialFxTargetAdapter
    {
        readonly MeshRenderer _renderer;
        readonly MaterialPropertyBlock _propertyBlock = new();
        readonly Vector4[] _vectorArrayBuffer = new Vector4[MeshMaterialFxService.MaxContourSamples];

        public bool IsValid => _renderer != null;
        public bool SupportsContourEffects => true;

        public MeshRendererMaterialFxTargetAdapter(MeshRenderer renderer)
        {
            _renderer = renderer;
        }

        public void BindMaterial(Material material)
        {
            if (_renderer != null && _renderer.sharedMaterial != material)
                _renderer.sharedMaterial = material;
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (_renderer != null)
                _renderer.sortingOrder = sortingOrder;
        }

        public void SetValue(int propertyId, VariableLayerValue value)
        {
            switch (value.Kind)
            {
                case Game.Common.ValueKind.Bool:
                    _propertyBlock.SetFloat(propertyId, value.BoolValue ? 1f : 0f);
                    break;
                case Game.Common.ValueKind.Int:
                    _propertyBlock.SetInt(propertyId, value.IntValue);
                    break;
                case Game.Common.ValueKind.Float:
                    _propertyBlock.SetFloat(propertyId, value.FloatValue);
                    break;
                case Game.Common.ValueKind.Vector2:
                    _propertyBlock.SetVector(propertyId, value.Vector2Value);
                    break;
                case Game.Common.ValueKind.Vector3:
                    _propertyBlock.SetVector(propertyId, value.Vector3Value);
                    break;
                case Game.Common.ValueKind.Vector4:
                    _propertyBlock.SetVector(propertyId, value.Vector4Value);
                    break;
                case Game.Common.ValueKind.Color:
                    _propertyBlock.SetColor(propertyId, value.ColorValue);
                    break;
            }
        }

        public void SetVectorArray(int propertyId, Vector4[] values, int count)
        {
            var clampedCount = Mathf.Clamp(count, 0, _vectorArrayBuffer.Length);
            for (var i = 0; i < clampedCount; i++)
                _vectorArrayBuffer[i] = values[i];
            for (var i = clampedCount; i < _vectorArrayBuffer.Length; i++)
                _vectorArrayBuffer[i] = Vector4.zero;
            _propertyBlock.SetVectorArray(propertyId, _vectorArrayBuffer);
        }

        public void Apply()
        {
            if (_renderer != null)
                _renderer.SetPropertyBlock(_propertyBlock);
        }

        public void Dispose()
        {
        }
    }

    sealed class SkinnedMeshRendererMaterialFxTargetAdapter : IMeshMaterialFxTargetAdapter
    {
        readonly SkinnedMeshRenderer _renderer;
        readonly MaterialPropertyBlock _propertyBlock = new();

        public bool IsValid => _renderer != null;
        public bool SupportsContourEffects => false;

        public SkinnedMeshRendererMaterialFxTargetAdapter(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
        }

        public void BindMaterial(Material material)
        {
            if (_renderer != null && _renderer.sharedMaterial != material)
                _renderer.sharedMaterial = material;
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (_renderer != null)
                _renderer.sortingOrder = sortingOrder;
        }

        public void SetValue(int propertyId, VariableLayerValue value)
        {
            switch (value.Kind)
            {
                case Game.Common.ValueKind.Bool:
                    _propertyBlock.SetFloat(propertyId, value.BoolValue ? 1f : 0f);
                    break;
                case Game.Common.ValueKind.Int:
                    _propertyBlock.SetInt(propertyId, value.IntValue);
                    break;
                case Game.Common.ValueKind.Float:
                    _propertyBlock.SetFloat(propertyId, value.FloatValue);
                    break;
                case Game.Common.ValueKind.Vector2:
                    _propertyBlock.SetVector(propertyId, value.Vector2Value);
                    break;
                case Game.Common.ValueKind.Vector3:
                    _propertyBlock.SetVector(propertyId, value.Vector3Value);
                    break;
                case Game.Common.ValueKind.Vector4:
                    _propertyBlock.SetVector(propertyId, value.Vector4Value);
                    break;
                case Game.Common.ValueKind.Color:
                    _propertyBlock.SetColor(propertyId, value.ColorValue);
                    break;
            }
        }

        public void SetVectorArray(int propertyId, Vector4[] values, int count)
        {
            _ = propertyId;
            _ = values;
            _ = count;
        }

        public void Apply()
        {
            if (_renderer != null)
                _renderer.SetPropertyBlock(_propertyBlock);
        }

        public void Dispose()
        {
        }
    }
}

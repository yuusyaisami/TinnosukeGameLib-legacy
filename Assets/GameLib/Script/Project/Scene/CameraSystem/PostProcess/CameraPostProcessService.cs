#nullable enable
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    public sealed class CameraPostProcessService : ICameraPostProcessService
    {
        const string DefaultLayerTag = "Default";

        readonly List<CameraPostProcessLayer> _layerView = new();
        readonly List<LayeredFloat.LayerSnapshot> _floatSnapshots = new();
        readonly List<LayeredColor.LayerSnapshot> _colorSnapshots = new();
        readonly List<LayeredVector2.LayerSnapshot> _vector2Snapshots = new();
        readonly List<LayeredBool.LayerSnapshot> _boolSnapshots = new();

        Volume _volume;
        VolumeProfile? _runtimeProfile;

        Bloom? _bloom;
        Vignette? _vignette;
        ColorAdjustments? _colorAdjustments;
        ChromaticAberration? _chromaticAberration;
        SplitToning? _splitToning;

        readonly LayeredFloat _bloomThresholdLayers = new();
        readonly LayeredFloat _bloomIntensityLayers = new();
        readonly LayeredFloat _bloomScatterLayers = new();
        readonly LayeredFloat _bloomClampLayers = new();
        readonly LayeredColor _bloomTintLayers = new();

        readonly LayeredColor _vignetteColorLayers = new();
        readonly LayeredVector2 _vignetteCenterLayers = new();
        readonly LayeredFloat _vignetteIntensityLayers = new();
        readonly LayeredFloat _vignetteSmoothnessLayers = new();
        readonly LayeredBool _vignetteRoundedLayers = new();

        readonly LayeredFloat _chromaticIntensityLayers = new();

        readonly LayeredFloat _postExposureLayers = new();
        readonly LayeredFloat _contrastLayers = new();
        readonly LayeredColor _colorFilterLayers = new();
        readonly LayeredFloat _hueShiftLayers = new();
        readonly LayeredFloat _saturationLayers = new();

        readonly LayeredColor _splitShadowsLayers = new();
        readonly LayeredColor _splitHighlightsLayers = new();
        readonly LayeredFloat _splitBalanceLayers = new();

        public CameraPostProcessService(Volume volume)
        {
            _volume = volume;
        }

        public IReadOnlyList<CameraPostProcessLayer> Layers => _layerView;

        public void Initialize()
        {
            if (_volume == null)
                return;

            var shared = _volume.sharedProfile;
            if (shared == null)
                return;

            _runtimeProfile = Object.Instantiate(shared);
            _volume.profile = _runtimeProfile;

            ClearAllInternal();
            CacheComponents();
            InitializeDefaultLayers();
        }

        public bool IsActive(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            return _bloomThresholdLayers.Contains(tag) ||
                   _bloomIntensityLayers.Contains(tag) ||
                   _bloomScatterLayers.Contains(tag) ||
                   _bloomClampLayers.Contains(tag) ||
                   _bloomTintLayers.Contains(tag) ||
                   _vignetteColorLayers.Contains(tag) ||
                   _vignetteCenterLayers.Contains(tag) ||
                   _vignetteIntensityLayers.Contains(tag) ||
                   _vignetteSmoothnessLayers.Contains(tag) ||
                   _vignetteRoundedLayers.Contains(tag) ||
                   _chromaticIntensityLayers.Contains(tag) ||
                   _postExposureLayers.Contains(tag) ||
                   _contrastLayers.Contains(tag) ||
                   _colorFilterLayers.Contains(tag) ||
                   _hueShiftLayers.Contains(tag) ||
                   _saturationLayers.Contains(tag) ||
                   _splitShadowsLayers.Contains(tag) ||
                   _splitHighlightsLayers.Contains(tag) ||
                   _splitBalanceLayers.Contains(tag);
        }

        public void SetLayer(string layerTag, CameraPostProcessFloatParam param, float value)
        {
            SetLayer(layerTag, param, value, 0f, Ease.Linear);
        }

        public void SetLayer(string layerTag, CameraPostProcessFloatParam param, float value, float duration, Ease ease)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            if (!EnsureComponent(param))
                return;

            var layers = GetLayerSet(param);
            if (layers == null)
                return;

            EnsureDefaultLayer(param, layers);
            layers.SetLayer(layerTag, value, duration, ease);
        }

        public void SetLayer(string layerTag, CameraPostProcessColorParam param, Color value)
        {
            SetLayer(layerTag, param, value, 0f, Ease.Linear);
        }

        public void SetLayer(string layerTag, CameraPostProcessColorParam param, Color value, float duration, Ease ease)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            if (!EnsureComponent(param))
                return;

            var layers = GetLayerSet(param);
            if (layers == null)
                return;

            EnsureDefaultLayer(param, layers);
            layers.SetLayer(layerTag, value, duration, ease);
        }

        public void SetLayer(string layerTag, CameraPostProcessVector2Param param, Vector2 value)
        {
            SetLayer(layerTag, param, value, 0f, Ease.Linear);
        }

        public void SetLayer(string layerTag, CameraPostProcessVector2Param param, Vector2 value, float duration, Ease ease)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            if (!EnsureComponent(param))
                return;

            var layers = GetLayerSet(param);
            if (layers == null)
                return;

            EnsureDefaultLayer(param, layers);
            layers.SetLayer(layerTag, value, duration, ease);
        }

        public void SetLayer(string layerTag, CameraPostProcessBoolParam param, bool value)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            if (!EnsureComponent(param))
                return;

            var layers = GetLayerSet(param);
            if (layers == null)
                return;

            EnsureDefaultLayer(param, layers);
            layers.SetLayer(layerTag, value);
        }

        public void ClearLayer(string layerTag, CameraPostProcessFloatParam param)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            var layers = GetLayerSet(param);
            layers?.ClearLayer(layerTag);
        }

        public void ClearLayer(string layerTag, CameraPostProcessColorParam param)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            var layers = GetLayerSet(param);
            layers?.ClearLayer(layerTag);
        }

        public void ClearLayer(string layerTag, CameraPostProcessVector2Param param)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            var layers = GetLayerSet(param);
            layers?.ClearLayer(layerTag);
        }

        public void ClearLayer(string layerTag, CameraPostProcessBoolParam param)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            var layers = GetLayerSet(param);
            layers?.ClearLayer(layerTag);
        }

        public void ClearLayer(string layerTag)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            _bloomThresholdLayers.ClearLayer(layerTag);
            _bloomIntensityLayers.ClearLayer(layerTag);
            _bloomScatterLayers.ClearLayer(layerTag);
            _bloomClampLayers.ClearLayer(layerTag);
            _bloomTintLayers.ClearLayer(layerTag);
            _vignetteColorLayers.ClearLayer(layerTag);
            _vignetteCenterLayers.ClearLayer(layerTag);
            _vignetteIntensityLayers.ClearLayer(layerTag);
            _vignetteSmoothnessLayers.ClearLayer(layerTag);
            _vignetteRoundedLayers.ClearLayer(layerTag);
            _chromaticIntensityLayers.ClearLayer(layerTag);
            _postExposureLayers.ClearLayer(layerTag);
            _contrastLayers.ClearLayer(layerTag);
            _colorFilterLayers.ClearLayer(layerTag);
            _hueShiftLayers.ClearLayer(layerTag);
            _saturationLayers.ClearLayer(layerTag);
            _splitShadowsLayers.ClearLayer(layerTag);
            _splitHighlightsLayers.ClearLayer(layerTag);
            _splitBalanceLayers.ClearLayer(layerTag);
        }

        public void ClearAllLayers()
        {
            _bloomThresholdLayers.ClearAllExcept(DefaultLayerTag);
            _bloomIntensityLayers.ClearAllExcept(DefaultLayerTag);
            _bloomScatterLayers.ClearAllExcept(DefaultLayerTag);
            _bloomClampLayers.ClearAllExcept(DefaultLayerTag);
            _bloomTintLayers.ClearAllExcept(DefaultLayerTag);
            _vignetteColorLayers.ClearAllExcept(DefaultLayerTag);
            _vignetteCenterLayers.ClearAllExcept(DefaultLayerTag);
            _vignetteIntensityLayers.ClearAllExcept(DefaultLayerTag);
            _vignetteSmoothnessLayers.ClearAllExcept(DefaultLayerTag);
            _vignetteRoundedLayers.ClearAllExcept(DefaultLayerTag);
            _chromaticIntensityLayers.ClearAllExcept(DefaultLayerTag);
            _postExposureLayers.ClearAllExcept(DefaultLayerTag);
            _contrastLayers.ClearAllExcept(DefaultLayerTag);
            _colorFilterLayers.ClearAllExcept(DefaultLayerTag);
            _hueShiftLayers.ClearAllExcept(DefaultLayerTag);
            _saturationLayers.ClearAllExcept(DefaultLayerTag);
            _splitShadowsLayers.ClearAllExcept(DefaultLayerTag);
            _splitHighlightsLayers.ClearAllExcept(DefaultLayerTag);
            _splitBalanceLayers.ClearAllExcept(DefaultLayerTag);
        }

        public void Tick(float dt)
        {
            _ = dt;
            if (_runtimeProfile == null)
                return;

            ApplyToProfile();
            BuildLayerView();
        }

        public void ResetToBase(bool immediate)
        {
            ClearAllLayers();
            if (!immediate || _runtimeProfile == null)
                return;

            ApplyToProfile();
        }

        void CacheComponents()
        {
            if (_runtimeProfile == null)
                return;

            _runtimeProfile.TryGet(out _bloom);
            _runtimeProfile.TryGet(out _vignette);
            _runtimeProfile.TryGet(out _colorAdjustments);
            _runtimeProfile.TryGet(out _chromaticAberration);
            _runtimeProfile.TryGet(out _splitToning);
        }

        void ClearAllInternal()
        {
            _bloomThresholdLayers.ClearAll();
            _bloomIntensityLayers.ClearAll();
            _bloomScatterLayers.ClearAll();
            _bloomClampLayers.ClearAll();
            _bloomTintLayers.ClearAll();
            _vignetteColorLayers.ClearAll();
            _vignetteCenterLayers.ClearAll();
            _vignetteIntensityLayers.ClearAll();
            _vignetteSmoothnessLayers.ClearAll();
            _vignetteRoundedLayers.ClearAll();
            _chromaticIntensityLayers.ClearAll();
            _postExposureLayers.ClearAll();
            _contrastLayers.ClearAll();
            _colorFilterLayers.ClearAll();
            _hueShiftLayers.ClearAll();
            _saturationLayers.ClearAll();
            _splitShadowsLayers.ClearAll();
            _splitHighlightsLayers.ClearAll();
            _splitBalanceLayers.ClearAll();
        }

        void InitializeDefaultLayers()
        {
            if (_bloom != null)
            {
                EnsureDefaultLayer(CameraPostProcessFloatParam.BloomThreshold, _bloomThresholdLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.BloomIntensity, _bloomIntensityLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.BloomScatter, _bloomScatterLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.BloomClamp, _bloomClampLayers);
                EnsureDefaultLayer(CameraPostProcessColorParam.BloomTint, _bloomTintLayers);
            }

            if (_vignette != null)
            {
                EnsureDefaultLayer(CameraPostProcessColorParam.VignetteColor, _vignetteColorLayers);
                EnsureDefaultLayer(CameraPostProcessVector2Param.VignetteCenter, _vignetteCenterLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.VignetteIntensity, _vignetteIntensityLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.VignetteSmoothness, _vignetteSmoothnessLayers);
                EnsureDefaultLayer(CameraPostProcessBoolParam.VignetteRounded, _vignetteRoundedLayers);
            }

            if (_colorAdjustments != null)
            {
                EnsureDefaultLayer(CameraPostProcessFloatParam.ColorAdjustmentsPostExposure, _postExposureLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.ColorAdjustmentsContrast, _contrastLayers);
                EnsureDefaultLayer(CameraPostProcessColorParam.ColorAdjustmentsColorFilter, _colorFilterLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.ColorAdjustmentsHueShift, _hueShiftLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.ColorAdjustmentsSaturation, _saturationLayers);
            }

            if (_chromaticAberration != null)
            {
                EnsureDefaultLayer(CameraPostProcessFloatParam.ChromaticAberrationIntensity, _chromaticIntensityLayers);
            }

            if (_splitToning != null)
            {
                EnsureDefaultLayer(CameraPostProcessColorParam.SplitToningShadows, _splitShadowsLayers);
                EnsureDefaultLayer(CameraPostProcessColorParam.SplitToningHighlights, _splitHighlightsLayers);
                EnsureDefaultLayer(CameraPostProcessFloatParam.SplitToningBalance, _splitBalanceLayers);
            }
        }

        void ApplyToProfile()
        {
            if (_bloom != null)
            {
                ApplyParam(_bloom.threshold, _bloomThresholdLayers, v => Mathf.Max(0f, v));
                ApplyParam(_bloom.intensity, _bloomIntensityLayers, v => Mathf.Max(0f, v));
                ApplyParam(_bloom.scatter, _bloomScatterLayers, Mathf.Clamp01);
                ApplyParam(_bloom.clamp, _bloomClampLayers, v => Mathf.Max(0f, v));
                ApplyParam(_bloom.tint, _bloomTintLayers);
            }

            if (_vignette != null)
            {
                ApplyParam(_vignette.color, _vignetteColorLayers);
                ApplyParam(_vignette.center, _vignetteCenterLayers);
                ApplyParam(_vignette.intensity, _vignetteIntensityLayers, Mathf.Clamp01);
                ApplyParam(_vignette.smoothness, _vignetteSmoothnessLayers, Mathf.Clamp01);
                ApplyParam(_vignette.rounded, _vignetteRoundedLayers);
            }

            if (_colorAdjustments != null)
            {
                ApplyParam(_colorAdjustments.postExposure, _postExposureLayers);
                ApplyParam(_colorAdjustments.contrast, _contrastLayers);
                ApplyParam(_colorAdjustments.colorFilter, _colorFilterLayers);
                ApplyParam(_colorAdjustments.hueShift, _hueShiftLayers);
                ApplyParam(_colorAdjustments.saturation, _saturationLayers);
            }

            if (_chromaticAberration != null)
            {
                ApplyParam(_chromaticAberration.intensity, _chromaticIntensityLayers, Mathf.Clamp01);
            }

            if (_splitToning != null)
            {
                ApplyParam(_splitToning.shadows, _splitShadowsLayers);
                ApplyParam(_splitToning.highlights, _splitHighlightsLayers);
                ApplyParam(_splitToning.balance, _splitBalanceLayers);
            }
        }

        void BuildLayerView()
        {
            _layerView.Clear();
            var map = new Dictionary<string, bool>();

            AppendLayerTags(map, _bloomThresholdLayers);
            AppendLayerTags(map, _bloomIntensityLayers);
            AppendLayerTags(map, _bloomScatterLayers);
            AppendLayerTags(map, _bloomClampLayers);
            AppendLayerTags(map, _bloomTintLayers);
            AppendLayerTags(map, _vignetteColorLayers);
            AppendLayerTags(map, _vignetteCenterLayers);
            AppendLayerTags(map, _vignetteIntensityLayers);
            AppendLayerTags(map, _vignetteSmoothnessLayers);
            AppendLayerTags(map, _vignetteRoundedLayers);
            AppendLayerTags(map, _chromaticIntensityLayers);
            AppendLayerTags(map, _postExposureLayers);
            AppendLayerTags(map, _contrastLayers);
            AppendLayerTags(map, _colorFilterLayers);
            AppendLayerTags(map, _hueShiftLayers);
            AppendLayerTags(map, _saturationLayers);
            AppendLayerTags(map, _splitShadowsLayers);
            AppendLayerTags(map, _splitHighlightsLayers);
            AppendLayerTags(map, _splitBalanceLayers);

            foreach (var tag in map.Keys)
            {
                _layerView.Add(new CameraPostProcessLayer(tag));
            }
        }

        void AppendLayerTags(Dictionary<string, bool> map, LayeredFloat layers)
        {
            _floatSnapshots.Clear();
            layers.AppendSnapshots(_floatSnapshots);
            for (int i = 0; i < _floatSnapshots.Count; i++)
            {
                map[_floatSnapshots[i].Tag] = true;
            }
        }

        void AppendLayerTags(Dictionary<string, bool> map, LayeredColor layers)
        {
            _colorSnapshots.Clear();
            layers.AppendSnapshots(_colorSnapshots);
            for (int i = 0; i < _colorSnapshots.Count; i++)
            {
                map[_colorSnapshots[i].Tag] = true;
            }
        }

        void AppendLayerTags(Dictionary<string, bool> map, LayeredVector2 layers)
        {
            _vector2Snapshots.Clear();
            layers.AppendSnapshots(_vector2Snapshots);
            for (int i = 0; i < _vector2Snapshots.Count; i++)
            {
                map[_vector2Snapshots[i].Tag] = true;
            }
        }

        void AppendLayerTags(Dictionary<string, bool> map, LayeredBool layers)
        {
            _boolSnapshots.Clear();
            layers.AppendSnapshots(_boolSnapshots);
            for (int i = 0; i < _boolSnapshots.Count; i++)
            {
                map[_boolSnapshots[i].Tag] = true;
            }
        }

        LayeredFloat? GetLayerSet(CameraPostProcessFloatParam param)
        {
            return param switch
            {
                CameraPostProcessFloatParam.BloomThreshold => _bloomThresholdLayers,
                CameraPostProcessFloatParam.BloomIntensity => _bloomIntensityLayers,
                CameraPostProcessFloatParam.BloomScatter => _bloomScatterLayers,
                CameraPostProcessFloatParam.BloomClamp => _bloomClampLayers,
                CameraPostProcessFloatParam.VignetteIntensity => _vignetteIntensityLayers,
                CameraPostProcessFloatParam.VignetteSmoothness => _vignetteSmoothnessLayers,
                CameraPostProcessFloatParam.ChromaticAberrationIntensity => _chromaticIntensityLayers,
                CameraPostProcessFloatParam.ColorAdjustmentsPostExposure => _postExposureLayers,
                CameraPostProcessFloatParam.ColorAdjustmentsContrast => _contrastLayers,
                CameraPostProcessFloatParam.ColorAdjustmentsHueShift => _hueShiftLayers,
                CameraPostProcessFloatParam.ColorAdjustmentsSaturation => _saturationLayers,
                CameraPostProcessFloatParam.SplitToningBalance => _splitBalanceLayers,
                _ => null
            };
        }

        LayeredColor? GetLayerSet(CameraPostProcessColorParam param)
        {
            return param switch
            {
                CameraPostProcessColorParam.BloomTint => _bloomTintLayers,
                CameraPostProcessColorParam.VignetteColor => _vignetteColorLayers,
                CameraPostProcessColorParam.ColorAdjustmentsColorFilter => _colorFilterLayers,
                CameraPostProcessColorParam.SplitToningShadows => _splitShadowsLayers,
                CameraPostProcessColorParam.SplitToningHighlights => _splitHighlightsLayers,
                _ => null
            };
        }

        LayeredVector2? GetLayerSet(CameraPostProcessVector2Param param)
        {
            return param switch
            {
                CameraPostProcessVector2Param.VignetteCenter => _vignetteCenterLayers,
                _ => null
            };
        }

        LayeredBool? GetLayerSet(CameraPostProcessBoolParam param)
        {
            return param switch
            {
                CameraPostProcessBoolParam.VignetteRounded => _vignetteRoundedLayers,
                _ => null
            };
        }

        bool EnsureComponent(CameraPostProcessFloatParam param)
        {
            return param switch
            {
                CameraPostProcessFloatParam.BloomThreshold => EnsureBloom(),
                CameraPostProcessFloatParam.BloomIntensity => EnsureBloom(),
                CameraPostProcessFloatParam.BloomScatter => EnsureBloom(),
                CameraPostProcessFloatParam.BloomClamp => EnsureBloom(),
                CameraPostProcessFloatParam.VignetteIntensity => EnsureVignette(),
                CameraPostProcessFloatParam.VignetteSmoothness => EnsureVignette(),
                CameraPostProcessFloatParam.ChromaticAberrationIntensity => EnsureChromaticAberration(),
                CameraPostProcessFloatParam.ColorAdjustmentsPostExposure => EnsureColorAdjustments(),
                CameraPostProcessFloatParam.ColorAdjustmentsContrast => EnsureColorAdjustments(),
                CameraPostProcessFloatParam.ColorAdjustmentsHueShift => EnsureColorAdjustments(),
                CameraPostProcessFloatParam.ColorAdjustmentsSaturation => EnsureColorAdjustments(),
                CameraPostProcessFloatParam.SplitToningBalance => EnsureSplitToning(),
                _ => false
            };
        }

        bool EnsureComponent(CameraPostProcessColorParam param)
        {
            return param switch
            {
                CameraPostProcessColorParam.BloomTint => EnsureBloom(),
                CameraPostProcessColorParam.VignetteColor => EnsureVignette(),
                CameraPostProcessColorParam.ColorAdjustmentsColorFilter => EnsureColorAdjustments(),
                CameraPostProcessColorParam.SplitToningShadows => EnsureSplitToning(),
                CameraPostProcessColorParam.SplitToningHighlights => EnsureSplitToning(),
                _ => false
            };
        }

        bool EnsureComponent(CameraPostProcessVector2Param param)
        {
            return param switch
            {
                CameraPostProcessVector2Param.VignetteCenter => EnsureVignette(),
                _ => false
            };
        }

        bool EnsureComponent(CameraPostProcessBoolParam param)
        {
            return param switch
            {
                CameraPostProcessBoolParam.VignetteRounded => EnsureVignette(),
                _ => false
            };
        }

        bool EnsureBloom()
        {
            if (_runtimeProfile == null)
                return false;

            if (_bloom == null)
                _bloom = _runtimeProfile.Add<Bloom>(true);

            return _bloom != null;
        }

        bool EnsureVignette()
        {
            if (_runtimeProfile == null)
                return false;

            if (_vignette == null)
                _vignette = _runtimeProfile.Add<Vignette>(true);

            return _vignette != null;
        }

        bool EnsureColorAdjustments()
        {
            if (_runtimeProfile == null)
                return false;

            if (_colorAdjustments == null)
                _colorAdjustments = _runtimeProfile.Add<ColorAdjustments>(true);

            return _colorAdjustments != null;
        }

        bool EnsureChromaticAberration()
        {
            if (_runtimeProfile == null)
                return false;

            if (_chromaticAberration == null)
                _chromaticAberration = _runtimeProfile.Add<ChromaticAberration>(true);

            return _chromaticAberration != null;
        }

        bool EnsureSplitToning()
        {
            if (_runtimeProfile == null)
                return false;

            if (_splitToning == null)
                _splitToning = _runtimeProfile.Add<SplitToning>(true);

            return _splitToning != null;
        }

        void EnsureDefaultLayer(CameraPostProcessFloatParam param, LayeredFloat layers)
        {
            if (layers.Contains(DefaultLayerTag))
                return;

            float value = GetFloatValue(param);
            layers.SetLayer(DefaultLayerTag, value);
        }

        void EnsureDefaultLayer(CameraPostProcessColorParam param, LayeredColor layers)
        {
            if (layers.Contains(DefaultLayerTag))
                return;

            Color value = GetColorValue(param);
            layers.SetLayer(DefaultLayerTag, value);
        }

        void EnsureDefaultLayer(CameraPostProcessVector2Param param, LayeredVector2 layers)
        {
            if (layers.Contains(DefaultLayerTag))
                return;

            Vector2 value = GetVector2Value(param);
            layers.SetLayer(DefaultLayerTag, value);
        }

        void EnsureDefaultLayer(CameraPostProcessBoolParam param, LayeredBool layers)
        {
            if (layers.Contains(DefaultLayerTag))
                return;

            bool value = GetBoolValue(param);
            layers.SetLayer(DefaultLayerTag, value);
        }

        float GetFloatValue(CameraPostProcessFloatParam param)
        {
            return param switch
            {
                CameraPostProcessFloatParam.BloomThreshold => _bloom?.threshold.value ?? 0f,
                CameraPostProcessFloatParam.BloomIntensity => _bloom?.intensity.value ?? 0f,
                CameraPostProcessFloatParam.BloomScatter => _bloom?.scatter.value ?? 0f,
                CameraPostProcessFloatParam.BloomClamp => _bloom?.clamp.value ?? 0f,
                CameraPostProcessFloatParam.VignetteIntensity => _vignette?.intensity.value ?? 0f,
                CameraPostProcessFloatParam.VignetteSmoothness => _vignette?.smoothness.value ?? 0f,
                CameraPostProcessFloatParam.ChromaticAberrationIntensity => _chromaticAberration?.intensity.value ?? 0f,
                CameraPostProcessFloatParam.ColorAdjustmentsPostExposure => _colorAdjustments?.postExposure.value ?? 0f,
                CameraPostProcessFloatParam.ColorAdjustmentsContrast => _colorAdjustments?.contrast.value ?? 0f,
                CameraPostProcessFloatParam.ColorAdjustmentsHueShift => _colorAdjustments?.hueShift.value ?? 0f,
                CameraPostProcessFloatParam.ColorAdjustmentsSaturation => _colorAdjustments?.saturation.value ?? 0f,
                CameraPostProcessFloatParam.SplitToningBalance => _splitToning?.balance.value ?? 0f,
                _ => 0f
            };
        }

        Color GetColorValue(CameraPostProcessColorParam param)
        {
            return param switch
            {
                CameraPostProcessColorParam.BloomTint => _bloom?.tint.value ?? new Color(0f, 0f, 0f, 0f),
                CameraPostProcessColorParam.VignetteColor => _vignette?.color.value ?? new Color(0f, 0f, 0f, 0f),
                CameraPostProcessColorParam.ColorAdjustmentsColorFilter => _colorAdjustments?.colorFilter.value ?? new Color(0f, 0f, 0f, 0f),
                CameraPostProcessColorParam.SplitToningShadows => _splitToning?.shadows.value ?? new Color(0f, 0f, 0f, 0f),
                CameraPostProcessColorParam.SplitToningHighlights => _splitToning?.highlights.value ?? new Color(0f, 0f, 0f, 0f),
                _ => new Color(0f, 0f, 0f, 0f)
            };
        }

        Vector2 GetVector2Value(CameraPostProcessVector2Param param)
        {
            return param switch
            {
                CameraPostProcessVector2Param.VignetteCenter => _vignette?.center.value ?? Vector2.zero,
                _ => Vector2.zero
            };
        }

        bool GetBoolValue(CameraPostProcessBoolParam param)
        {
            return param switch
            {
                CameraPostProcessBoolParam.VignetteRounded => _vignette?.rounded.value ?? false,
                _ => false
            };
        }

        static void ApplyParam(FloatParameter param, LayeredFloat layers, System.Func<float, float>? adjust = null)
        {
            bool has = layers.HasLayers;
            param.overrideState = has;
            if (!has)
                return;

            float value = layers.CurrentSum;
            param.value = adjust != null ? adjust(value) : value;
        }

        static void ApplyParam(ColorParameter param, LayeredColor layers)
        {
            bool has = layers.HasLayers;
            param.overrideState = has;
            if (!has)
                return;

            param.value = layers.CurrentSum;
        }

        static void ApplyParam(Vector2Parameter param, LayeredVector2 layers)
        {
            bool has = layers.HasLayers;
            param.overrideState = has;
            if (!has)
                return;

            param.value = layers.CurrentSum;
        }

        static void ApplyParam(BoolParameter param, LayeredBool layers)
        {
            bool has = layers.HasLayers;
            param.overrideState = has;
            if (!has)
                return;

            param.value = layers.CurrentValue;
        }
    }
}

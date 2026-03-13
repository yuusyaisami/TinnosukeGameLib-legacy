#nullable enable
using DG.Tweening;
using Game.Collision;
using Game.MaterialFx;
using UnityEngine;

namespace Game.Channel
{
    public interface IMeshFxChannelPlayer : System.IDisposable
    {
        string Tag { get; }
        MeshFxChannelDef Def { get; }
        bool IsActive { get; }
        MeshFxRuntimeQualityOverride RuntimeQualityOverride { get; }

        void SetActive(bool active);
        bool SetMaterialLayer(
            string stableKey,
            string contextTag,
            MaterialFxTypedValue value,
            MaterialFxBlendMode blendMode,
            float durationSeconds,
            Ease easing,
            int priority = 0,
            float lifetimeSeconds = -1f);
        bool ClearMaterialContext(string contextTag);
        void OnAcquire();
        void OnRelease();
        void Tick(int frameIndex, float deltaTime);
    }

    public sealed class MeshFxChannelPlayer : IMeshFxChannelPlayer, System.IDisposable
    {
        readonly MeshFxChannelDef _def;
        readonly int _channelIndex;

        readonly Transform? _ownerTransform;

        readonly IMeshFxPathService _pathService;
        readonly IMeshFxGeometryService _geometryService;
        readonly MeshFxVisualService _visualService;
        readonly MeshFxCollisionService _collisionService;

        readonly MeshFxGeometryFrame _geometryFrame = new();
        readonly System.Collections.Generic.List<Vector3> _pathPoints = new(128);

        readonly MeshFxRuntimeQualityOverride _runtimeQualityOverride = new();

        bool _acquired;
        bool _disposed;
        bool _active;

        float _timeSeconds;
        int _stableFrameCount;
        int _degradeCooldown;

        public string Tag => _def.Tag;
        public MeshFxChannelDef Def => _def;
        public bool IsActive => _active;
        public MeshFxRuntimeQualityOverride RuntimeQualityOverride => _runtimeQualityOverride;

        public MeshFxChannelPlayer(
            MeshFxChannelDef def,
            IScopeNode ownerScope,
            int channelIndex,
            IMaterialFxServiceFactory? materialFxFactory,
            ICollisionService? collisionService,
            IHitColliderScopeRegistry? hitScopeRegistry,
            IHitColliderChannelHub? hitChannelHub)
        {
            _def = def;
            _channelIndex = Mathf.Max(0, channelIndex);

            _ownerTransform = ownerScope.Identity?.SelfTransform;

            _pathService = new MeshFxPathService(def, ownerScope!);
            _geometryService = new MeshFxGeometryService();
            _visualService = new MeshFxVisualService(def, _ownerTransform, materialFxFactory);
            _collisionService = new MeshFxCollisionService(def, ownerScope!, collisionService, hitScopeRegistry, hitChannelHub);
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (!_active)
            {
                _visualService.SetVisible(false);
                _collisionService.Clear();
            }
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

            return _visualService.SetMaterialLayer(
                stableKey,
                contextTag,
                value,
                blendMode,
                durationSeconds,
                easing,
                priority,
                lifetimeSeconds);
        }

        public bool ClearMaterialContext(string contextTag)
        {
            if (_disposed)
                return false;

            return _visualService.ClearMaterialContext(contextTag);
        }

        public void OnAcquire()
        {
            if (_disposed)
                return;

            _acquired = true;
            _timeSeconds = 0f;
            _stableFrameCount = 0;
            _degradeCooldown = 0;
            _runtimeQualityOverride.Clear();

            _pathService.OnAcquire();
            _visualService.OnAcquire();
            _collisionService.OnAcquire();

            SetActive(_def.EnabledOnAcquire || _def.PlayOnSpawn);
        }

        public void OnRelease()
        {
            if (_disposed)
                return;

            _acquired = false;
            _active = false;
            _runtimeQualityOverride.Clear();

            _collisionService.OnRelease();
            _visualService.OnRelease();
            _pathService.OnRelease();

            _pathPoints.Clear();
            _geometryFrame.ClearAll();
        }

        public void Tick(int frameIndex, float deltaTime)
        {
            if (_disposed || !_acquired || !_active)
                return;

            if (deltaTime < 0f)
                deltaTime = 0f;

            _timeSeconds += deltaTime;
            UpdateRuntimeQuality(deltaTime);

            var visualInterval = Mathf.Max(1, _def.UpdateIntervalFrames);
            var collisionInterval = Mathf.Clamp(
                _def.CollisionUpdateIntervalFrames + _runtimeQualityOverride.CollisionIntervalBonus,
                1,
                16);

            var runVisual = ((frameIndex + _channelIndex) % visualInterval) == 0;
            var runCollision = _def.CollisionEnabled && ((frameIndex + _channelIndex) % collisionInterval) == 0;

            if (!runVisual && !runCollision)
            {
                _pathService.UpdateTracking(deltaTime);
                return;
            }

            if (!_pathService.ResolvePath(_pathPoints))
            {
                _visualService.SetVisible(false);
                if (runCollision)
                    _collisionService.Clear();
                return;
            }

            var buildMesh = runVisual;
            if (!_geometryService.Evaluate(
                    _def,
                    _pathPoints,
                    _timeSeconds,
                    _runtimeQualityOverride,
                    buildMesh,
                    _geometryFrame))
            {
                _visualService.SetVisible(false);
                if (runCollision)
                    _collisionService.Clear();
                return;
            }

            if (runVisual)
            {
                _visualService.ApplyGeometry(_geometryFrame);
            }

            if (runCollision)
            {
                var collisionPath = _def.CollisionPathSource == MeshFxCollisionPathSource.DeformedVisual
                    ? _geometryFrame.VisualCenterline
                    : _geometryFrame.BaseCenterline;

                _collisionService.Sync(collisionPath, _geometryFrame.WidthSamples);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            OnRelease();
            _disposed = true;
            _visualService.Dispose();
        }

        void UpdateRuntimeQuality(float deltaTime)
        {
            var threshold = ResolvePressureThreshold(_def.PerformanceTier);
            var recoveryThreshold = threshold * 0.75f;

            if (_degradeCooldown > 0)
                _degradeCooldown--;

            if (deltaTime > threshold)
            {
                _stableFrameCount = 0;
                if (_degradeCooldown == 0)
                {
                    ApplyDegradeStep();
                    _degradeCooldown = 10;
                }
                return;
            }

            if (deltaTime < recoveryThreshold)
            {
                _stableFrameCount++;
                if (_stableFrameCount >= 120)
                {
                    RecoverDegradeStep();
                    _stableFrameCount = 0;
                }
            }
            else
            {
                _stableFrameCount = 0;
            }
        }

        void ApplyDegradeStep()
        {
            if (_runtimeQualityOverride.CornerSubdivisionPenalty < 8)
            {
                _runtimeQualityOverride.CornerSubdivisionPenalty++;
                return;
            }

            var maxCollisionIntervalBonus = Mathf.Max(0, 16 - _def.CollisionUpdateIntervalFrames);
            if (_runtimeQualityOverride.CollisionIntervalBonus < maxCollisionIntervalBonus)
            {
                _runtimeQualityOverride.CollisionIntervalBonus++;
                return;
            }

            _runtimeQualityOverride.SimplifyToleranceBonus = Mathf.Min(
                0.5f,
                _runtimeQualityOverride.SimplifyToleranceBonus + 0.01f);
        }

        void RecoverDegradeStep()
        {
            if (_runtimeQualityOverride.SimplifyToleranceBonus > 0f)
            {
                _runtimeQualityOverride.SimplifyToleranceBonus = Mathf.Max(
                    0f,
                    _runtimeQualityOverride.SimplifyToleranceBonus - 0.01f);
                return;
            }

            if (_runtimeQualityOverride.CollisionIntervalBonus > 0)
            {
                _runtimeQualityOverride.CollisionIntervalBonus--;
                return;
            }

            if (_runtimeQualityOverride.CornerSubdivisionPenalty > 0)
            {
                _runtimeQualityOverride.CornerSubdivisionPenalty--;
            }
        }

        static float ResolvePressureThreshold(MeshFxPerformanceTier tier)
        {
            return tier switch
            {
                MeshFxPerformanceTier.High => 1f / 120f,
                MeshFxPerformanceTier.Medium => 1f / 90f,
                MeshFxPerformanceTier.Low => 1f / 60f,
                _ => 1f / 90f,
            };
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using Game.MaterialFx;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Collision;

namespace Game.Channel
{
    [Serializable]
    public sealed class MeshFxChannelDef : ChannelDefBase, IChannelMaterialFx
    {
        [Header("Lifecycle")]
        [SerializeField] bool enabledOnAcquire = true;
        [SerializeField] bool playOnSpawn = false;

        [Header("Performance")]
        [SerializeField, Range(1, 8)] int updateIntervalFrames = 1;
        [SerializeField, Range(1, 16)] int collisionUpdateIntervalFrames = 2;
        [SerializeField] MeshFxPerformanceTier performanceTier = MeshFxPerformanceTier.Medium;

        [Header("Shape")]
        [SerializeField] MeshFxShapeMode mode = MeshFxShapeMode.Beam;
        [SerializeField, ShowIf(nameof(IsBeamMode)), InlineProperty] MeshFxBeamSettings beamSettings = new();
        [SerializeField, ShowIf(nameof(IsWaveLineMode)), InlineProperty] MeshFxWaveLineSettings waveLineSettings = new();
        [SerializeField, ShowIf(nameof(IsRibbonMode)), InlineProperty] MeshFxRibbonSettings ribbonSettings = new();
        [SerializeField, ShowIf(nameof(IsConeMode)), InlineProperty] MeshFxConeSettings coneSettings = new();
        [SerializeField, ShowIf(nameof(IsArcMode)), InlineProperty] MeshFxArcSettings arcSettings = new();

        [Header("Path")]
        [SerializeField] MeshFxPathMode pathMode = MeshFxPathMode.ScopeToScope;
        [SerializeField, ShowIf(nameof(IsSingleDirectionPath)), InlineProperty] MeshFxSingleDirectionSettings singleDirectionSettings = new();
        [SerializeField, ShowIf(nameof(IsScopeToScopePath)), InlineProperty] MeshFxScopeToScopeSettings scopeToScopeSettings = new();
        [SerializeField, ShowIf(nameof(IsTrajectoryTrackPath)), InlineProperty] MeshFxTrajectoryTrackSettings trajectoryTrackSettings = new();

        [Header("Visual")]
        [SerializeField] bool applyMaterialFx = true;
        [SerializeField, ShowIf(nameof(applyMaterialFx)), InlineProperty] BaseShaderFxPresetReference baseShaderPreset = new();
        [SerializeField, ShowIf(nameof(applyMaterialFx)), ListDrawerSettings(ShowPaging = false, ShowFoldout = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        List<MaterialFxPresetEntry> materialFxPresetEntries = new();
        [SerializeField, Range(-100, 100)] int queueOffset = 0;

        [Header("Collision")]
        [SerializeField] bool collisionEnabled = true;
        [SerializeField, ShowIf(nameof(collisionEnabled)), Range(0, 31)] int layerId = 0;
        [SerializeField, ShowIf(nameof(collisionEnabled))] uint hitMask = ~0u;
        [SerializeField, ShowIf(nameof(collisionEnabled))] DynamicColliderSetId setId = DynamicColliderSetId.EnemyBullet;
        [SerializeField, ShowIf(nameof(collisionEnabled))] MeshFxCollisionPathSource collisionPathSource = MeshFxCollisionPathSource.BaseCenterline;
        [SerializeField, ShowIf(nameof(collisionEnabled)), InlineProperty] MeshFxCollisionApproximationSettings collisionApproximation = new();

        public bool EnabledOnAcquire => enabledOnAcquire;
        public bool PlayOnSpawn => playOnSpawn;
        public int UpdateIntervalFrames => Mathf.Clamp(updateIntervalFrames, 1, 8);
        public int CollisionUpdateIntervalFrames => Mathf.Clamp(collisionUpdateIntervalFrames, 1, 16);
        public MeshFxPerformanceTier PerformanceTier => performanceTier;
        public MeshFxShapeMode Mode => mode;
        public MeshFxBeamSettings BeamSettings => beamSettings;
        public MeshFxWaveLineSettings WaveLineSettings => waveLineSettings;
        public MeshFxRibbonSettings RibbonSettings => ribbonSettings;
        public MeshFxConeSettings ConeSettings => coneSettings;
        public MeshFxArcSettings ArcSettings => arcSettings;
        public MeshFxPathMode PathMode => pathMode;
        public MeshFxSingleDirectionSettings SingleDirectionSettings => singleDirectionSettings;
        public MeshFxScopeToScopeSettings ScopeToScopeSettings => scopeToScopeSettings;
        public MeshFxTrajectoryTrackSettings TrajectoryTrackSettings => trajectoryTrackSettings;
        public bool ApplyMaterialFx => applyMaterialFx;
        public BaseShaderFxPreset? BaseShaderPreset => baseShaderPreset.ResolvePreset();
        public IReadOnlyList<MaterialFxPresetEntry> MaterialFxPresetEntries => materialFxPresetEntries;
        public int QueueOffset => queueOffset;
        public bool CollisionEnabled => collisionEnabled;
        public int LayerId => layerId;
        public uint HitMask => hitMask;
        public DynamicColliderSetId SetId => setId;
        public MeshFxCollisionPathSource CollisionPathSource => collisionPathSource;
        public MeshFxCollisionApproximationSettings CollisionApproximation => collisionApproximation;

        bool IsBeamMode => mode == MeshFxShapeMode.Beam;
        bool IsWaveLineMode => mode == MeshFxShapeMode.WaveLine;
        bool IsRibbonMode => mode == MeshFxShapeMode.Ribbon;
        bool IsConeMode => mode == MeshFxShapeMode.Cone;
        bool IsArcMode => mode == MeshFxShapeMode.Arc;
        bool IsSingleDirectionPath => pathMode == MeshFxPathMode.SingleDirection;
        bool IsScopeToScopePath => pathMode == MeshFxPathMode.ScopeToScope;
        bool IsTrajectoryTrackPath => pathMode == MeshFxPathMode.TrajectoryTrack;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            updateIntervalFrames = Mathf.Clamp(updateIntervalFrames, 1, 8);
            collisionUpdateIntervalFrames = Mathf.Clamp(collisionUpdateIntervalFrames, 1, 16);
            queueOffset = Mathf.Clamp(queueOffset, -100, 100);
            layerId = Mathf.Clamp(layerId, 0, 31);

            if (!applyMaterialFx)
                return;
        }
    }
}

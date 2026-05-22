#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum MeshMaterialBlendMode
    {
        Override = 10,
        Add = 20,
        Multiply = 30,
        Overlay = 40,
    }

    public enum MeshTrackRelaxationPolicy
    {
        None = 10,
        SmoothReturn = 20,
        HoldShape = 30,
    }

    public enum MeshWaveSpace
    {
        World = 10,
        NormalizedLength = 20,
    }

    public enum MeshLineDashPatternKind
    {
        Visible = 10,
        Gap = 20,
    }

    public enum MeshTargetLinkTopology
    {
        Independent = 10,
        ChainPath = 20,
        ChainTree = 30,
    }

    public enum MeshEdgeAlphaMode
    {
        FadeInterior = 10,
        FadeContour = 20,
    }

    public interface IMeshChannelHubService
    {
        IReadOnlyList<IMeshChannelPlayerRuntime> Players { get; }
        bool TryGetPlayer(string tag, out IMeshChannelPlayerRuntime player);
        IMeshChannelPlayerRuntime GetPlayer(string tag);
    }

    public interface IMeshChannelPlayerRuntime
    {
        string Tag { get; }
        bool IsActive { get; }
        IReadOnlyList<string> TrackKeys { get; }
    }

    public interface IMeshChannelControlService
    {
        bool SwapRootDefinition(string tag, MeshDefinitionPreset preset);
        bool SwapTrackDefinition(string tag, string key, MeshTrackDefinition definition);
        bool MutateTrackVisualizer(string tag, string key, MeshTrackVisualizerRuntimeMutation mutation);
        bool MutateTrackPlayer(string tag, string key, MeshTrackPlayerRuntimeMutation mutation);
        bool MutateTrackCollider(string tag, string key, MeshTrackColliderRuntimeMutation mutation);
        bool MutateTrackMaterial(string tag, string key, MeshTrackMaterialRuntimeMutation mutation);
        bool MutateSimulationTrack(string tag, string key, MeshSimulationTrackRuntimeMutation mutation);
        bool ResetRuntimeOverrides(
            string tag,
            bool resetVisualizer,
            bool resetPlayer,
            bool resetCollider,
            bool resetMaterial,
            bool resetSimulation);
        bool SetTrackEnabled(string tag, string key, bool enabled);
    }

    public static class MeshChannelDynamicValueFactory
    {
        public static DynamicValue<T> FromManaged<T>(T preset) where T : class
        {
            return DynamicValue<T>.FromSource(new ManagedRefLiteralSource<T>(preset));
        }
    }

    [Serializable]
    public sealed class MeshChannelEntry
    {
        [LabelText("Tag")]
        public string Tag = "default";

        [InlineProperty]
        [HideLabel]
        [LabelText("Definition")]
        public DynamicValue<MeshDefinitionPreset> Definition =
            MeshChannelDynamicValueFactory.FromManaged(new MeshDefinitionPreset());
    }

    [Serializable]
    public sealed class MeshDefinitionPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Root")]
        [LabelText("Enabled On Acquire")]
        public bool EnabledOnAcquire = true;

        [BoxGroup("Tracks")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<MeshTrackDefinition> RegularTracks = new();

        [BoxGroup("Simulation")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = false)]
        public List<MeshSimulationTrackDefinition> SimulationTracks = new();

        [BoxGroup("Pipeline")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshRenderPipelinePreset> RenderPipeline =
            MeshChannelDynamicValueFactory.FromManaged(new MeshRenderPipelinePreset());

        internal MeshDefinitionPreset CreateRuntimeCopy()
        {
            var copy = new MeshDefinitionPreset
            {
                EnabledOnAcquire = EnabledOnAcquire,
                RenderPipeline = RenderPipeline,
            };

            copy.RegularTracks = new List<MeshTrackDefinition>(RegularTracks.Count);
            for (int i = 0; i < RegularTracks.Count; i++)
            {
                MeshTrackDefinition track = RegularTracks[i] ?? throw new InvalidOperationException("Mesh definition contains a null regular track entry.");
                copy.RegularTracks.Add(track.CreateRuntimeCopy());
            }

            copy.SimulationTracks = new List<MeshSimulationTrackDefinition>(SimulationTracks.Count);
            for (int i = 0; i < SimulationTracks.Count; i++)
            {
                MeshSimulationTrackDefinition track = SimulationTracks[i] ?? throw new InvalidOperationException("Mesh definition contains a null simulation track entry.");
                copy.SimulationTracks.Add(track.CreateRuntimeCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class MeshRenderPipelinePreset : IDynamicManagedRefValue
    {
        [BoxGroup("Visual")]
        [LabelText("Enable Visual")]
        [Tooltip("Inspector setting.")]
        public bool EnableVisual = true;

        [BoxGroup("Visual")]
        [LabelText("Default Shader")]
        [Tooltip("Inspector setting.")]
        public Shader? DefaultShader;

        [BoxGroup("Visual")]
        [LabelText("Sorting Order")]
        public int SortingOrder = 0;

        [BoxGroup("Collider")]
        [LabelText("Enable Collider Ownership")]
        public bool EnableColliderObject = true;

        [BoxGroup("Debug")]
        [LabelText("Enable Debug Log")]
        public bool EnableDebugLog = false;

        internal MeshRenderPipelinePreset CreateRuntimeCopy()
        {
            return new MeshRenderPipelinePreset
            {
                EnableVisual = EnableVisual,
                DefaultShader = DefaultShader,
                SortingOrder = SortingOrder,
                EnableColliderObject = EnableColliderObject,
                EnableDebugLog = EnableDebugLog,
            };
        }
    }

    [Serializable]
    public sealed class MeshTrackDefinition
    {
        [BoxGroup("Meta")]
        [LabelText("Key")]
        public string Key = "track";

        [BoxGroup("Meta")]
        [LabelText("Tag")]
        public string Tag = "track";

        [BoxGroup("Meta")]
        [LabelText("Priority")]
        public int Priority = 0;

        [BoxGroup("Meta")]
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = true;

        [BoxGroup("Visualizer")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackVisualizerPresetBase> Visualizer =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackVisualizerPresetBase>(new MeshLineTrackVisualizerPreset());

        [BoxGroup("Player")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackPlayerPresetBase> Player =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackPlayerPresetBase>(new MeshLineTrackPlayerPreset());

        [BoxGroup("Collider")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackColliderPresetBase> Collider =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackColliderPresetBase>(new MeshPolygonTrackColliderPreset());

        [BoxGroup("Material")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackMaterialPreset> Material =
            MeshChannelDynamicValueFactory.FromManaged(new MeshTrackMaterialPreset());

        internal MeshTrackDefinition CreateRuntimeCopy()
        {
            return new MeshTrackDefinition
            {
                Key = Key,
                Tag = Tag,
                Priority = Priority,
                Enabled = Enabled,
                Visualizer = Visualizer,
                Player = Player,
                Collider = Collider,
                Material = Material,
            };
        }
    }

    [Serializable]
    public sealed class MeshSimulationTrackDefinition
    {
        [BoxGroup("Meta")]
        [LabelText("Key")]
        public string Key = "simulation";

        [BoxGroup("Meta")]
        [LabelText("Priority")]
        public int Priority = 0;

        [BoxGroup("Meta")]
        [LabelText("Enabled")]
        public bool Enabled = true;

        [BoxGroup("Preset")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshSimulationPresetBase> Preset =
            MeshChannelDynamicValueFactory.FromManaged<MeshSimulationPresetBase>(new MeshClayTransientSimulationPreset());

        internal MeshSimulationTrackDefinition CreateRuntimeCopy()
        {
            return new MeshSimulationTrackDefinition
            {
                Key = Key,
                Priority = Priority,
                Enabled = Enabled,
                Preset = Preset,
            };
        }
    }

    [Serializable]
    public sealed class MeshPolygonSyncSettings
    {
        [LabelText("Contour Tolerance")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float ContourTolerance = 0.02f;

        [LabelText("Max Point Count")]
        [MinValue(3)]
        [Tooltip("Inspector setting.")]
        public int MaxPointCount = 128;

        [LabelText("Min Point Move")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float MinPointMove = 0.01f;

        [LabelText("Min Area Delta")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float MinAreaDelta = 0.005f;

        [LabelText("Min Angle Delta")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float MinAngleDelta = 0.5f;

        [LabelText("Update Interval Frames")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        public int UpdateIntervalFrames = 1;

        internal MeshPolygonSyncSettings CreateRuntimeCopy()
        {
            return new MeshPolygonSyncSettings
            {
                ContourTolerance = ContourTolerance,
                MaxPointCount = MaxPointCount,
                MinPointMove = MinPointMove,
                MinAreaDelta = MinAreaDelta,
                MinAngleDelta = MinAngleDelta,
                UpdateIntervalFrames = UpdateIntervalFrames,
            };
        }
    }

    [Serializable]
    public sealed class MeshContourSamplingPreset
    {
        [LabelText("Max Samples")]
        [MinValue(4)]
        [Tooltip("Inspector setting.")]
        public int MaxSamples = 48;

        [LabelText("Min Sample Spacing")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float MinSampleSpacing = 0.025f;

        internal MeshContourSamplingPreset CreateRuntimeCopy()
        {
            return new MeshContourSamplingPreset
            {
                MaxSamples = MaxSamples,
                MinSampleSpacing = MinSampleSpacing,
            };
        }
    }

    [Serializable]
    public sealed class MeshContourGradientMaterialPreset
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = true;

        [LabelText("Color")]
        [Tooltip("Inspector setting.")]
        public Color Color = new(1f, 0.35f, 0.35f, 1f);

        [LabelText("Blend Mode")]
        [Tooltip("Inspector setting.")]
        public MeshMaterialBlendMode BlendMode = MeshMaterialBlendMode.Multiply;

        [LabelText("Strength")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Strength = 0.2f;

        [LabelText("Range")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float Range = 0.5f;

        [LabelText("Falloff")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float Falloff = 1.5f;

        internal MeshContourGradientMaterialPreset CreateRuntimeCopy()
        {
            return new MeshContourGradientMaterialPreset
            {
                Enabled = Enabled,
                Color = Color,
                BlendMode = BlendMode,
                Strength = Strength,
                Range = Range,
                Falloff = Falloff,
            };
        }
    }

    [Serializable]
    public sealed class MeshEdgeAlphaMaterialPreset
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = true;

        [LabelText("Mode")]
        [Tooltip("Inspector setting.")]
        public MeshEdgeAlphaMode Mode = MeshEdgeAlphaMode.FadeInterior;

        [LabelText("Gain")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Gain = 0.35f;

        [LabelText("Range")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float Range = 0.2f;

        [LabelText("Softness")]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float Softness = 0.65f;

        internal MeshEdgeAlphaMaterialPreset CreateRuntimeCopy()
        {
            return new MeshEdgeAlphaMaterialPreset
            {
                Enabled = Enabled,
                Mode = Mode,
                Gain = Gain,
                Range = Range,
                Softness = Softness,
            };
        }
    }

    [Serializable]
    public sealed class MeshBandMaterialPreset
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = false;

        [LabelText("Count")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        public int Count = 4;

        [LabelText("Contrast")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Contrast = 0.65f;

        [LabelText("Color")]
        [Tooltip("Inspector setting.")]
        public Color Color = new(0.95f, 0.95f, 1f, 1f);

        [LabelText("Blend Mode")]
        [Tooltip("Inspector setting.")]
        public MeshMaterialBlendMode BlendMode = MeshMaterialBlendMode.Multiply;

        [LabelText("Intensity")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Intensity = 0.25f;

        internal MeshBandMaterialPreset CreateRuntimeCopy()
        {
            return new MeshBandMaterialPreset
            {
                Enabled = Enabled,
                Count = Count,
                Contrast = Contrast,
                Color = Color,
                BlendMode = BlendMode,
                Intensity = Intensity,
            };
        }
    }

    [Serializable]
    public sealed class MeshEdgeFlowMaterialPreset
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = false;

        [LabelText("Color")]
        [Tooltip("Inspector setting.")]
        public Color Color = new(1f, 1f, 1f, 1f);

        [LabelText("Blend Mode")]
        [Tooltip("Inspector setting.")]
        public MeshMaterialBlendMode BlendMode = MeshMaterialBlendMode.Multiply;

        [LabelText("Width")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float Width = 0.12f;

        [LabelText("Speed")]
        [Tooltip("Inspector setting.")]
        public float Speed = 1.2f;

        [LabelText("Intensity")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Intensity = 0.45f;

        internal MeshEdgeFlowMaterialPreset CreateRuntimeCopy()
        {
            return new MeshEdgeFlowMaterialPreset
            {
                Enabled = Enabled,
                Color = Color,
                BlendMode = BlendMode,
                Width = Width,
                Speed = Speed,
                Intensity = Intensity,
            };
        }
    }

    [Serializable]
    public sealed class MeshInteriorNoiseMaterialPreset
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = false;

        [LabelText("Scale")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float Scale = 8f;

        [LabelText("Speed")]
        [Tooltip("Inspector setting.")]
        public float Speed = 0.5f;

        [LabelText("Strength")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float Strength = 0.08f;

        internal MeshInteriorNoiseMaterialPreset CreateRuntimeCopy()
        {
            return new MeshInteriorNoiseMaterialPreset
            {
                Enabled = Enabled,
                Scale = Scale,
                Speed = Speed,
                Strength = Strength,
            };
        }
    }

    [Serializable]
    public sealed class MeshTrackMaterialPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Base")]
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = true;

        [BoxGroup("Base")]
        [LabelText("Material")]
        [ShowIf(nameof(Enabled))]
        [Tooltip("Inspector setting.")]
        public Material? Material;

        [BoxGroup("Base")]
        [LabelText("Base Tint")]
        [ShowIf(nameof(Enabled))]
        [Tooltip("Inspector setting.")]
        public Color BaseTint = Color.white;

        [BoxGroup("Base")]
        [LabelText("Sorting Order Offset")]
        [ShowIf(nameof(Enabled))]
        [Tooltip("Inspector setting.")]
        public int SortingOrderOffset = 0;

        [BoxGroup("Contour Input")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshContourSamplingPreset ContourSampling = new();

        [BoxGroup("Contour Gradient")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshContourGradientMaterialPreset ContourGradient = new();

        [BoxGroup("Edge Alpha")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshEdgeAlphaMaterialPreset EdgeAlpha = new();

        [BoxGroup("Bands")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshBandMaterialPreset Bands = new();

        [BoxGroup("Edge Flow")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshEdgeFlowMaterialPreset EdgeFlow = new();

        [BoxGroup("Interior Noise")]
        [ShowIf(nameof(Enabled))]
        [InlineProperty]
        [HideLabel]
        public MeshInteriorNoiseMaterialPreset InteriorNoise = new();

        internal MeshTrackMaterialPreset CreateRuntimeCopy()
        {
            return new MeshTrackMaterialPreset
            {
                Enabled = Enabled,
                Material = Material,
                BaseTint = BaseTint,
                SortingOrderOffset = SortingOrderOffset,
                ContourSampling = ContourSampling?.CreateRuntimeCopy() ?? new MeshContourSamplingPreset(),
                ContourGradient = ContourGradient?.CreateRuntimeCopy() ?? new MeshContourGradientMaterialPreset(),
                EdgeAlpha = EdgeAlpha?.CreateRuntimeCopy() ?? new MeshEdgeAlphaMaterialPreset(),
                Bands = Bands?.CreateRuntimeCopy() ?? new MeshBandMaterialPreset(),
                EdgeFlow = EdgeFlow?.CreateRuntimeCopy() ?? new MeshEdgeFlowMaterialPreset(),
                InteriorNoise = InteriorNoise?.CreateRuntimeCopy() ?? new MeshInteriorNoiseMaterialPreset(),
            };
        }
    }

    [Serializable]
    public abstract class MeshTrackPlayerPresetBase : IDynamicManagedRefValue
    {
        internal abstract MeshTrackPlayerPresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public abstract class MeshTrackVisualizerPresetBase : IDynamicManagedRefValue
    {
        internal abstract MeshTrackVisualizerPresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public abstract class MeshTrackColliderPresetBase : IDynamicManagedRefValue
    {
        internal abstract MeshTrackColliderPresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class MeshNoColliderTrackColliderPreset : MeshTrackColliderPresetBase
    {
        internal override MeshTrackColliderPresetBase CreateRuntimeCopy()
        {
            return new MeshNoColliderTrackColliderPreset();
        }
    }

    [Serializable]
    public abstract class MeshSimulationPresetBase : IDynamicManagedRefValue
    {
        internal abstract MeshSimulationPresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class MeshLineTrackPlayerPreset : MeshTrackPlayerPresetBase
    {
        [BoxGroup("Line")]
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Line")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        [LabelText("Points")]
        [Tooltip("Inspector setting.")]
        public List<DynamicValue<Vector3>> Points = new()
        {
            DynamicValueExtensions.FromLiteral(Vector3.zero),
            DynamicValueExtensions.FromLiteral(Vector3.right),
        };

        [BoxGroup("Line")]
        [LabelText("Closed")]
        [Tooltip("Inspector setting.")]
        public bool Closed = false;

        [BoxGroup("Line")]
        [LabelText("Smooth Path")]
        [Tooltip("Inspector setting.")]
        public bool SmoothPath = true;

        [BoxGroup("Line")]
        [LabelText("Smoothing Subdivisions")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        public int SmoothingSubdivisions = 8;

        internal override MeshTrackPlayerPresetBase CreateRuntimeCopy()
        {
            return new MeshLineTrackPlayerPreset
            {
                Condition = Condition,
                Points = new List<DynamicValue<Vector3>>(Points),
                Closed = Closed,
                SmoothPath = SmoothPath,
                SmoothingSubdivisions = SmoothingSubdivisions,
            };
        }
    }

    [Serializable]
    public sealed class MeshTrailTrackPlayerPreset : MeshTrackPlayerPresetBase
    {
        [BoxGroup("Trail")]
        [LabelText("Target Position")]
        public DynamicValue<Vector3> TargetPosition = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Trail")]
        [LabelText("Duration Seconds")]
        [MinValue(0.01f)]
        public float DurationSeconds = 0.75f;

        [BoxGroup("Trail")]
        [LabelText("Min Distance")]
        [MinValue(0f)]
        public float MinDistance = 0.05f;

        [BoxGroup("Trail")]
        [LabelText("Min Time")]
        [MinValue(0f)]
        public float MinTime = 0.03f;

        [BoxGroup("Trail")]
        [LabelText("Max Points")]
        [MinValue(2)]
        public int MaxPoints = 64;

        [BoxGroup("Trail")]
        [LabelText("Smooth Path")]
        public bool SmoothPath = true;

        [BoxGroup("Trail")]
        [LabelText("Smoothing Subdivisions")]
        [MinValue(1)]
        public int SmoothingSubdivisions = 8;

        internal override MeshTrackPlayerPresetBase CreateRuntimeCopy()
        {
            return new MeshTrailTrackPlayerPreset
            {
                TargetPosition = TargetPosition,
                DurationSeconds = DurationSeconds,
                MinDistance = MinDistance,
                MinTime = MinTime,
                MaxPoints = MaxPoints,
                SmoothPath = SmoothPath,
                SmoothingSubdivisions = SmoothingSubdivisions,
            };
        }
    }

    [Serializable]
    public sealed class MeshAreaFillTrackPlayerPreset : MeshTrackPlayerPresetBase
    {
        [BoxGroup("Area")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Hub Source\", AreaHubSource)")]
        public ActorSource AreaHubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Area")]
        [LabelText("Area Tag")]
        public string AreaTag = "default";

        internal override MeshTrackPlayerPresetBase CreateRuntimeCopy()
        {
            return new MeshAreaFillTrackPlayerPreset
            {
                AreaHubSource = AreaHubSource,
                AreaTag = AreaTag,
            };
        }
    }

    [Serializable]
    public sealed class MeshTargetLinkTrackPlayerPreset : MeshTrackPlayerPresetBase
    {
        [BoxGroup("Target Link")]
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Target Link")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Self Actor Source\", SelfActorSource)")]
        public ActorSource SelfActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target Link")]
        [LabelText("Target Channel Tag")]
        public string TargetChannelTag = "default";

        [BoxGroup("Target Link")]
        [LabelText("Top N (0 = All)")]
        [MinValue(0)]
        public int TopN = 0;

        [BoxGroup("Target Link")]
        [LabelText("Topology")]
        [EnumToggleButtons]
        public MeshTargetLinkTopology Topology = MeshTargetLinkTopology.Independent;

        internal override MeshTrackPlayerPresetBase CreateRuntimeCopy()
        {
            return new MeshTargetLinkTrackPlayerPreset
            {
                Condition = Condition,
                SelfActorSource = SelfActorSource,
                TargetChannelTag = TargetChannelTag,
                TopN = TopN,
                Topology = Topology,
            };
        }
    }

    [Serializable]
    public struct MeshLineDashPatternElement
    {
        [HorizontalGroup("Pattern", Width = 120f)]
        [LabelText("Kind")]
        public MeshLineDashPatternKind Kind;

        [HorizontalGroup("Pattern")]
        [LabelText("Length")]
        [MinValue(0.001f)]
        public float Length;
    }

    [Serializable]
    public sealed class MeshLineTrackVisualizerPreset : MeshTrackVisualizerPresetBase
    {
        bool ShowWaveSettings => WaveEnabled;

        [BoxGroup("Shape")]
        [LabelText("Base Width")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float BaseWidth = 0.08f;

        [BoxGroup("Shape")]
        [LabelText("Head Taper Normalized")]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float HeadTaperNormalized = 0.1f;

        [BoxGroup("Shape")]
        [LabelText("Tail Taper Normalized")]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float TailTaperNormalized = 0.1f;

        [BoxGroup("Wave")]
        [LabelText("Wave Enabled")]
        [Tooltip("Inspector setting.")]
        public bool WaveEnabled = true;

        [BoxGroup("Wave")]
        [ShowIf(nameof(ShowWaveSettings))]
        [LabelText("Wave Space")]
        [Tooltip("Inspector setting.")]
        public MeshWaveSpace WaveSpace = MeshWaveSpace.NormalizedLength;

        [BoxGroup("Wave")]
        [ShowIf(nameof(ShowWaveSettings))]
        [LabelText("Wave Amplitude")]
        [MinValue(0f)]
        public float WaveAmplitude = 0f;

        [BoxGroup("Wave")]
        [ShowIf(nameof(ShowWaveSettings))]
        [LabelText("Wave Length")]
        [MinValue(0.001f)]
        public float WaveLength = 0.5f;

        [BoxGroup("Wave")]
        [ShowIf(nameof(ShowWaveSettings))]
        [LabelText("Wave Phase")]
        public float WavePhase = 0f;

        [BoxGroup("Wave")]
        [ShowIf(nameof(ShowWaveSettings))]
        [LabelText("Wave Scroll Speed")]
        public float WaveScrollSpeed = 0f;

        [BoxGroup("Dash")]
        [LabelText("Dash Enabled")]
        [Tooltip("Inspector setting.")]
        public bool DashEnabled = false;

        [BoxGroup("Dash")]
        [ShowIf(nameof(DashEnabled))]
        [LabelText("Dash Space")]
        [Tooltip("Inspector setting.")]
        public MeshWaveSpace DashSpace = MeshWaveSpace.World;

        [BoxGroup("Dash")]
        [ShowIf(nameof(DashEnabled))]
        [LabelText("Dash Scroll Speed")]
        [Tooltip("Inspector setting.")]
        public float DashScrollSpeed = 0f;

        [BoxGroup("Dash")]
        [ShowIf(nameof(DashEnabled))]
        [LabelText("Dash Scroll Offset")]
        [Tooltip("Inspector setting.")]
        public float DashScrollOffset = 0f;

        [BoxGroup("Dash")]
        [ShowIf(nameof(DashEnabled))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        [LabelText("Pattern")]
        [Tooltip("Inspector setting.")]
        public List<MeshLineDashPatternElement> Pattern = new()
        {
            new MeshLineDashPatternElement
            {
                Kind = MeshLineDashPatternKind.Visible,
                Length = 0.5f,
            },
            new MeshLineDashPatternElement
            {
                Kind = MeshLineDashPatternKind.Gap,
                Length = 0.25f,
            },
        };

        [BoxGroup("Sampling")]
        [LabelText("Min Segment Length")]
        [MinValue(0.001f)]
        [Tooltip("Inspector setting.")]
        public float MinSegmentLength = 0.05f;

        [BoxGroup("Sampling")]
        [LabelText("Max Point Count")]
        [MinValue(4)]
        [Tooltip("Inspector setting.")]
        public int MaxPointCount = 128;

        internal override MeshTrackVisualizerPresetBase CreateRuntimeCopy()
        {
            return new MeshLineTrackVisualizerPreset
            {
                BaseWidth = BaseWidth,
                HeadTaperNormalized = HeadTaperNormalized,
                TailTaperNormalized = TailTaperNormalized,
                WaveEnabled = WaveEnabled,
                WaveSpace = WaveSpace,
                WaveAmplitude = WaveAmplitude,
                WaveLength = WaveLength,
                WavePhase = WavePhase,
                WaveScrollSpeed = WaveScrollSpeed,
                DashEnabled = DashEnabled,
                DashSpace = DashSpace,
                DashScrollSpeed = DashScrollSpeed,
                DashScrollOffset = DashScrollOffset,
                Pattern = Pattern != null
                    ? new List<MeshLineDashPatternElement>(Pattern)
                    : new List<MeshLineDashPatternElement>(),
                MinSegmentLength = MinSegmentLength,
                MaxPointCount = MaxPointCount,
            };
        }
    }

    [Serializable]
    public sealed class MeshAreaFillTrackVisualizerPreset : MeshTrackVisualizerPresetBase
    {
        [BoxGroup("Area Fill")]
        [LabelText("Contour Sample Count")]
        [MinValue(8)]
        public int ContourSampleCount = 48;

        internal override MeshTrackVisualizerPresetBase CreateRuntimeCopy()
        {
            return new MeshAreaFillTrackVisualizerPreset
            {
                ContourSampleCount = ContourSampleCount,
            };
        }
    }

    [Serializable]
    public sealed class MeshPolygonTrackColliderPreset : MeshTrackColliderPresetBase
    {
        [BoxGroup("Collider")]
        [LabelText("Sync Polygon To Collider")]
        public bool SyncPolygonToCollider = true;

        [BoxGroup("Collider")]
        [LabelText("Enable Hit Capture")]
        public bool EnableHitCapture = true;

        [BoxGroup("Collider")]
        [LabelText("Capture Enter")]
        public bool CaptureEnter = true;

        [BoxGroup("Collider")]
        [LabelText("Capture Stay")]
        public bool CaptureStay = true;

        [BoxGroup("Collider")]
        [LabelText("Capture Exit")]
        public bool CaptureExit = true;

        [BoxGroup("Collider")]
        [LabelText("Velocity Weight")]
        [MinValue(0f)]
        public float VelocityWeight = 1f;

        [BoxGroup("Collider")]
        [LabelText("Impulse Weight")]
        [MinValue(0f)]
        public float ImpulseWeight = 1f;

        [BoxGroup("Collider")]
        [LabelText("Relaxation Policy")]
        public MeshTrackRelaxationPolicy RelaxationPolicy = MeshTrackRelaxationPolicy.SmoothReturn;

        [BoxGroup("Sync")]
        [InlineProperty]
        public MeshPolygonSyncSettings Sync = new();

        internal override MeshTrackColliderPresetBase CreateRuntimeCopy()
        {
            return new MeshPolygonTrackColliderPreset
            {
                SyncPolygonToCollider = SyncPolygonToCollider,
                EnableHitCapture = EnableHitCapture,
                CaptureEnter = CaptureEnter,
                CaptureStay = CaptureStay,
                CaptureExit = CaptureExit,
                VelocityWeight = VelocityWeight,
                ImpulseWeight = ImpulseWeight,
                RelaxationPolicy = RelaxationPolicy,
                Sync = Sync?.CreateRuntimeCopy() ?? new MeshPolygonSyncSettings(),
            };
        }
    }

    [Serializable]
    public sealed class MeshClayTransientSimulationPreset : MeshSimulationPresetBase
    {
        [BoxGroup("Clay")]
        [LabelText("Radius")]
        [MinValue(0.001f)]
        public float Radius = 0.5f;

        [BoxGroup("Clay")]
        [LabelText("Impact Strength")]
        [MinValue(0f)]
        public float ImpactStrength = 0.25f;

        [BoxGroup("Clay")]
        [LabelText("Recover Speed")]
        [MinValue(0f)]
        public float RecoverSpeed = 6f;

        internal override MeshSimulationPresetBase CreateRuntimeCopy()
        {
            return new MeshClayTransientSimulationPreset
            {
                Radius = Radius,
                ImpactStrength = ImpactStrength,
                RecoverSpeed = RecoverSpeed,
            };
        }
    }

    [Serializable]
    public sealed class MeshClayPersistentSimulationPreset : MeshSimulationPresetBase
    {
        [BoxGroup("Clay")]
        [LabelText("Radius")]
        [MinValue(0.001f)]
        public float Radius = 0.5f;

        [BoxGroup("Clay")]
        [LabelText("Impact Strength")]
        [MinValue(0f)]
        public float ImpactStrength = 0.25f;

        [BoxGroup("Clay")]
        [LabelText("Recover Speed")]
        [MinValue(0f)]
        public float RecoverSpeed = 0f;

        internal override MeshSimulationPresetBase CreateRuntimeCopy()
        {
            return new MeshClayPersistentSimulationPreset
            {
                Radius = Radius,
                ImpactStrength = ImpactStrength,
                RecoverSpeed = RecoverSpeed,
            };
        }
    }

    [Serializable]
    public sealed class MeshFluidSimulationPreset : MeshSimulationPresetBase
    {
        [BoxGroup("Fluid")]
        [LabelText("Radius")]
        [MinValue(0.001f)]
        public float Radius = 0.8f;

        [BoxGroup("Fluid")]
        [LabelText("Wave Speed")]
        [MinValue(0f)]
        public float WaveSpeed = 2.5f;

        [BoxGroup("Fluid")]
        [LabelText("Band Width")]
        [MinValue(0.001f)]
        public float BandWidth = 0.2f;

        [BoxGroup("Fluid")]
        [LabelText("Spatial Frequency")]
        [MinValue(0f)]
        public float SpatialFrequency = 14f;

        [BoxGroup("Fluid")]
        [LabelText("Temporal Frequency")]
        [MinValue(0f)]
        public float TemporalFrequency = 8f;

        [BoxGroup("Fluid")]
        [LabelText("Wave Strength")]
        [MinValue(0f)]
        public float WaveStrength = 0.15f;

        [BoxGroup("Fluid")]
        [LabelText("Impact Scale")]
        [MinValue(0f)]
        public float ImpactScale = 0.1f;

        [BoxGroup("Fluid")]
        [LabelText("Max Amplitude")]
        [MinValue(0f)]
        public float MaxAmplitude = 0.2f;

        [BoxGroup("Fluid")]
        [LabelText("Max Active Ripples")]
        [MinValue(1)]
        public int MaxActiveRipples = 64;

        [BoxGroup("Fluid")]
        [LabelText("Distance Damping")]
        [MinValue(0f)]
        public float DistanceDamping = 1.1f;

        [BoxGroup("Fluid")]
        [LabelText("Distance Falloff Weight")]
        [Range(0f, 1f)]
        public float DistanceFalloffWeight = 0f;

        [BoxGroup("Fluid")]
        [LabelText("Edge Softness")]
        [Range(0f, 1f)]
        public float EdgeSoftness = 0.2f;

        [BoxGroup("Fluid")]
        [LabelText("Radial Blend")]
        [Range(0f, 1f)]
        public float RadialBlend = 0.2f;

        [BoxGroup("Fluid")]
        [LabelText("Radial Falloff Weight")]
        [Range(0f, 1f)]
        public float RadialFalloffWeight = 0f;

        [BoxGroup("Fluid")]
        [LabelText("Frequency Jitter")]
        [Range(0f, 0.5f)]
        public float FrequencyJitter = 0.1f;

        [BoxGroup("Fluid")]
        [LabelText("Damping")]
        [MinValue(0f)]
        public float Damping = 3f;

        internal override MeshSimulationPresetBase CreateRuntimeCopy()
        {
            return new MeshFluidSimulationPreset
            {
                Radius = Radius,
                WaveSpeed = WaveSpeed,
                BandWidth = BandWidth,
                SpatialFrequency = SpatialFrequency,
                TemporalFrequency = TemporalFrequency,
                WaveStrength = WaveStrength,
                ImpactScale = ImpactScale,
                MaxAmplitude = MaxAmplitude,
                MaxActiveRipples = MaxActiveRipples,
                DistanceDamping = DistanceDamping,
                DistanceFalloffWeight = DistanceFalloffWeight,
                EdgeSoftness = EdgeSoftness,
                RadialBlend = RadialBlend,
                RadialFalloffWeight = RadialFalloffWeight,
                FrequencyJitter = FrequencyJitter,
                Damping = Damping,
            };
        }
    }

    [Serializable]
    public sealed class MeshTrackVisualizerRuntimeMutation
    {
        bool ShowWaveMutationValues => ApplyWave && WaveEnabled;

        [LabelText("Replace Preset")]
        [Tooltip("Inspector setting.")]
        public bool ReplacePreset = false;

        [ShowIf(nameof(ReplacePreset))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackVisualizerPresetBase> Preset =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackVisualizerPresetBase>(new MeshLineTrackVisualizerPreset());

        [LabelText("Apply Width")]
        public bool ApplyWidth = false;

        [ShowIf(nameof(ApplyWidth))]
        [MinValue(0.001f)]
        public float BaseWidth = 0.08f;

        [LabelText("Apply Taper")]
        [Tooltip("Inspector setting.")]
        public bool ApplyTaper = false;

        [ShowIf(nameof(ApplyTaper))]
        [Range(0f, 1f)]
        public float HeadTaperNormalized = 0.1f;

        [ShowIf(nameof(ApplyTaper))]
        [Range(0f, 1f)]
        public float TailTaperNormalized = 0.1f;

        [LabelText("Apply Wave")]
        [Tooltip("Inspector setting.")]
        public bool ApplyWave = false;

        [ShowIf(nameof(ApplyWave))]
        public bool WaveEnabled = true;

        [ShowIf(nameof(ShowWaveMutationValues))]
        [MinValue(0f)]
        public float WaveAmplitude = 0f;

        [ShowIf(nameof(ShowWaveMutationValues))]
        [MinValue(0.001f)]
        public float WaveLength = 0.5f;

        [ShowIf(nameof(ShowWaveMutationValues))]
        public float WavePhase = 0f;

        [ShowIf(nameof(ShowWaveMutationValues))]
        public float WaveScrollSpeed = 0f;

        [LabelText("Apply Dash")]
        [Tooltip("Inspector setting.")]
        public bool ApplyDash = false;

        [ShowIf(nameof(ApplyDash))]
        public bool DashEnabled = false;

        [ShowIf(nameof(ApplyDash))]
        public MeshWaveSpace DashSpace = MeshWaveSpace.World;

        [ShowIf(nameof(ApplyDash))]
        public float DashScrollSpeed = 0f;

        [ShowIf(nameof(ApplyDash))]
        public float DashScrollOffset = 0f;

        [ShowIf(nameof(ApplyDash))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<MeshLineDashPatternElement> Pattern = new()
        {
            new MeshLineDashPatternElement
            {
                Kind = MeshLineDashPatternKind.Visible,
                Length = 0.5f,
            },
            new MeshLineDashPatternElement
            {
                Kind = MeshLineDashPatternKind.Gap,
                Length = 0.25f,
            },
        };

        public bool HasAnyMutation()
        {
            return ReplacePreset || ApplyWidth || ApplyTaper || ApplyWave || ApplyDash;
        }
    }

    [Serializable]
    public sealed class MeshTrackPlayerRuntimeMutation
    {
        [LabelText("Replace Preset")]
        [Tooltip("Inspector setting.")]
        public bool ReplacePreset = false;

        [ShowIf(nameof(ReplacePreset))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackPlayerPresetBase> Preset =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackPlayerPresetBase>(new MeshLineTrackPlayerPreset());

        [LabelText("Apply Condition")]
        [Tooltip("Inspector setting.")]
        public bool ApplyCondition = false;

        [ShowIf(nameof(ApplyCondition))]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [LabelText("Apply Points")]
        [Tooltip("Inspector setting.")]
        public bool ApplyPoints = false;

        [ShowIf(nameof(ApplyPoints))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<DynamicValue<Vector3>> Points = new();

        [LabelText("Apply Trail Config")]
        [Tooltip("Inspector setting.")]
        public bool ApplyTrailConfig = false;

        [ShowIf(nameof(ApplyTrailConfig))]
        public DynamicValue<Vector3> TargetPosition = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [ShowIf(nameof(ApplyTrailConfig))]
        [MinValue(0.01f)]
        public float DurationSeconds = 0.75f;

        [ShowIf(nameof(ApplyTrailConfig))]
        [MinValue(0f)]
        public float MinDistance = 0.05f;

        [ShowIf(nameof(ApplyTrailConfig))]
        [MinValue(0f)]
        public float MinTime = 0.03f;

        [ShowIf(nameof(ApplyTrailConfig))]
        [MinValue(2)]
        public int MaxPoints = 64;

        [LabelText("Apply Area Tag")]
        [Tooltip("Inspector setting.")]
        public bool ApplyAreaTag = false;

        [ShowIf(nameof(ApplyAreaTag))]
        public string AreaTag = "default";

        [LabelText("Apply Target Link Config")]
        [Tooltip("Inspector setting.")]
        public bool ApplyTargetLinkConfig = false;

        [ShowIf(nameof(ApplyTargetLinkConfig))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Self Actor Source\", SelfActorSource)")]
        public ActorSource SelfActorSource = new() { Kind = ActorSourceKind.Current };

        [ShowIf(nameof(ApplyTargetLinkConfig))]
        public string TargetChannelTag = "default";

        [ShowIf(nameof(ApplyTargetLinkConfig))]
        [MinValue(0)]
        public int TopN = 0;

        [ShowIf(nameof(ApplyTargetLinkConfig))]
        public MeshTargetLinkTopology Topology = MeshTargetLinkTopology.Independent;

        public bool HasAnyMutation()
        {
            return ReplacePreset || ApplyCondition || ApplyPoints || ApplyTrailConfig || ApplyAreaTag || ApplyTargetLinkConfig;
        }
    }

    [Serializable]
    public sealed class MeshTrackColliderRuntimeMutation
    {
        [LabelText("Replace Preset")]
        [Tooltip("Inspector setting.")]
        public bool ReplacePreset = false;

        [ShowIf(nameof(ReplacePreset))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackColliderPresetBase> Preset =
            MeshChannelDynamicValueFactory.FromManaged<MeshTrackColliderPresetBase>(new MeshPolygonTrackColliderPreset());

        [LabelText("Apply Sync Toggle")]
        [Tooltip("Inspector setting.")]
        public bool ApplySyncToggle = false;

        [ShowIf(nameof(ApplySyncToggle))]
        public bool SyncPolygonToCollider = true;

        [LabelText("Apply Hit Capture Toggle")]
        [Tooltip("Inspector setting.")]
        public bool ApplyHitCaptureToggle = false;

        [ShowIf(nameof(ApplyHitCaptureToggle))]
        public bool EnableHitCapture = true;

        [LabelText("Apply Sync Settings")]
        [Tooltip("Inspector setting.")]
        public bool ApplySyncSettings = false;

        [ShowIf(nameof(ApplySyncSettings))]
        [InlineProperty]
        public MeshPolygonSyncSettings Sync = new();

        public bool HasAnyMutation()
        {
            return ReplacePreset || ApplySyncToggle || ApplyHitCaptureToggle || ApplySyncSettings;
        }
    }

    [Serializable]
    public sealed class MeshTrackMaterialRuntimeMutation
    {
        [LabelText("Replace Preset")]
        [Tooltip("Inspector setting.")]
        public bool ReplacePreset = false;

        [ShowIf(nameof(ReplacePreset))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshTrackMaterialPreset> Preset =
            MeshChannelDynamicValueFactory.FromManaged(new MeshTrackMaterialPreset());

        [LabelText("Apply Enabled")]
        [Tooltip("Inspector setting.")]
        public bool ApplyEnabled = false;

        [ShowIf(nameof(ApplyEnabled))]
        public bool Enabled = true;

        [LabelText("Apply Base Tint")]
        [Tooltip("Inspector setting.")]
        public bool ApplyBaseTint = false;

        [ShowIf(nameof(ApplyBaseTint))]
        public Color BaseTint = Color.white;

        [LabelText("Apply Sorting Order Offset")]
        [Tooltip("Inspector setting.")]
        public bool ApplySortingOrderOffset = false;

        [ShowIf(nameof(ApplySortingOrderOffset))]
        public int SortingOrderOffset = 0;

        public bool HasAnyMutation()
        {
            return ReplacePreset || ApplyEnabled || ApplyBaseTint || ApplySortingOrderOffset;
        }
    }

    [Serializable]
    public sealed class MeshSimulationTrackRuntimeMutation
    {
        [LabelText("Replace Preset")]
        public bool ReplacePreset = false;

        [ShowIf(nameof(ReplacePreset))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshSimulationPresetBase> Preset =
            MeshChannelDynamicValueFactory.FromManaged<MeshSimulationPresetBase>(new MeshClayTransientSimulationPreset());

        [LabelText("Apply Priority")]
        public bool ApplyPriority = false;

        [ShowIf(nameof(ApplyPriority))]
        public int Priority = 0;

        [LabelText("Apply Enabled")]
        public bool ApplyEnabled = false;

        [ShowIf(nameof(ApplyEnabled))]
        public bool Enabled = true;

        public bool HasAnyMutation()
        {
            return ReplacePreset || ApplyPriority || ApplyEnabled;
        }
    }

    public readonly struct MeshHitContactInfo
    {
        public readonly Collider2D? OtherCollider;
        public readonly Vector2 ContactPoint;
        public readonly Vector2 ContactNormal;
        public readonly Vector2 RelativeVelocity;
        public readonly float ImpulseEstimate;
        public readonly float PenetrationEstimate;
        public readonly float StayTime;

        public MeshHitContactInfo(
            Collider2D? otherCollider,
            Vector2 contactPoint,
            Vector2 contactNormal,
            Vector2 relativeVelocity,
            float impulseEstimate,
            float penetrationEstimate,
            float stayTime)
        {
            OtherCollider = otherCollider;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal;
            RelativeVelocity = relativeVelocity;
            ImpulseEstimate = impulseEstimate;
            PenetrationEstimate = penetrationEstimate;
            StayTime = stayTime;
        }
    }

    [CreateAssetMenu(fileName = "MeshDefinitionPreset", menuName = "Game/Mesh Channel/Definition Preset")]
    public sealed class MeshDefinitionPresetSO : ScriptableObject, IDynamicValueAsset<MeshDefinitionPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshDefinitionPreset? _preset = new();

        public MeshDefinitionPreset? Preset
        {
            get
            {
                _preset ??= new MeshDefinitionPreset();
                return _preset;
            }
        }
    }

    [CreateAssetMenu(fileName = "MeshRenderPipelinePreset", menuName = "Game/Mesh Channel/Render Pipeline Preset")]
    public sealed class MeshRenderPipelinePresetSO : ScriptableObject, IDynamicValueAsset<MeshRenderPipelinePreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshRenderPipelinePreset? _preset = new();

        public MeshRenderPipelinePreset? Preset
        {
            get
            {
                _preset ??= new MeshRenderPipelinePreset();
                return _preset;
            }
        }
    }

    [CreateAssetMenu(fileName = "MeshTrackVisualizerPreset", menuName = "Game/Mesh Channel/Track Visualizer Preset")]
    public sealed class MeshTrackVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<MeshTrackVisualizerPresetBase>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshTrackVisualizerPresetBase? _preset = new MeshLineTrackVisualizerPreset();

        public MeshTrackVisualizerPresetBase? Preset
        {
            get
            {
                _preset ??= new MeshLineTrackVisualizerPreset();
                return _preset;
            }
        }
    }

    [CreateAssetMenu(fileName = "MeshTrackPlayerPreset", menuName = "Game/Mesh Channel/Track Player Preset")]
    public sealed class MeshTrackPlayerPresetSO : ScriptableObject, IDynamicValueAsset<MeshTrackPlayerPresetBase>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshTrackPlayerPresetBase? _preset = new MeshLineTrackPlayerPreset();

        public MeshTrackPlayerPresetBase? Preset
        {
            get
            {
                _preset ??= new MeshLineTrackPlayerPreset();
                return _preset;
            }
        }
    }

    [CreateAssetMenu(fileName = "MeshTrackColliderPreset", menuName = "Game/Mesh Channel/Track Collider Preset")]
    public sealed class MeshTrackColliderPresetSO : ScriptableObject, IDynamicValueAsset<MeshTrackColliderPresetBase>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshTrackColliderPresetBase? _preset = new MeshPolygonTrackColliderPreset();

        public MeshTrackColliderPresetBase? Preset
        {
            get => _preset;
        }
    }

    [CreateAssetMenu(fileName = "MeshTrackMaterialPreset", menuName = "Game/Mesh Channel/Track Material Preset")]
    public sealed class MeshTrackMaterialPresetSO : ScriptableObject, IDynamicValueAsset<MeshTrackMaterialPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshTrackMaterialPreset? _preset = new();

        public MeshTrackMaterialPreset? Preset
        {
            get
            {
                _preset ??= new MeshTrackMaterialPreset();
                return _preset;
            }
        }
    }

    [CreateAssetMenu(fileName = "MeshSimulationPreset", menuName = "Game/Mesh Channel/Simulation Preset")]
    public sealed class MeshSimulationPresetSO : ScriptableObject, IDynamicValueAsset<MeshSimulationPresetBase>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MeshSimulationPresetBase? _preset = new MeshClayTransientSimulationPreset();

        public MeshSimulationPresetBase? Preset
        {
            get
            {
                _preset ??= new MeshClayTransientSimulationPreset();
                return _preset;
            }
        }
    }
}

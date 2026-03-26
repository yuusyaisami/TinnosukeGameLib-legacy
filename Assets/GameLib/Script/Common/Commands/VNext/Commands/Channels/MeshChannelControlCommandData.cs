#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum MeshChannelControlOperation
    {
        SwapRootDefinition = 10,
        SwapTrackDefinition = 20,
        MutateTrackVisualizer = 30,
        MutateTrackPlayer = 40,
        MutateTrackCollider = 50,
        MutateTrackMaterial = 60,
        MutateSimulationTrack = 70,
        ResetRuntimeOverrides = 80,
        SetTrackEnabled = 90,
    }

    [Serializable]
    public sealed class MeshChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.MeshChannelControl;

        public string DebugData => $"Hub={HubSource.Kind} Tag={Tag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        [Tooltip("MeshChannelHub を解決する ActorSource です。Current の場合は現在の scope から hub を探します。")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("操作対象の MeshChannel tag です。Hub 内で player runtime を引くキーになります。")]
        public string Tag = "default";

        [BoxGroup("Operation")]
        [EnumToggleButtons]
        [Tooltip("実行する MeshChannel 制御の種類です。Track 定義差し替え、mutation、enabled 切り替えなどを選びます。")]
        public MeshChannelControlOperation Operation = MeshChannelControlOperation.SwapTrackDefinition;

        [BoxGroup("Root")]
        [ShowIf(nameof(UsesRootDefinition))]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<MeshDefinitionPreset> RootDefinition =
            MeshChannelDynamicValueFactory.FromManaged(new MeshDefinitionPreset());

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesTrackKey))]
        [LabelText("Track Key")]
        [Tooltip("操作対象の track key です。same-tag blend 用の Tag ではなく、track 自体の識別子を指定します。")]
        public string TrackKey = string.Empty;

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesTrackDefinition))]
        [InlineProperty]
        [HideLabel]
        public MeshTrackDefinition TrackDefinition = new();

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesVisualizerMutation))]
        [InlineProperty]
        [HideLabel]
        public MeshTrackVisualizerRuntimeMutation VisualizerMutation = new();

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        public MeshTrackPlayerRuntimeMutation PlayerMutation = new();

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesColliderMutation))]
        [InlineProperty]
        [HideLabel]
        public MeshTrackColliderRuntimeMutation ColliderMutation = new();

        [BoxGroup("Track")]
        [ShowIf(nameof(UsesMaterialMutation))]
        [InlineProperty]
        [HideLabel]
        public MeshTrackMaterialRuntimeMutation MaterialMutation = new();

        [BoxGroup("Simulation")]
        [ShowIf(nameof(UsesSimulationMutation))]
        [InlineProperty]
        [HideLabel]
        public MeshSimulationTrackRuntimeMutation SimulationMutation = new();

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        public bool ResetVisualizer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        public bool ResetPlayer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        public bool ResetCollider = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        public bool ResetMaterial = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        public bool ResetSimulation = true;

        [BoxGroup("State")]
        [ShowIf(nameof(UsesEnabled))]
        public bool Enabled = true;

        bool UsesRootDefinition => Operation == MeshChannelControlOperation.SwapRootDefinition;
        bool UsesTrackKey =>
            Operation != MeshChannelControlOperation.SwapRootDefinition;
        bool UsesTrackDefinition => Operation == MeshChannelControlOperation.SwapTrackDefinition;
        bool UsesVisualizerMutation => Operation == MeshChannelControlOperation.MutateTrackVisualizer;
        bool UsesPlayerMutation => Operation == MeshChannelControlOperation.MutateTrackPlayer;
        bool UsesColliderMutation => Operation == MeshChannelControlOperation.MutateTrackCollider;
        bool UsesMaterialMutation => Operation == MeshChannelControlOperation.MutateTrackMaterial;
        bool UsesSimulationMutation => Operation == MeshChannelControlOperation.MutateSimulationTrack;
        bool UsesReset => Operation == MeshChannelControlOperation.ResetRuntimeOverrides;
        bool UsesEnabled => Operation == MeshChannelControlOperation.SetTrackEnabled;
    }
}

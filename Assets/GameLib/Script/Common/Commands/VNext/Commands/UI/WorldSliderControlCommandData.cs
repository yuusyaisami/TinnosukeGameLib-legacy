#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WorldSliderControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.WorldSliderControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Op={Operation} SwapV={ApplyVisualizerPreset} SwapP={ApplyPlayerPreset} MutV={ApplyVisualizerMutation} MutP={ApplyPlayerMutation} ResetV={ResetVisualizer} ResetP={ResetPlayer}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("IWorldSliderControlService を持つ target scope を指定します。通常は操作したい WorldSlider 自身を指します。")]
        public ActorSource Target;

        [BoxGroup("Operation")]
        [LabelText("Operation")]
        [Tooltip("Preset の差し替え、現在の runtime 設定の部分変更、runtime override のリセットを切り替えます。")]
        public WorldSliderControlOperation Operation = WorldSliderControlOperation.MutateSettings;

        [BoxGroup("Swap")]
        [ShowIf(nameof(IsSwapOperation))]
        [ToggleLeft]
        [LabelText("Apply Visualizer Preset")]
        [Tooltip("有効な場合、Visualizer 側の base/effective preset を指定 preset へ完全に差し替えます。")]
        public bool ApplyVisualizerPreset;

        [BoxGroup("Swap")]
        [ShowIf(nameof(ShouldShowVisualizerPreset))]
        [LabelText("Visualizer Preset")]
        [Tooltip("差し替え先の visualizer preset です。適用時は以前の visualizer runtime state を破棄して新しい preset で再構築します。")]
        public DynamicValue<WorldSliderVisualizerPreset> VisualizerPreset =
            DynamicValue<WorldSliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderVisualizerPreset>(new WorldSliderVisualizerPreset()));

        [BoxGroup("Swap")]
        [ShowIf(nameof(IsSwapOperation))]
        [ToggleLeft]
        [LabelText("Apply Player Preset")]
        [Tooltip("有効な場合、Player 側の base/effective preset を指定 preset へ完全に差し替えます。")]
        public bool ApplyPlayerPreset;

        [BoxGroup("Swap")]
        [ShowIf(nameof(ShouldShowPlayerPreset))]
        [LabelText("Player Preset")]
        [Tooltip("差し替え先の player preset です。binding、range、transition、player commands をまとめて入れ替えます。")]
        public DynamicValue<WorldSliderPlayerPreset> PlayerPreset =
            DynamicValue<WorldSliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderPlayerPreset>(new WorldSliderPlayerPreset()));

        [BoxGroup("Mutate")]
        [ShowIf(nameof(IsMutateOperation))]
        [ToggleLeft]
        [LabelText("Apply Visualizer Mutation")]
        [Tooltip("有効な場合、現在の visualizer effective preset に対して partial mutate を適用します。source preset 自体は変更しません。")]
        public bool ApplyVisualizerMutation;

        [BoxGroup("Mutate")]
        [ShowIf(nameof(ShouldShowVisualizerMutation))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("visualizer の runtime 設定を部分変更します。mode、backend、layout、spawn command などを更新できます。")]
        public WorldSliderVisualizerRuntimeMutation VisualizerMutation = new();

        [BoxGroup("Mutate")]
        [ShowIf(nameof(IsMutateOperation))]
        [ToggleLeft]
        [LabelText("Apply Player Mutation")]
        [Tooltip("有効な場合、現在の player effective preset に対して partial mutate を適用します。source preset 自体は変更しません。")]
        public bool ApplyPlayerMutation;

        [BoxGroup("Mutate")]
        [ShowIf(nameof(ShouldShowPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("player の runtime 設定を部分変更します。binding、range、transition、display mode、player commands を更新できます。")]
        public WorldSliderPlayerRuntimeMutation PlayerMutation = new();

        [BoxGroup("Reset")]
        [ShowIf(nameof(IsResetOperation))]
        [ToggleLeft]
        [LabelText("Reset Visualizer")]
        [Tooltip("有効な場合、visualizer の runtime override を破棄して WorldSliderMB の source preset へ戻します。")]
        public bool ResetVisualizer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(IsResetOperation))]
        [ToggleLeft]
        [LabelText("Reset Player")]
        [Tooltip("有効な場合、player の runtime override を破棄して WorldSliderMB の source preset へ戻します。")]
        public bool ResetPlayer = true;

        bool IsSwapOperation() => Operation == WorldSliderControlOperation.SwapPreset;
        bool IsMutateOperation() => Operation == WorldSliderControlOperation.MutateSettings;
        bool IsResetOperation() => Operation == WorldSliderControlOperation.ResetRuntimeOverrides;
        bool ShouldShowVisualizerPreset() => IsSwapOperation() && ApplyVisualizerPreset;
        bool ShouldShowPlayerPreset() => IsSwapOperation() && ApplyPlayerPreset;
        bool ShouldShowVisualizerMutation() => IsMutateOperation() && ApplyVisualizerMutation;
        bool ShouldShowPlayerMutation() => IsMutateOperation() && ApplyPlayerMutation;
    }
}

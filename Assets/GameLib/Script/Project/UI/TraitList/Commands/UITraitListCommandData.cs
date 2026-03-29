#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BuildUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.BuildUITraitList;
        public string DebugData
        {
            get
            {
                var assetName = ProfileSource.Asset != null ? ProfileSource.Asset.name : "null";
                if (ProfileSource.PreferVar && ProfileSource.VarId != 0)
                    return $"VarId={ProfileSource.VarId} Asset={assetName} Key={HolderKey}";
                return $"Asset={assetName} Key={HolderKey}";
            }
        }

        [Tooltip("使用する UITraitListProfileSO。Var 参照が有効なら実行時に差し替えられ、無効なら Asset 側をそのまま使います。")]
        public VarUnityObjectSource<UITraitListProfileSO> ProfileSource = new();
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        [Tooltip("TraitHolderHubService を持つ Actor。ここから HolderKey を使って表示対象 holder を解決します。")]
        public ActorSource HolderHubSource;
        [Tooltip("Build 対象の TraitHolder キー。HolderHubSource 側の TraitHolderHubMB に登録されている key を指定します。")]
        public string HolderKey = string.Empty;
        [InlineProperty]
        [Tooltip("今回の Build で表示する範囲。Profile の DefaultRange よりこちらが優先されます。")]
        public UITraitListRange Range;
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(BuildScope)")]
        [Tooltip("UITraitListBuilderService を解決する対象 Actor。通常は UITraitListSystemMB が付いている scope を指定します。")]
        public ActorSource BuildScope;
    }

    [Serializable]
    public sealed class RefreshUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshUITraitList;
        public string DebugData => $"Mode={RefreshMode}";

        [Tooltip("差分更新にするか、全 rebuild にするかの方針。通常は Incremental、見た目崩れや構成差分が大きい場合は Full が向きます。")]
        public UITraitListRefreshMode RefreshMode = UITraitListRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class SetUITraitListRangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetUITraitListRange;
        public string DebugData => $"Start={Range.StartIndex} Count={Range.Count} Rebuild={Rebuild}";

        [InlineProperty]
        [Tooltip("更新後の表示範囲。現在の runtime に対してこの範囲へ切り替えます。")]
        public UITraitListRange Range;
        [Tooltip("true の場合、Range 設定後に即 rebuild/refresh を行います。false の場合は範囲だけ変えて描画更新は別タイミングで行います。")]
        public bool Rebuild = true;
    }

    [Serializable]
    public sealed class ClearUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearUITraitList;
        public string DebugData => $"KeepBinding={KeepBinding}";

        [Tooltip("true の場合、表示だけ消して BoundHolder / BoundProfile の紐付けは維持します。false の場合は binding も解除します。")]
        public bool KeepBinding = false;
    }

    [Serializable]
    public sealed class AddTraitToHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.AddTraitToHolder;
        public string DebugData => $"UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [LabelText("Trait")]
        [Tooltip("追加する Trait。AssetTraitDefinitionSource、Var、Blackboard などから解決する。")]
        public DynamicValue<TraitDefinitionSO> TraitDefinition;
        [Tooltip("現在 UITraitList に bind されている holder を使うかどうか。true なら HolderActorSource/HolderKey は無視されます。")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("TraitHolder を保有する対象 Actor。ここから TraitHolderHubService を解決して HolderKey を参照する。")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        [Tooltip("UseBoundHolder が false のときに使う holder key。HolderActorSource 側の TraitHolderHubMB に登録されている key を指定します。")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class RemoveTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={TargetHolder.Kind} Key={TargetHolderKey}";

        [InlineProperty]
        [Tooltip("削除対象 Trait の選択条件。DynamicValue の評価元はコマンド実行者側で、Target Holder には引っ張られません。")]
        public TraitElementSelector Selector;
        [Tooltip("現在 UITraitList に bind されている holder を使うかどうか。true なら TargetHolder/TargetHolderKey は無視されます。")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [FormerlySerializedAs("TargetHolderActorSource")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetHolder)")]
        [Tooltip("Trait を外す先の Target Holder を持つ Actor。Selector の DynamicValue はこの Actor ではなく、コマンド実行者側の文脈で評価されます。")]
        public ActorSource TargetHolder;

        [ShowIf("@!UseBoundHolder")]
        [FormerlySerializedAs("HolderKey")]
        [Tooltip("UseBoundHolder が false のときに使う Target Holder key。実際に Trait を remove する holder をこの key で解決します。")]
        public string TargetHolderKey = string.Empty;
    }

    [Serializable]
    public sealed class UseTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.UseTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [InlineProperty]
        [Tooltip("使用対象 Trait の選択条件。定義・instanceId・index などから 1 件解決して use を実行します。")]
        public TraitElementSelector Selector;
        [Tooltip("現在 UITraitList に bind されている holder を使うかどうか。true なら HolderActorSource/HolderKey は無視されます。")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("TraitHolder を保有する対象 Actor。ここから TraitHolderHubService を解決して HolderKey を参照する。")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        [Tooltip("UseBoundHolder が false のときに使う holder key。使用対象 holder をこの key で解決します。")]
        public string HolderKey = string.Empty;
    }

    public enum UITraitTargetKind
    {
        ByDefinition = 0,
        ByInstanceId = 1,
        ByIndex = 2,
        ByRowAndColumn = 3,
    }

    [Serializable]
    public struct UITraitTarget
    {
        [EnumToggleButtons]
        [Tooltip("どの方法で UI 上の Trait を特定するか。Definition / InstanceId / Index / Row+Column から選びます。")]
        public UITraitTargetKind Kind;

        [ShowIf("@Kind == UITraitTargetKind.ByDefinition")]
        [LabelText("Definition")]
        [Tooltip("Kind=ByDefinition のときに使う TraitDefinition。最初に一致したスロットを対象にします。")]
        public DynamicValue<TraitDefinitionSO> Definition;

        [ShowIf("@Kind == UITraitTargetKind.ByInstanceId")]
        [Tooltip("Kind=ByInstanceId のときに使う Trait instanceId。完全一致でスロットを探します。")]
        public string InstanceId;

        [ShowIf("@Kind == UITraitTargetKind.ByIndex")]
        [Tooltip("Kind=ByIndex のときに使う一覧 index。現在の表示範囲内インデックスではなく holder 全体の trait index を想定します。")]
        public DynamicValue<int> TraitIndex;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        [Tooltip("Kind=ByRowAndColumn のときに使う row 値。現在のレイアウト上の行番号で対象を探します。")]
        public DynamicValue<int> Row;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        [Tooltip("Kind=ByRowAndColumn のときに使う column 値。Row と組み合わせて現在のレイアウト上のスロットを特定します。")]
        public DynamicValue<int> Column;
    }
}

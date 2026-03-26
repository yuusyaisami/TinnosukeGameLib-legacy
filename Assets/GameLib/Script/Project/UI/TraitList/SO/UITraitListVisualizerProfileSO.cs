#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI.TraitList
{
    [CreateAssetMenu(
        fileName = "UITraitListVisualizerProfile",
        menuName = "Game/UI/Trait List/Visualizer Profile")]
    public sealed class UITraitListVisualizerProfileSO : ScriptableObject
    {
        [BoxGroup("Spawn")]
        [Tooltip("UI 要素を Prefab から生成するか、RuntimeTemplate から生成するかを選びます。")]
        public UITraitListSpawnSource SpawnSource = UITraitListSpawnSource.Prefab;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.RuntimeTemplate)]
        [InlineProperty, HideLabel]
        [Tooltip("SpawnSource が RuntimeTemplate のときに使う RuntimeTemplatePreset。実行時に template SO へ解決されます。")]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.Prefab)]
        [Tooltip("SpawnSource が Prefab のときに生成するプレハブ。UITraitListSlot の見た目本体になります。")]
        public GameObject? Prefab;

        [BoxGroup("Spawn")]
        [Tooltip("生成物を pool 対応で扱うかどうか。頻繁に Build / Refresh / Clear する一覧では true 推奨です。")]
        public bool AllowPooling = true;

        [BoxGroup("Spawn")]
        [Tooltip("生成した UI 要素の親を profile 側で強制したい場合に指定します。未指定時は system / build 時に与えられた親を使います。")]
        public Transform? SpawnParentOverride;

        [BoxGroup("Spawn")]
        [LabelText("Override Size")]
        [Tooltip("生成した要素の RectTransform サイズを profile 側で固定上書きします。Prefab 既定サイズを無視したい場合に使います。")]
        public bool OverrideSize;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Width")]
        [Min(0f)]
        [Tooltip("Override Size 有効時の横幅。Layout 計算や配置見た目を profile 側で統一したいときに指定します。")]
        public float Width;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Height")]
        [Min(0f)]
        [Tooltip("Override Size 有効時の高さ。Width とセットで slot の矩形サイズを明示したいときに使います。")]
        public float Height;

        [BoxGroup("Commands")]
        [CommandListFunctionName("UITraitList.Spawn")]
        [Tooltip("各 UI 要素の spawn 直後に共通で実行するコマンド。Trait の Blackboard/RichText を読んで初期表示を作る用途に使います。")]
        public CommandListData SpawnCommands = new();

        [BoxGroup("Commands")]
        [Tooltip("TraitDefinition ごとの差し替えコマンド。共通 SpawnCommands の後に、定義一致したものだけ追加実行されます。")]
        public List<UITraitDefinitionCommand> ByDefinition = new();

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!RuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        void BindDebugOwners()
        {
            SpawnCommands?.BindDebugOwner(this, nameof(SpawnCommands));
            if (ByDefinition == null)
                return;
            for (int i = 0; i < ByDefinition.Count; i++)
            {
                var entry = ByDefinition[i];
                entry.Commands?.BindDebugOwner(this, $"ByDefinition[{i}].Commands");
            }
        }

    }

    [Serializable]
    public struct UITraitDefinitionCommand
    {
        [Tooltip("この TraitDefinition に一致したときだけ Commands を実行します。定義ごとの見た目差し替えや追加装飾に使います。")]
        public TraitDefinitionSO? Definition;
        [Tooltip("Definition に一致したスロットに対して追加で流すコマンド。個別アイコン変更、色変化、補助 UI 表示などに使えます。")]
        public CommandListData Commands;
    }
}

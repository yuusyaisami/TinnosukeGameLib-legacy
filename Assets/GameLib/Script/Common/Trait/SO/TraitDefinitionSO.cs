#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Animation;
using Game.Channel;
using Game.Common;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    public interface ITraitDefinition
    {
        string DefinitionId { get; }
        ITraitInstance CreateInstance(TraitInstanceContext context);
        string RefKeyPrefix { get; }
        TransformAnimationPreset? TraitListMovePreset { get; }
        PlaceableTraitSettings PlaceableSettings { get; }
    }

    public interface ITraitInstance
    {
        string InstanceId { get; }
        ITraitDefinition Definition { get; }
        TraitInstanceContext Context { get; }
        void OnLtsInstantiated(IScopeNode scope);
        void OnAdded();
        void OnHold();
        void OnUse();
        void OnRemove();
    }

    public class TraitInstanceContext
    {
        public IScopeNode? Scope { get; }
        public Transform? Owner => Scope?.Identity?.SelfTransform;
        public IObjectResolver? Resolver => Scope?.Resolver;
        public VarStore Vars { get; }

        public TraitInstanceContext(IScopeNode? scope, VarStore? vars = null)
        {
            Scope = scope;
            Vars = vars ?? new VarStore();
        }
    }
    [CreateAssetMenu(fileName = "TraitDefinition", menuName = "Game/Trait/Trait Definition SO", order = 1)]
    public class TraitDefinitionSO : ScriptableObject, ITraitDefinition, IRichTextDescribableTrait
    {
        [BoxGroup("Trait Info")]
        [LabelText("Definition ID")]  // ゲーム内で一意のID
        [SerializeField]
        [FormerlySerializedAs("_itemId")]
        string _definitionId = string.Empty;

        [BoxGroup("Trait Info")]
        [LabelText("Name")]
        [SerializeField]
        RichTextTemplateData _name = new();

        [BoxGroup("Trait Info")]
        [LabelText("Description")]
        [SerializeField]
        RichTextTemplateData _description = new();

        [BoxGroup("Trait Info")]
        [LabelText("Weight")]
        [Tooltip("Trait 抽選時の選ばれやすさ。0 以下は抽選対象外。")]
        [MinValue(0f)]
        [SerializeField]
        float _weight = 1f;

        [BoxGroup("Visual")]
        [SerializeField]
        VisualSettings _visualSettings = new();

        [BoxGroup("Visual")]
        [LabelText("Trait List Move Preset")]
        [Tooltip("UITraitList の RelayoutAnimation で使う移動プリセット。各 Trait 側で決める。")]
        [SerializeField]
        TransformAnimationPreset? _traitListMovePreset;

        [BoxGroup("Commands")]
        [LabelText("Run On Added")]
        [SerializeField]
        bool _runOnAddedCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnAddedCommands))]
        [LabelText("On Added Commands")]
        [Tooltip("TraitDefinitionSO が Trait を Holder に追加した時に実行します。実行主体は TraitDefinitionSO で、VarStore には実際の Trait instance データが入ります。")]
        [SerializeField]
        VNext.CommandListData _onAddedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Run On Equip")]
        [SerializeField]
        bool _runOnHoldCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnHoldCommands))]
        [LabelText("On Equip Commands")]
        [Tooltip("TraitDefinitionSO が Trait を Hold した時に実行します。実行主体は TraitDefinitionSO で、VarStore には実際の Trait instance データが入ります。")]
        [SerializeField]
        VNext.CommandListData _onHoldCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Run On Use")]
        [SerializeField]
        bool _runOnUseCommands = true;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnUseCommands))]
        [LabelText("On Use Commands")]
        [Tooltip("TraitDefinitionSO が Trait を Use した時に実行します。実行主体は TraitDefinitionSO で、VarStore には実際の Trait instance データが入ります。")]
        [SerializeField]
        VNext.CommandListData _onUseCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Run On Remove")]
        [SerializeField]
        bool _runOnRemoveCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnRemoveCommands))]
        [LabelText("On Remove Commands")]
        [Tooltip("TraitDefinitionSO が Trait を Remove した時に実行します。実行主体は TraitDefinitionSO で、VarStore には実際の Trait instance データが入ります。")]
        [SerializeField]
        VNext.CommandListData _onRemoveCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On LTS Instantiated Commands")]
        [Tooltip("TraitDefinitionSO が Trait の LTS instantiate hook で実行します。実行主体は TraitDefinitionSO で、VarStore には実際の Trait instance データが入ります。")]
        [SerializeField]
        VNext.CommandListData _onLtsInstantiatedCommands = new();

        [BoxGroup("VarStore")]
        [LabelText("Apply Common Vars")]
        [SerializeField]
        bool _applyCommonVars;

        [BoxGroup("VarStore")]
        [ShowIf(nameof(_applyCommonVars))]
        [LabelText("Common Vars")]
        [SerializeField]
        VarStorePayload _commonVars = new();

        [BoxGroup("VarStore Grid")]
        [LabelText("Apply Common Grid Table")]
        [SerializeField]
        bool _applyCommonGridTable;

        [BoxGroup("VarStore Grid")]
        [ShowIf(nameof(_applyCommonGridTable))]
        [LabelText("Grid Table Key")]
        [Tooltip("Grid row/column count の参照キーとして各セルに書き込む VarId です。0 の場合は既存のセル VarId のみを書き込みます。")]
        [VarIdDropdown]
        [SerializeField]
        int _commonGridTableKeyVarId;

        [BoxGroup("VarStore Grid")]
        [ShowIf(nameof(_applyCommonGridTable))]
        [LabelText("Common Grid Table")]
        [InlineProperty]
        [SerializeField]
        TraitGridTablePayload _commonGridTable = new();


        [BoxGroup("Placement")]
        [LabelText("Placeable")]
        [InlineProperty]
        [SerializeField]
        PlaceableTraitSettings _placeableSettings = new();

        public string DefinitionId => _definitionId;
        public virtual string RefKeyPrefix => "trait";
        public RichTextTemplateData? Description => _description;
        public RichTextTemplateData? Name => _name;
        public VisualSettings VisualSettings => _visualSettings;
        public TransformAnimationPreset? TraitListMovePreset => _traitListMovePreset;
        public float Weight => Mathf.Max(0f, _weight);
        public PlaceableTraitSettings PlaceableSettings => _placeableSettings;

        public virtual ITraitInstance CreateInstance(TraitInstanceContext context)
        {
            ApplyCommonVars(context);
            var instance = new TraitDefinitionInstance(this, context);
            var vars = context.Vars;
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.definitionId, DynamicVariant.FromString(_definitionId));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.instanceId, DynamicVariant.FromString(instance.InstanceId));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.definitionAsset, DynamicVariant.FromUnityObject(this));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.weight, DynamicVariant.FromFloat(Weight));
            if (_name != null)
                vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.nameTemplate, DynamicVariant.FromString(_name.Template ?? string.Empty));
            if (_description != null)
                vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.descriptionTemplate, DynamicVariant.FromString(_description.Template ?? string.Empty));

            var visualSettings = _visualSettings;
            if (visualSettings != null)
            {
                if (VarIds.GameLib.Base.VisualSetting.defaultAnim != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.defaultAnim, visualSettings.DefaultAnim);
                if (VarIds.GameLib.Base.VisualSetting.focusAnim != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.focusAnim, visualSettings.FocusAnim);
                if (VarIds.GameLib.Base.VisualSetting.InteractAnim != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.InteractAnim, visualSettings.InteractAnim);
                if (VarIds.GameLib.Base.VisualSetting.disableAnim != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.disableAnim, visualSettings.DisableAnim);
            }

            return instance;
        }

        public void ApplyCommonVars(IVarStore destination, bool overwrite = true)
        {
            if (!_applyCommonVars)
                return;

            if (destination == null || _commonVars == null)
                return;

            _commonVars.ApplyTo(destination, overwrite);
        }

        public void ApplyCommonGridTable(IGridBlackboardService? grid, bool overwrite = true)
        {
            if (!_applyCommonGridTable)
                return;

            if (grid == null || _commonGridTable == null || !_commonGridTable.HasTable)
                return;

            var tableKeyVarId = _commonGridTableKeyVarId;
            var hasTableKey = tableKeyVarId != 0;
            var tableKeyValue = DynamicVariant.FromBool(true);

            var rows = _commonGridTable.Rows;
            for (int row = 0; row < rows.Count; row++)
            {
                var columns = rows[row]?.Columns;
                if (columns == null)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var cell = columns[column];
                    var vars = cell?.Vars;
                    if (vars == null)
                        continue;

                    if (hasTableKey)
                    {
                        GridBlackboardWriteUtility.TryWriteCellValue(grid, row, column, tableKeyVarId, in tableKeyValue, overwrite, upsert: true);
                    }

                    for (int varIndex = 0; varIndex < vars.Count; varIndex++)
                    {
                        var varPayload = vars[varIndex];
                        if (varPayload == null)
                            continue;

                        if (varPayload.VarId == 0)
                        {
                            Debug.LogError($"[TraitDefinitionSO] CommonGridTable has var entry with empty VarKey. definition={_definitionId} row={row} col={column} index={varIndex}");
                            continue;
                        }

                        if (!varPayload.TryToVariant(out var value))
                        {
                            Debug.LogError($"[TraitDefinitionSO] CommonGridTable value conversion failed. definition={_definitionId} row={row} col={column} index={varIndex} varId={varPayload.VarId}");
                            continue;
                        }

                        if (!overwrite && grid.TryGetVariant(varPayload.VarId, row, column, out var _))
                            continue;

                        if (!GridBlackboardWriteUtility.TryWriteCellValue(grid, row, column, varPayload.VarId, in value, overwrite, upsert: true))
                        {
                            Debug.LogError($"[TraitDefinitionSO] CommonGridTable write failed. definition={_definitionId} row={row} col={column} varId={varPayload.VarId} kind={value.Kind}");
                        }
                    }
                }
            }
        }

        protected virtual void OnHold(ITraitInstance instance)
        {
            if (_runOnHoldCommands)
                ExecuteCommands(instance.Context, _onHoldCommands);
        }

        protected virtual void OnAdded(ITraitInstance instance)
        {
            if (_runOnAddedCommands)
                ExecuteCommands(instance.Context, _onAddedCommands);
        }

        protected virtual void OnUse(ITraitInstance instance)
        {
            if (_runOnUseCommands)
                ExecuteCommands(instance.Context, _onUseCommands);
        }

        protected virtual void OnRemove(ITraitInstance instance)
        {
            if (_runOnRemoveCommands)
                ExecuteCommands(instance.Context, _onRemoveCommands);
        }

        protected virtual void OnLtsInstantiated(ITraitInstance instance, IScopeNode scope)
        {
            _visualSettings?.OnLtsInstantiated(scope);
            ExecuteCommands(instance.Context, _onLtsInstantiatedCommands);
        }

        protected void ExecuteCommands(TraitInstanceContext context, VNext.CommandListData? commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            var scope = context.Scope;
            if (scope == null)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            resolver.TryResolve(out VNext.ICommandRunner? runner);
            if (runner == null)
                return;

            var ctx = new VNext.CommandContext(scope, context.Vars, runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            });
        }

        protected void ApplyCommonVars(TraitInstanceContext context)
        {
            if (!_applyCommonVars)
                return;

            if (_commonVars == null)
                return;

            _commonVars.ApplyTo(context.Vars, overwrite: true);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrEmpty(_definitionId))
                _definitionId = name;
        }
#endif

        sealed class TraitDefinitionInstance : ITraitInstance
        {
            readonly TraitDefinitionSO _definition;
            readonly string _instanceId = Guid.NewGuid().ToString("N");

            public ITraitDefinition Definition => _definition;
            public TraitInstanceContext Context { get; }
            public string InstanceId => _instanceId;

            public TraitDefinitionInstance(TraitDefinitionSO definition, TraitInstanceContext context)
            {
                _definition = definition;
                Context = context;
            }

            public void OnHold()
            {
                _definition.OnHold(this);
            }

            public void OnLtsInstantiated(IScopeNode scope)
            {
                _definition.OnLtsInstantiated(this, scope);
            }

            public void OnAdded()
            {
                _definition.OnAdded(this);
            }


            public void OnUse()
            {
                _definition.OnUse(this);
            }

            public void OnRemove()
            {
                _definition.OnRemove(this);
            }
        }
    }

    /// <summary>
    /// Trait の汎用的な見た目設定。
    /// 将来的に LTS のインスタンス化時に登録される前提のデータ。
    /// </summary>
    [Serializable]
    public sealed class VisualSettings
    {
        [BoxGroup("Animations")]
        [LabelText("Default Anim")]
        public AnimationSpritePreset DefaultAnim = new();

        [BoxGroup("Animations")]
        [LabelText("Focus Anim")]
        public AnimationSpritePreset FocusAnim = new();
        [BoxGroup("Animations")]
        [LabelText("Interact Anim")]
        public AnimationSpritePreset InteractAnim = new();

        [BoxGroup("Animations")]
        [LabelText("Disable Anim")]
        public AnimationSpritePreset DisableAnim = new();
        /// <summary>
        /// LTS によってインスタンス化された直後に呼ばれる想定のフック。
        /// 現時点では未使用。
        /// </summary>
        public void OnLtsInstantiated(IScopeNode scope)
        {
            // Intentionally left blank. Reserved for future integration.
            if (scope.Resolver.TryResolve<IBlackboardService>(out var bb))
            {
                bb.LocalVars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.defaultAnim, DefaultAnim);
                bb.LocalVars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.focusAnim, FocusAnim);
                bb.LocalVars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.InteractAnim, InteractAnim);
                bb.LocalVars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.disableAnim, DisableAnim);
            }
        }
    }
}

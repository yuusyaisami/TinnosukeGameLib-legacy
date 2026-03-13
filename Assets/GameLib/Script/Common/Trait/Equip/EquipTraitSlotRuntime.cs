#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VContainer;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    /// <summary>
    /// EquipTraitHolder の1スロット。
    /// 最大1つの ITraitInstance を「装備」として保持し、
    /// Equip / Unequip 時にコマンドを実行する。
    /// </summary>
    public sealed class EquipTraitSlotRuntime
    {
        readonly IScopeNode? _scope;
        readonly string _slotKey;

        ITraitInstance? _equipped;
        VarStore _slotVars = new();

        bool _runOnEquipCommands;
        VNext.CommandListData _onEquipCommands = new();
        bool _runOnUnequipCommands;
        VNext.CommandListData _onUnequipCommands = new();

        public EquipTraitSlotRuntime(IScopeNode? scope, string slotKey)
        {
            _scope = scope;
            _slotKey = slotKey ?? string.Empty;
        }

        /// <summary>現在装備中の Trait。null なら空スロット。</summary>
        public ITraitInstance? Equipped => _equipped;

        /// <summary>スロットキー。</summary>
        public string SlotKey => _slotKey;

        /// <summary>このスロットの VarStore（Equip/Unequip コマンドで参照可能）。</summary>
        public VarStore SlotVars => _slotVars;

        /// <summary>装備中か。</summary>
        public bool IsOccupied => _equipped != null;

        public event Action<EquipTraitSlotRuntime>? OnEquipped;
        public event Action<EquipTraitSlotRuntime>? OnUnequipped;

        // ───────────────── Configuration ─────────────────

        internal void SetSlotCommands(
            bool runOnEquip,
            VNext.CommandListData? onEquipCommands,
            bool runOnUnequip,
            VNext.CommandListData? onUnequipCommands)
        {
            _runOnEquipCommands = runOnEquip;
            _runOnUnequipCommands = runOnUnequip;
            _onEquipCommands = onEquipCommands ?? new VNext.CommandListData();
            _onUnequipCommands = onUnequipCommands ?? new VNext.CommandListData();
        }

        // ───────────────── Operations ─────────────────

        /// <summary>
        /// Trait を装備する。既に装備中の場合は先に Unequip してから装備する。
        /// </summary>
        /// <param name="instance">装備する Trait。</param>
        /// <param name="awaitUnequip">true の場合、Unequip コマンド完了を待ってから Equip する。</param>
        /// <param name="payload">Equip 時に SlotVars へマージする追加変数。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public async UniTask EquipAsync(
            ITraitInstance instance,
            bool awaitUnequip,
            VarStorePayload? payload,
            CancellationToken ct)
        {
            if (instance == null) return;

            // 既に同じ Trait が装備されている場合は何もしない
            if (ReferenceEquals(_equipped, instance)) return;

            // 既に別の Trait が装備中なら Unequip
            if (_equipped != null)
            {
                if (awaitUnequip)
                    await UnequipAsync(ct);
                else
                    UnequipImmediate();
            }

            _equipped = instance;
            _slotVars = new VarStore();

            // Trait 側の Context.Vars を SlotVars にコピー
            var traitVars = instance.Context?.Vars;
            if (traitVars != null)
                CopyVarStore(traitVars, _slotVars);

            // 追加 Payload をマージ
            payload?.ApplyTo(_slotVars, overwrite: true);

            OnEquipped?.Invoke(this);
            await ExecuteCommandsAsync(_onEquipCommands, _runOnEquipCommands, ct);
        }

        /// <summary>
        /// 現在の装備を非同期で解除する。
        /// </summary>
        public async UniTask UnequipAsync(CancellationToken ct)
        {
            if (_equipped == null) return;

            var prev = _equipped;
            _equipped = null;

            await ExecuteCommandsAsync(_onUnequipCommands, _runOnUnequipCommands, ct);

            _slotVars = new VarStore();
            OnUnequipped?.Invoke(this);
        }

        /// <summary>
        /// 現在の装備を即時解除する（コマンドは fire-and-forget）。
        /// TraitHolder からの自動アンケーションで使用。
        /// </summary>
        public void UnequipImmediate()
        {
            if (_equipped == null) return;

            _equipped = null;
            ExecuteCommandsFireAndForget(_onUnequipCommands, _runOnUnequipCommands);
            _slotVars = new VarStore();
            OnUnequipped?.Invoke(this);
        }

        /// <summary>スロットをリセットする（イベント通知なし）。</summary>
        internal void Reset()
        {
            _equipped = null;
            _slotVars = new VarStore();
        }

        // ───────────────── Command Execution ─────────────────

        async UniTask ExecuteCommandsAsync(VNext.CommandListData? commands, bool shouldRun, CancellationToken ct)
        {
            if (!shouldRun || commands == null || commands.Count == 0)
                return;
            if (_scope == null) return;

            var resolver = _scope.Resolver;
            if (resolver == null) return;

            resolver.TryResolve(out VNext.ICommandRunner? runner);
            if (runner == null) return;

            var ctx = new VNext.CommandContext(_scope, _slotVars, runner);
            try
            {
                await runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void ExecuteCommandsFireAndForget(VNext.CommandListData? commands, bool shouldRun)
        {
            if (!shouldRun || commands == null || commands.Count == 0)
                return;
            if (_scope == null) return;

            var resolver = _scope.Resolver;
            if (resolver == null) return;

            resolver.TryResolve(out VNext.ICommandRunner? runner);
            if (runner == null) return;

            var vars = _slotVars;
            var ctx = new VNext.CommandContext(_scope, vars, runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        /// <summary>
        /// VarStore の内容を別の VarStore にコピーする。
        /// </summary>
        static void CopyVarStore(VarStore source, VarStore dest)
        {
            foreach (var varId in source.EnumerateVarIds())
            {
                var kind = source.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (source.TryGetManagedRef(varId, out var managed))
                        dest.TrySetManagedRef(varId, managed);
                }
                else if (kind != ValueKind.Null)
                {
                    if (source.TryGetVariant(varId, out var variant))
                        dest.TrySetVariant(varId, variant);
                }
            }
        }
    }
}

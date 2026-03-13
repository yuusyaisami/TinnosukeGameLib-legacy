#nullable enable

using System;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.Common;
using VContainer;

namespace Game.Flow
{
    /// <summary>
    /// Flow 実行コンテキストを表します。
    /// <para>プログラムデータ、共有変数、現在のスコープ、ローカル変数配列など、実行時に必要な情報を保持します。</para>
    /// </summary>
    public sealed class FlowContext : IDynamicContext
    {
        /// <summary>実行対象のプログラムデータ（読み取り専用）</summary>
        public IFlowProgramData Program { get; }

        /// <summary>共有変数ストア（共有スコープ）</summary>
        public IVarStore Vars { get; }

        /// <summary>実行時のスコープノード（Resolver 等へアクセスするために使用）</summary>
        public IScopeNode Scope { get; }
        public IScopeNode? CommandRootScope => null;

        /// <summary>現在アクティブなローカル変数配列</summary>
        public DynamicVariant[] CurrentLocals { get; private set; }

        public FlowContext(IFlowProgramData program, IScopeNode scope, IVarStore sharedVars, DynamicVariant[] locals)
        {
            Program = program;
            Scope = scope;
            Vars = sharedVars ?? NullVarStore.Instance;
            CurrentLocals = locals ?? Array.Empty<DynamicVariant>();
        }

        /// <summary>ローカル配列を差し替えます（関数呼び出し時に呼ばれます）。</summary>
        public void SetLocals(DynamicVariant[] locals) => CurrentLocals = locals ?? Array.Empty<DynamicVariant>();

        /// <summary>
        /// 指定のターゲットフィルタに一致するスコープ（他スコープ）を解決して返します。
        /// <para>Registry ベースの解決が失敗した場合は現在のスコープを返します。</para>
        /// </summary>
        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            var origin = Scope;
            if (origin == null)
                return origin!;

            var resolver = origin.Resolver;
            if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) && registry != null)
                return registry.Resolve(filter, origin);

            // Fallback: return the current scope.
            return origin;
        }

        public bool TryGetStringFromStringTable(int stringId, out string value)
        {
            value = string.Empty;
            var table = Program.StringTable;
            if (table == null || (uint)stringId >= (uint)table.Length)
                return false;
            value = table[stringId] ?? string.Empty;
            return true;
        }

        public bool TryResolveArg(in FlowArg arg, out DynamicVariant value, out string error)
        {
            error = string.Empty;
            value = DynamicVariant.Null;

            switch (arg.Kind)
            {
                case FlowArgKind.None:
                    value = DynamicVariant.Null;
                    return true;

                case FlowArgKind.ConstInt:
                    value = DynamicVariant.FromInt(arg.Int0);
                    return true;

                case FlowArgKind.ConstFloat:
                    value = DynamicVariant.FromFloat(arg.Float0);
                    return true;

                case FlowArgKind.ConstBool:
                    value = DynamicVariant.FromBool(arg.Int0 != 0);
                    return true;

                case FlowArgKind.ConstString:
                    if (!TryGetStringFromStringTable(arg.Int0, out var s))
                    {
                        error = $"ConstString stringId out of range: {arg.Int0}";
                        return false;
                    }
                    value = DynamicVariant.FromString(s);
                    return true;

                case FlowArgKind.ConstVector2:
                    value = DynamicVariant.FromVector2(new UnityEngine.Vector2(arg.Vec4.x, arg.Vec4.y));
                    return true;

                case FlowArgKind.ConstVector3:
                    value = DynamicVariant.FromVector3(new UnityEngine.Vector3(arg.Vec4.x, arg.Vec4.y, arg.Vec4.z));
                    return true;

                case FlowArgKind.ConstVector4:
                    value = DynamicVariant.FromVector4(arg.Vec4);
                    return true;

                case FlowArgKind.ConstColor:
                    value = DynamicVariant.FromColor(new UnityEngine.Color(arg.Vec4.x, arg.Vec4.y, arg.Vec4.z, arg.Vec4.w));
                    return true;

                case FlowArgKind.VarLocal:
                    {
                        var idx = arg.Int0;
                        if ((uint)idx >= (uint)CurrentLocals.Length)
                        {
                            error = $"VarLocal index out of range: {idx} (locals={CurrentLocals.Length})";
                            return false;
                        }
                        value = CurrentLocals[idx];
                        return true;
                    }

                case FlowArgKind.VarShared:
                    {
                        var varId = arg.Int0;
                        if (varId == 0)
                        {
                            error = "VarShared varId is 0";
                            return false;
                        }

                        if (Vars.TryGetVariant(varId, out var v))
                        {
                            value = v;
                            return true;
                        }

                        value = DynamicVariant.Null;
                        return true;
                    }

                case FlowArgKind.Dynamic:
                    {
                        var src = arg.DynamicSource;
                        if (src == null)
                        {
                            error = "DynamicSource is null";
                            return false;
                        }

                        try
                        {
                            value = src.Evaluate(this);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            error = $"DynamicSource.Evaluate threw: {ex.GetType().Name}: {ex.Message}";
                            value = DynamicVariant.Null;
                            return false;
                        }
                    }

                case FlowArgKind.UnityObject:
                    value = DynamicVariant.FromUnityObject(arg.Obj0);
                    return true;

                case FlowArgKind.CommandSource:
                    // ICommandSource is not representable as DynamicVariant.
                    error = "CommandSource cannot be resolved as DynamicVariant. Use TryResolveObject instead.";
                    value = DynamicVariant.Null;
                    return false;

                default:
                    error = $"Unknown FlowArgKind: {arg.Kind}";
                    return false;
            }
        }

        public bool TryResolveArgByIndex(int argIndex, out DynamicVariant value, out string error)
        {
            var args = Program.Args;
            if (args == null || (uint)argIndex >= (uint)args.Length)
            {
                value = DynamicVariant.Null;
                error = $"Arg index out of range: {argIndex} (args={args?.Length ?? 0})";
                return false;
            }

            return TryResolveArg(args[argIndex], out value, out error);
        }

        public bool TryResolveObjectByIndex(int argIndex, out object value)
        {
            value = null!;

            var args = Program.Args;
            if (args == null || (uint)argIndex >= (uint)args.Length)
                return false;

            return TryResolveObject(args[argIndex], out value);
        }

        public bool TryResolveObject(in FlowArg arg, out object value)
        {
            value = null!;

            switch (arg.Kind)
            {
                case FlowArgKind.CommandSource:
                    value = arg.CommandSource!;
                    return arg.CommandSource != null;

                case FlowArgKind.UnityObject:
                    value = arg.Obj0!;
                    return arg.Obj0 != null;

                case FlowArgKind.VarShared:
                    {
                        var varId = arg.Int0;
                        if (varId == 0)
                            return false;

                        if (Vars.TryGetManagedRef(varId, out var obj))
                        {
                            value = obj;
                            return obj != null;
                        }

                        if (Vars.TryGetVariant(varId, out var v) && v.TryGet<UnityEngine.Object>(out var uo) && uo != null)
                        {
                            value = uo;
                            return true;
                        }

                        return false;
                    }

                case FlowArgKind.Dynamic:
                    {
                        if (!TryResolveArg(arg, out var v, out _))
                            return false;

                        if (v.TryGet<UnityEngine.Object>(out var uo) && uo != null)
                        {
                            value = uo;
                            return true;
                        }
                        return false;
                    }

                default:
                    return false;
            }
        }
    }
}

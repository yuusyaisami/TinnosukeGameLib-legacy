#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;

namespace Game.Flow
{
    /// <summary>
    /// Flow バイトコードの実行エンジン（ランタイム）。
    /// <para>静的メソッド <see cref="RunAsync"/> を使用してプログラムを実行します。</para>
    /// </summary>
    public static class FlowRunner
    {
        /// <summary>関数呼び出し時に使用するフレーム情報</summary>
        sealed class CallFrame
        {
            public int ReturnIp;
            public int FunctionIndex;
            public DynamicVariant[] Locals = Array.Empty<DynamicVariant>();
        }

        /// <summary>
        /// プログラムを非同期実行します。
        /// </summary>
        /// <param name="program">実行するプログラムデータ</param>
        /// <param name="scope">実行時スコープノード</param>
        /// <param name="sharedVars">共有変数ストア</param>
        /// <param name="host">ホスト側のシステムコール実装</param>
        /// <param name="entryFunctionName">開始関数名</param>
        /// <param name="options">実行オプション</param>
        /// <param name="ct">キャンセルトークン</param>
        /// <returns>実行結果（FlowRunResult）</returns>
        public static async UniTask<FlowRunResult> RunAsync(
            IFlowProgramData program,
            IScopeNode scope,
            IVarStore sharedVars,
            IFlowHost host,
            string entryFunctionName,
            FlowRunOptions? options,
            CancellationToken ct)
        {
            if (program == null)
                return FlowRunResult.Error(lastIp: -1, errorIp: -1, message: "Program is null");
            if (scope == null)
                return FlowRunResult.Error(lastIp: -1, errorIp: -1, message: "Scope is null");
            if (host == null)
                return FlowRunResult.Error(lastIp: -1, errorIp: -1, message: "Host is null");

            var opt = options ?? new FlowRunOptions();
            var telemetry = opt.Telemetry;

            var functions = program.Functions;
            var stringTable = program.StringTable;
            var code = program.Code;
            var args = program.Args;

            if (functions == null || functions.Length == 0)
                return FlowRunResult.Error(-1, -1, "Program has no functions");
            if (stringTable == null)
                return FlowRunResult.Error(-1, -1, "Program StringTable is null");
            if (code == null)
                return FlowRunResult.Error(-1, -1, "Program Code is null");
            if (args == null)
                return FlowRunResult.Error(-1, -1, "Program Args is null");

            var entryFunctionIndex = FindFunctionIndex(functions, stringTable, entryFunctionName);
            if (entryFunctionIndex < 0)
                return FlowRunResult.Error(-1, -1, $"Entry function not found: '{entryFunctionName}'");

            var entryIp = functions[entryFunctionIndex].EntryIp;
            if ((uint)entryIp >= (uint)code.Length)
                return FlowRunResult.Error(-1, -1, $"EntryIp out of range: {entryIp} (code={code.Length})");

            var entryLocalCount = functions[entryFunctionIndex].LocalCount;
            if (entryLocalCount < 0)
                return FlowRunResult.Error(-1, -1, $"Entry LocalCount invalid: {entryLocalCount}");

            var callStack = new CallFrame[opt.MaxCallDepth <= 0 ? 1 : opt.MaxCallDepth];
            int callDepth = 0;

            var entryLocals = entryLocalCount == 0 ? Array.Empty<DynamicVariant>() : new DynamicVariant[entryLocalCount];
            var ctx = new FlowContext(program, scope, sharedVars ?? NullVarStore.Instance, entryLocals);

            TryTelemetryStart(telemetry, program, entryFunctionName);

            int ip = entryIp;
            int lastIp = ip;
            int instructionsThisFrame = 0;

            try
            {
                while (true)
                {
                    if (ct.IsCancellationRequested)
                    {
                        var canceled = FlowRunResult.Canceled(lastIp);
                        TryTelemetryEnd(telemetry, canceled);
                        return canceled;
                    }

                    if ((uint)ip >= (uint)code.Length)
                    {
                        var err = FlowRunResult.Error(lastIp, ip, $"IP out of range: {ip} (code={code.Length})");
                        TryTelemetryEnd(telemetry, err);
                        return err;
                    }

                    lastIp = ip;
                    var instr = code[ip];
                    TryTelemetryInstruction(telemetry, ip, in instr);

                    switch (instr.Op)
                    {
                        case FlowOpCode.Nop:
                            ip++;
                            break;

                        case FlowOpCode.Jump:
                            ip = instr.A;
                            break;

                        case FlowOpCode.BranchFalse:
                            {
                                var condArgIndex = instr.A;
                                var targetIp = instr.B;
                                if (!ctx.TryResolveArgByIndex(condArgIndex, out var cond, out var errMsg))
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"BranchFalse resolve failed: {errMsg}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                bool b;
                                if (cond.TryGet<bool>(out var bb))
                                    b = bb;
                                else
                                    b = false;

                                ip = b ? ip + 1 : targetIp;
                                break;
                            }

                        case FlowOpCode.Call:
                            {
                                var functionIndex = instr.A;
                                if ((uint)functionIndex >= (uint)functions.Length)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"Call functionIndex out of range: {functionIndex} (functions={functions.Length})");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                if (callDepth >= callStack.Length)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"MaxCallDepth exceeded: {callStack.Length}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                var nextIp = functions[functionIndex].EntryIp;
                                if ((uint)nextIp >= (uint)code.Length)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"Call entryIp out of range: {nextIp} (code={code.Length})");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                var frame = callStack[callDepth] ??= new CallFrame();
                                frame.ReturnIp = ip + 1;
                                frame.FunctionIndex = functionIndex;

                                var localCount = functions[functionIndex].LocalCount;
                                if (localCount < 0)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"Call LocalCount invalid: {localCount}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                if (localCount == 0)
                                {
                                    frame.Locals = Array.Empty<DynamicVariant>();
                                }
                                else
                                {
                                    var existing = frame.Locals;
                                    if (existing == null || existing.Length != localCount)
                                        frame.Locals = new DynamicVariant[localCount];
                                    else
                                        System.Array.Clear(existing, 0, existing.Length);
                                }

                                callDepth++;
                                ctx.SetLocals(frame.Locals);

                                ip = nextIp;
                                break;
                            }

                        case FlowOpCode.Return:
                            {
                                if (callDepth <= 0)
                                {
                                    var done = FlowRunResult.Completed(lastIp);
                                    TryTelemetryEnd(telemetry, done);
                                    return done;
                                }

                                callDepth--;
                                var frame = callStack[callDepth];
                                if (frame == null)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, "CallFrame missing");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                ip = frame.ReturnIp;

                                // Restore locals of previous frame (or empty if returning to top level)
                                if (callDepth > 0)
                                {
                                    var prev = callStack[callDepth - 1];
                                    ctx.SetLocals(prev?.Locals ?? Array.Empty<DynamicVariant>());
                                }
                                else
                                {
                                    ctx.SetLocals(Array.Empty<DynamicVariant>());
                                }

                                break;
                            }

                        case FlowOpCode.SetVar:
                            {
                                var id = instr.A;
                                var targetScope = instr.B;
                                var valueArgIndex = instr.C;

                                if (!ctx.TryResolveArgByIndex(valueArgIndex, out var value, out var errMsg))
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"SetVar resolve failed: {errMsg}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                if (targetScope == 0)
                                {
                                    // Local
                                    if (id < 0)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"SetVar(Local) invalid slot: {id}");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }

                                    var locals = ctx.CurrentLocals;
                                    if ((uint)id >= (uint)locals.Length)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"SetVar(Local) slot out of range: {id} (locals={locals.Length})");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }

                                    locals[id] = value;
                                    ip++;
                                    break;
                                }

                                if (targetScope == 1)
                                {
                                    // Shared
                                    if (id == 0)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, "SetVar(Shared) varId is 0");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }

                                    bool ok;
                                    if (value.Kind == ValueKind.Null)
                                        ok = ctx.Vars.TryUnset(id);
                                    else
                                        ok = ctx.Vars.TrySetVariant(id, value);

                                    if (!ok)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"SetVar(Shared) failed varId={id} kind={value.Kind}");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }

                                    ip++;
                                    break;
                                }

                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"SetVar unknown targetScope: {targetScope}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }
                            }

                        case FlowOpCode.HostCall:
                            {
                                var sysId = instr.A;
                                var argStart = instr.B;
                                var argCount = instr.C;
                                var resultVarId = instr.D;

                                TryTelemetrySyscall(telemetry, sysId, argStart, argCount);

                                if (sysId == 0)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, "HostCall sysId is 0");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                if (argStart < 0 || argCount < 0 || argStart + argCount > args.Length)
                                {
                                    var err = FlowRunResult.Error(lastIp, ip, $"HostCall args range invalid: start={argStart} count={argCount} args={args.Length}");
                                    TryTelemetryEnd(telemetry, err);
                                    return err;
                                }

                                FlowSyscallResult result;
                                try
                                {
                                    result = await host.InvokeAsync(ctx, new FlowSyscallRequest(sysId, argStart, argCount, resultVarId), ct);
                                }
                                catch (Exception ex)
                                {
                                    result = FlowSyscallResult.Failure;
                                    if (!opt.ContinueOnHostCallFailure)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"HostCall threw: {ex.GetType().Name}: {ex.Message}");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }
                                }

                                if (!result.HasValue)
                                {
                                    if (!opt.ContinueOnHostCallFailure)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"HostCall failed sysId={sysId}");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }

                                    ip++;
                                    break;
                                }

                                if (resultVarId > 0)
                                {
                                    bool ok;
                                    if (result.Value.Kind == ValueKind.Null)
                                        ok = ctx.Vars.TryUnset(resultVarId);
                                    else
                                        ok = ctx.Vars.TrySetVariant(resultVarId, result.Value);

                                    if (!ok)
                                    {
                                        var err = FlowRunResult.Error(lastIp, ip, $"HostCall result write failed varId={resultVarId} kind={result.Value.Kind}");
                                        TryTelemetryEnd(telemetry, err);
                                        return err;
                                    }
                                }
                                ip++;
                                break;
                            }

                        default:
                            {
                                var err = FlowRunResult.Error(lastIp, ip, $"Unknown opcode: {instr.Op}");
                                TryTelemetryEnd(telemetry, err);
                                return err;
                            }
                    }

                    // Avoid main-thread stalls by yielding after a budget of instructions.
                    if (opt.MaxInstructionsPerFrame > 0)
                    {
                        instructionsThisFrame++;
                        if (instructionsThisFrame >= opt.MaxInstructionsPerFrame)
                        {
                            instructionsThisFrame = 0;
                            await UniTask.Yield(PlayerLoopTiming.Update);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var canceled = FlowRunResult.Canceled(lastIp);
                TryTelemetryEnd(telemetry, canceled);
                return canceled;
            }
            catch (Exception ex)
            {
                var err = FlowRunResult.Error(lastIp, lastIp, $"Runner exception: {ex.GetType().Name}: {ex.Message}");
                TryTelemetryEnd(telemetry, err);
                return err;
            }
        }

        static int FindFunctionIndex(FlowFunctionInfo[] functions, string[] stringTable, string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            for (int i = 0; i < functions.Length; i++)
            {
                var id = functions[i].NameStringId;
                if ((uint)id >= (uint)stringTable.Length)
                    continue;
                if (string.Equals(stringTable[id], name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static void TryTelemetryStart(IFlowTelemetry? telemetry, IFlowProgramData program, string entry)
        {
            if (telemetry == null)
                return;
            try { telemetry.OnStart(program, entry); } catch { }
        }

        static void TryTelemetryInstruction(IFlowTelemetry? telemetry, int ip, in FlowInstruction instr)
        {
            if (telemetry == null)
                return;
            try { telemetry.OnInstruction(ip, in instr); } catch { }
        }

        static void TryTelemetrySyscall(IFlowTelemetry? telemetry, int sysId, int argStart, int argCount)
        {
            if (telemetry == null)
                return;
            try { telemetry.OnSyscall(sysId, argStart, argCount); } catch { }
        }

        static void TryTelemetryEnd(IFlowTelemetry? telemetry, in FlowRunResult result)
        {
            if (telemetry == null)
                return;
            try { telemetry.OnEnd(in result); } catch { }
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Game.Common;
using Game.Commands.VNext;
using Game.VarStoreKeys;
using UnityEngine;

namespace Game.Flow
{
    public static class FlowCompiler
    {
        sealed class StringTableBuilder
        {
            readonly Dictionary<string, int> _toId = new(StringComparer.Ordinal);
            readonly List<string> _items = new();

            public int GetOrAdd(string s)
            {
                s ??= string.Empty;
                if (_toId.TryGetValue(s, out var id))
                    return id;

                id = _items.Count;
                _items.Add(s);
                _toId.Add(s, id);
                return id;
            }

            public string[] ToArray() => _items.ToArray();
        }

        sealed class FunctionCompileContext
        {
            readonly Dictionary<string, int> _localNameToSlot = new(StringComparer.Ordinal);

            public int LocalCount => _localNameToSlot.Count;

            public int GetOrCreateLocalSlot(string localName)
            {
                if (string.IsNullOrWhiteSpace(localName))
                    return -1;

                if (_localNameToSlot.TryGetValue(localName, out var slot))
                    return slot;

                slot = _localNameToSlot.Count;
                _localNameToSlot.Add(localName, slot);
                return slot;
            }
        }

        sealed class CompileContext
        {
            public readonly List<FlowInstruction> Code = new(256);
            public readonly List<FlowArg> Args = new(256);
            public readonly StringTableBuilder StringTable = new();
            public readonly Dictionary<string, int> FunctionNameToIndex = new(StringComparer.Ordinal);
            public readonly List<FlowFunctionInfo> Functions = new(64);
            public readonly FlowCompileReport Report = new();

            public VarKeyRegistry? Registry;

            public int AddArg(in FlowArg arg)
            {
                Args.Add(arg);
                return Args.Count - 1;
            }

            public int Emit(in FlowInstruction instr)
            {
                Code.Add(instr);
                return Code.Count - 1;
            }
        }

        public static bool TryCompile(FlowDefinitionSO definition, FlowProgramAssetSO destination, out FlowCompileReport report)
        {
            var ctx = new CompileContext();

            if (definition == null)
            {
                ctx.Report.Error("FlowDefinitionSO is null");
                report = ctx.Report;
                return false;
            }

            if (destination == null)
            {
                ctx.Report.Error("Destination FlowProgramAssetSO is null");
                report = ctx.Report;
                return false;
            }

            definition.EnsureIntegrity(definition);

            var funcs = definition.Functions ?? Array.Empty<FlowFunctionSO>();
            if (funcs.Length == 0)
                ctx.Report.Error("Definition has no functions");

            ctx.Registry = VarKeyRegistryLocator.GetOrCreate();
            if (ctx.Registry == null)
                ctx.Report.Error("VarKeyRegistry is null (VarKeyRegistryLocator.GetOrCreate returned null)");

            // Function table + name validation
            var functionList = new List<FlowFunctionSO>(funcs.Length);
            for (int i = 0; i < funcs.Length; i++)
            {
                var f = funcs[i];
                if (f == null)
                    continue;
                f.EnsureIntegrity(f);

                var name = f.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    ctx.Report.Error($"Function at index {i} has empty name");
                    continue;
                }

                if (ctx.FunctionNameToIndex.ContainsKey(name))
                {
                    ctx.Report.Error($"Duplicate function name: '{name}'");
                    continue;
                }

                ctx.FunctionNameToIndex.Add(name, functionList.Count);
                functionList.Add(f);
            }

            // Entry check
            var entryName = definition.EntryFunctionName;
            if (string.IsNullOrWhiteSpace(entryName))
                ctx.Report.Error("EntryFunctionName is empty");
            else if (!ctx.FunctionNameToIndex.ContainsKey(entryName))
                ctx.Report.Error($"Entry function not found: '{entryName}'");

            if (ctx.Report.HasErrors)
            {
                report = ctx.Report;
                ApplyToDestination(destination, definition, ctx, version: destination.Version + 1);
                return false;
            }

            // Pre-add all function names to string table (stable ids)
            var functionNameStringIds = new int[functionList.Count];
            for (int i = 0; i < functionList.Count; i++)
                functionNameStringIds[i] = ctx.StringTable.GetOrAdd(functionList[i].Name);

            // Compile each function
            ctx.Functions.Clear();
            var entryIps = new int[functionList.Count];
            var localCounts = new int[functionList.Count];

            for (int i = 0; i < functionList.Count; i++)
            {
                var f = functionList[i];
                entryIps[i] = ctx.Code.Count;

                var fctx = new FunctionCompileContext();
                CompileStatements(ctx, fctx, f.Statements);
                ctx.Emit(new FlowInstruction(FlowOpCode.Return, 0, 0, 0, 0));

                localCounts[i] = fctx.LocalCount;
            }

            // Build function infos
            for (int i = 0; i < functionList.Count; i++)
            {
                ctx.Functions.Add(new FlowFunctionInfo(
                    nameStringId: functionNameStringIds[i],
                    entryIp: entryIps[i],
                    localCount: localCounts[i],
                    maxStackHint: 0));
            }

            report = ctx.Report;

            var newVersion = destination.Version + 1;
            ApplyToDestination(destination, definition, ctx, version: newVersion);
            return !ctx.Report.HasErrors;
        }

        static void CompileStatements(CompileContext ctx, FunctionCompileContext fctx, FlowStatement[]? statements)
        {
            if (statements == null || statements.Length == 0)
                return;

            for (int i = 0; i < statements.Length; i++)
            {
                var s = statements[i];
                if (s == null)
                    continue;

                switch (s)
                {
                    case FlowIfStmt ifs:
                        CompileIf(ctx, fctx, ifs);
                        break;
                    case FlowWhileStmt wh:
                        CompileWhile(ctx, fctx, wh);
                        break;
                    case FlowSetVarStmt set:
                        CompileSetVar(ctx, fctx, set);
                        break;
                    case FlowCallStmt call:
                        CompileCall(ctx, call);
                        break;
                    case FlowHostCallStmt host:
                        CompileHostCall(ctx, fctx, host);
                        break;
                    case FlowRunCommandStmt runCommand:
                        CompileRunCommand(ctx, fctx, runCommand);
                        break;
                    case FlowWithActorCommandStmt withActorCmd:
                        CompileWithActorCommand(ctx, fctx, withActorCmd);
                        break;
                    case FlowReturnStmt:
                        ctx.Emit(new FlowInstruction(FlowOpCode.Return, 0, 0, 0, 0));
                        break;
                    default:
                        ctx.Report.Error($"Unknown statement type: {s.GetType().Name}");
                        break;
                }

                if (ctx.Report.HasErrors)
                    return;
            }
        }

        static void CompileIf(CompileContext ctx, FunctionCompileContext fctx, FlowIfStmt stmt)
        {
            if (!TryCompileArg(ctx, fctx, stmt.Condition, out var condArgIndex))
                return;

            // BranchFalse cond -> else
            var branchIp = ctx.Emit(new FlowInstruction(FlowOpCode.BranchFalse, condArgIndex, b: 0, c: 0, d: 0));

            CompileStatements(ctx, fctx, stmt.Then);
            if (ctx.Report.HasErrors)
                return;

            var hasElse = stmt.Else != null && stmt.Else.Length > 0;
            int jumpIp = -1;
            if (hasElse)
                jumpIp = ctx.Emit(new FlowInstruction(FlowOpCode.Jump, a: 0, b: 0, c: 0, d: 0));

            var elseIp = ctx.Code.Count;

            // Patch BranchFalse target
            var patchedBranch = ctx.Code[branchIp];
            patchedBranch.B = elseIp;
            ctx.Code[branchIp] = patchedBranch;

            if (hasElse)
            {
                CompileStatements(ctx, fctx, stmt.Else);
                if (ctx.Report.HasErrors)
                    return;

                var endIp = ctx.Code.Count;
                var patchedJump = ctx.Code[jumpIp];
                patchedJump.A = endIp;
                ctx.Code[jumpIp] = patchedJump;
            }
        }

        static void CompileWhile(CompileContext ctx, FunctionCompileContext fctx, FlowWhileStmt stmt)
        {
            var loopStartIp = ctx.Code.Count;

            if (!TryCompileArg(ctx, fctx, stmt.Condition, out var condArgIndex))
                return;

            var branchIp = ctx.Emit(new FlowInstruction(FlowOpCode.BranchFalse, condArgIndex, b: 0, c: 0, d: 0));

            CompileStatements(ctx, fctx, stmt.Body);
            if (ctx.Report.HasErrors)
                return;

            ctx.Emit(new FlowInstruction(FlowOpCode.Jump, loopStartIp, 0, 0, 0));

            var loopEndIp = ctx.Code.Count;
            var patchedBranch = ctx.Code[branchIp];
            patchedBranch.B = loopEndIp;
            ctx.Code[branchIp] = patchedBranch;
        }

        static void CompileSetVar(CompileContext ctx, FunctionCompileContext fctx, FlowSetVarStmt stmt)
        {
            if (!TryCompileArg(ctx, fctx, stmt.Value, out var valueArgIndex))
                return;

            if (stmt.TargetScope == FlowTargetScope.Local)
            {
                if (string.IsNullOrWhiteSpace(stmt.LocalName))
                {
                    ctx.Report.Error("SetVar(Local) LocalName is empty");
                    return;
                }

                var slot = fctx.GetOrCreateLocalSlot(stmt.LocalName);
                if (slot < 0)
                {
                    ctx.Report.Error($"SetVar(Local) could not allocate local slot: '{stmt.LocalName}'");
                    return;
                }

                ctx.Emit(new FlowInstruction(FlowOpCode.SetVar, a: slot, b: 0, c: valueArgIndex, d: 0));
                return;
            }

            // Shared
            if (string.IsNullOrWhiteSpace(stmt.StableKey))
            {
                ctx.Report.Error("SetVar(Shared) StableKey is empty");
                return;
            }

            if (ctx.Registry == null || !ctx.Registry.TryResolve(stmt.StableKey, out var varId) || varId == 0)
            {
                ctx.Report.Error($"SetVar(Shared) StableKey not registered: '{stmt.StableKey}'");
                return;
            }

            ctx.Emit(new FlowInstruction(FlowOpCode.SetVar, a: varId, b: 1, c: valueArgIndex, d: 0));
        }

        static void CompileCall(CompileContext ctx, FlowCallStmt stmt)
        {
            if (string.IsNullOrWhiteSpace(stmt.FunctionName))
            {
                ctx.Report.Error("Call FunctionName is empty");
                return;
            }

            if (!ctx.FunctionNameToIndex.TryGetValue(stmt.FunctionName, out var idx))
            {
                ctx.Report.Error($"Call target function not found: '{stmt.FunctionName}'");
                return;
            }

            ctx.Emit(new FlowInstruction(FlowOpCode.Call, a: idx, b: 0, c: 0, d: 0));
        }

        static void CompileHostCall(CompileContext ctx, FunctionCompileContext fctx, FlowHostCallStmt stmt)
        {
            if (stmt.SysId == 0)
            {
                ctx.Report.Error("HostCall SysId is 0");
                return;
            }

            var args = stmt.Args ?? Array.Empty<FlowArgDef>();
            var argStart = ctx.Args.Count;

            for (int i = 0; i < args.Length; i++)
            {
                if (!TryCompileArgToRuntime(ctx, fctx, args[i], out var runtimeArg))
                    return;
                ctx.Args.Add(runtimeArg);
            }

            var resultVarId = 0;
            if (!string.IsNullOrWhiteSpace(stmt.ResultStableKey))
            {
                if (ctx.Registry == null || !ctx.Registry.TryResolve(stmt.ResultStableKey, out resultVarId) || resultVarId == 0)
                {
                    ctx.Report.Error($"HostCall ResultStableKey not registered: '{stmt.ResultStableKey}'");
                    return;
                }
            }

            ctx.Emit(new FlowInstruction(
                FlowOpCode.HostCall,
                a: stmt.SysId,
                b: argStart,
                c: args.Length,
                d: resultVarId));
        }

        static void CompileRunCommand(CompileContext ctx, FunctionCompileContext fctx, FlowRunCommandStmt stmt)
        {
            if (stmt.Command == null)
            {
                ctx.Report.Error("RunCommand Command is null");
                return;
            }

            var argStart = ctx.Args.Count;

            ctx.Args.Add(FlowArg.VNextCommandSource(stmt.Command));
            var argCount = 1;

            if (stmt.UseActorOverride)
            {
                if (!TryCompileArgToRuntime(ctx, fctx, stmt.Actor, out var actorArg))
                    return;
                ctx.Args.Add(actorArg);
                argCount++;
            }

            ctx.Emit(new FlowInstruction(
                FlowOpCode.HostCall,
                a: FlowSysIds.RunCommand,
                b: argStart,
                c: argCount,
                d: 0));
        }

        static void CompileWithActorCommand(CompileContext ctx, FunctionCompileContext fctx, FlowWithActorCommandStmt stmt)
        {
            if (stmt.CommandData == null)
            {
                ctx.Report.Error("WithActorCommand CommandData is null");
                return;
            }

            if (stmt.CommandData.Body == null || stmt.CommandData.Body.Count == 0)
            {
                ctx.Report.Error("WithActorCommand Body is empty");
                return;
            }

            var argStart = ctx.Args.Count;

            // WithActorCommandData をInlineCommandSourceでラップして渡す
            var commandSource = new InlineCommandSource(stmt.CommandData);
            ctx.Args.Add(FlowArg.VNextCommandSource(commandSource));

            var argCount = ctx.Args.Count - argStart;

            ctx.Emit(new FlowInstruction(
                FlowOpCode.HostCall,
                a: FlowSysIds.RunCommand,
                b: argStart,
                c: argCount,
                d: 0));
        }

        static bool TryCompileArg(CompileContext ctx, FunctionCompileContext fctx, FlowArgDef def, out int argIndex)
        {
            argIndex = -1;
            if (!TryCompileArgToRuntime(ctx, fctx, def, out var arg))
                return false;

            argIndex = ctx.AddArg(in arg);
            return true;
        }

        static bool TryCompileArgToRuntime(CompileContext ctx, FunctionCompileContext fctx, FlowArgDef def, out FlowArg arg)
        {
            arg = default;
            def.EnsureIntegrity();

            switch (def.Kind)
            {
                case FlowArgKind.None:
                    arg = new FlowArg { Kind = FlowArgKind.None };
                    return true;

                case FlowArgKind.ConstInt:
                    arg = FlowArg.ConstInt(def.IntValue);
                    return true;

                case FlowArgKind.ConstFloat:
                    arg = FlowArg.ConstFloat(def.FloatValue);
                    return true;

                case FlowArgKind.ConstBool:
                    arg = FlowArg.ConstBool(def.IntValue != 0);
                    return true;

                case FlowArgKind.ConstString:
                    {
                        if (def.StringValue == null)
                            def.StringValue = string.Empty;
                        var id = ctx.StringTable.GetOrAdd(def.StringValue);
                        arg = FlowArg.ConstString(id);
                        return true;
                    }

                case FlowArgKind.ConstVector2:
                case FlowArgKind.ConstVector3:
                case FlowArgKind.ConstVector4:
                case FlowArgKind.ConstColor:
                    arg = new FlowArg { Kind = def.Kind, Vec4 = def.Vec4 };
                    return true;

                case FlowArgKind.VarLocal:
                    {
                        if (string.IsNullOrWhiteSpace(def.LocalName))
                        {
                            ctx.Report.Error("VarLocal LocalName is empty");
                            return false;
                        }

                        var slot = fctx.GetOrCreateLocalSlot(def.LocalName);
                        if (slot < 0)
                        {
                            ctx.Report.Error($"VarLocal could not allocate local slot: '{def.LocalName}'");
                            return false;
                        }

                        arg = FlowArg.VarLocal(slot);
                        return true;
                    }

                case FlowArgKind.VarShared:
                    {
                        if (string.IsNullOrWhiteSpace(def.StableKey))
                        {
                            ctx.Report.Error("VarShared StableKey is empty");
                            return false;
                        }

                        if (ctx.Registry == null || !ctx.Registry.TryResolve(def.StableKey, out var varId) || varId == 0)
                        {
                            ctx.Report.Error($"VarShared StableKey not registered: '{def.StableKey}'");
                            return false;
                        }

                        arg = FlowArg.VarShared(varId);
                        return true;
                    }

                case FlowArgKind.Dynamic:
                    if (def.DynamicSource == null)
                    {
                        ctx.Report.Error("DynamicSource is null");
                        return false;
                    }
                    arg = new FlowArg { Kind = FlowArgKind.Dynamic, DynamicSource = def.DynamicSource };
                    return true;

                case FlowArgKind.UnityObject:
                    arg = FlowArg.UnityObject(def.Obj);
                    return true;

                default:
                    ctx.Report.Error($"Unknown FlowArgKind: {def.Kind}");
                    return false;
            }
        }

        static void ApplyToDestination(FlowProgramAssetSO dest, FlowDefinitionSO src, CompileContext ctx, int version)
        {
            var stringTable = ctx.StringTable.ToArray();
            var code = ctx.Code.ToArray();
            var args = ctx.Args.ToArray();
            var functions = ctx.Functions.ToArray();

            var hash = ComputeSourceHash(src);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            dest.SetCompiledData(
                newVersion: version,
                newCode: code,
                newArgs: args,
                newStringTable: stringTable,
                newFunctions: functions,
                newSourceDefinition: src,
                newSourceHash: hash,
                newBuildTimestamp: timestamp,
                newReport: ctx.Report);
        }

        static string ComputeSourceHash(FlowDefinitionSO src)
        {
            if (src == null)
                return string.Empty;

            var sb = new StringBuilder(1024);
            sb.Append("Entry:").Append(src.EntryFunctionName).Append('\n');

            var funcs = src.Functions ?? Array.Empty<FlowFunctionSO>();
            for (int i = 0; i < funcs.Length; i++)
            {
                var f = funcs[i];
                if (f == null) continue;
                sb.Append("Func:").Append(f.Name).Append('\n');

                var st = f.Statements ?? Array.Empty<FlowStatement>();
                for (int s = 0; s < st.Length; s++)
                {
                    var stmt = st[s];
                    if (stmt == null) continue;
                    sb.Append("  ").Append(stmt.GetType().Name).Append('\n');
                }
            }

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hashBytes = sha.ComputeHash(bytes);

            var hex = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
                hex.Append(hashBytes[i].ToString("x2"));
            return hex.ToString();
        }
    }
}

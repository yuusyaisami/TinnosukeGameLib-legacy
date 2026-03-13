#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Scalar;
using UnityEngine;

namespace Game.Save
{
    public sealed class SaveBinderV2 : ISaveBinder
    {
        public void Collect(in SaveContext ctx, SavePlan plan, in SaveScopeRegistration reg, ref SavePayload payload)
        {
            var bb = reg.Blackboard;
            var scalar = reg.Scalars;

            if (bb == null || scalar == null)
            {
                Debug.LogWarning($"[SaveBinderV2] Collect failed: Blackboard or Scalar is null for {ctx.ScopeKey}");
                return;
            }

            var bbList = new List<BlackboardVarPayload>(64);
            var scalarList = new List<ScalarKeyPayload>(32);

            var entries = plan.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.IsValid)
                    continue;

                if (e.Kind == SaveTargetKind.Blackboard)
                {
                    if (e.VarId == 0)
                        continue;

                    if (!bb.TryLocalGetVariant(e.VarId, out var v))
                        continue;

                    if (v.Kind == ValueKind.ManagedRef || v.Kind == ValueKind.UnityObject)
                        continue;

                    var p = ToPayload(e.VarId, v);
                    bbList.Add(p);
                }
                else
                {
                    if (e.ScalarKeyId == 0)
                        continue;

                    var key = new ScalarKey { Id = e.ScalarKeyId, Name = string.Empty };
                    if (!scalar.TryGetRuntime(key, out var rt) || rt == null)
                        continue;

                    var p = ExportScalar(rt);
                    scalarList.Add(p);
                }
            }

            payload.Blackboard = bbList.Count == 0 ? Array.Empty<BlackboardVarPayload>() : bbList.ToArray();
            payload.Scalars = scalarList.Count == 0 ? Array.Empty<ScalarKeyPayload>() : scalarList.ToArray();
        }

        public void Apply(in SaveContext ctx, SavePlan plan, in SaveScopeRegistration reg, in SavePayload payload, ISaveLayerPolicy layerPolicy)
        {
            var bb = reg.Blackboard;
            var scalar = reg.Scalars;
            if (bb == null || scalar == null)
            {
                Debug.LogWarning($"[SaveBinderV2] Apply failed: Blackboard or Scalar is null for {ctx.ScopeKey}");
                return;
            }

            var bbMissing = layerPolicy.GetBlackboardMissingPolicy(ctx.Layer);
            var scalarMissing = layerPolicy.GetScalarMissingPolicy(ctx.Layer);

            var bbMap = new Dictionary<int, BlackboardVarPayload>(payload.Blackboard?.Length ?? 0);
            if (payload.Blackboard != null)
            {
                for (int i = 0; i < payload.Blackboard.Length; i++)
                {
                    var p = payload.Blackboard[i];
                    if (p.VarId != 0)
                        bbMap[p.VarId] = p;
                }
            }

            var scalarMap = new Dictionary<int, ScalarKeyPayload>(payload.Scalars?.Length ?? 0);
            if (payload.Scalars != null)
            {
                for (int i = 0; i < payload.Scalars.Length; i++)
                {
                    var p = payload.Scalars[i];
                    if (p.KeyId != 0)
                        scalarMap[p.KeyId] = p;
                }
            }

            var entries = plan.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.IsValid)
                    continue;

                if (e.Kind == SaveTargetKind.Blackboard)
                {
                    if (e.VarId == 0)
                        continue;

                    if (bbMap.TryGetValue(e.VarId, out var p))
                    {
                        var v = FromPayload(p);
                        bb.TryLocalSetVariant(e.VarId, in v);
                    }
                    else
                    {
                        if (bbMissing == MissingPolicy.Clear)
                        {
                            var v = DynamicVariant.Null;
                            bb.TryLocalSetVariant(e.VarId, in v);
                        }
                    }
                }
                else
                {
                    if (e.ScalarKeyId == 0)
                        continue;

                    var key = new ScalarKey { Id = e.ScalarKeyId, Name = string.Empty };

                    if (scalarMap.TryGetValue(e.ScalarKeyId, out var s))
                    {
                        ImportScalar(scalar, key, s);
                    }
                    else
                    {
                        if (scalarMissing == MissingPolicy.Clear)
                        {
                            scalar.ClearAll(key);
                        }
                    }
                }
            }
        }

        static BlackboardVarPayload ToPayload(int varId, in DynamicVariant v)
        {
            var p = new BlackboardVarPayload
            {
                VarId = varId,
                Kind = (byte)v.Kind,
                Numeric = 0,
                Str = string.Empty,
                X = 0,
                Y = 0,
                Z = 0,
                W = 0,
            };

            switch (v.Kind)
            {
                case ValueKind.Bool:
                    p.Numeric = v.AsBool ? 1 : 0;
                    break;
                case ValueKind.Int:
                    p.Numeric = v.AsInt;
                    break;
                case ValueKind.Float:
                    p.Numeric = v.AsFloat;
                    break;
                case ValueKind.String:
                    p.Str = v.AsString;
                    break;
                case ValueKind.Vector2:
                    {
                        var vv = v.AsVector2;
                        p.X = vv.x;
                        p.Y = vv.y;
                        break;
                    }
                case ValueKind.Vector3:
                    {
                        var vv = v.AsVector3;
                        p.X = vv.x;
                        p.Y = vv.y;
                        p.Z = vv.z;
                        break;
                    }
                case ValueKind.Vector4:
                    {
                        var vv = v.AsVector4;
                        p.X = vv.x;
                        p.Y = vv.y;
                        p.Z = vv.z;
                        p.W = vv.w;
                        break;
                    }
                case ValueKind.Color:
                    {
                        var c = v.AsColor;
                        p.X = c.r;
                        p.Y = c.g;
                        p.Z = c.b;
                        p.W = c.a;
                        break;
                    }
            }

            return p;
        }

        static DynamicVariant FromPayload(in BlackboardVarPayload p)
        {
            var kind = (ValueKind)p.Kind;
            switch (kind)
            {
                case ValueKind.Bool:
                    return DynamicVariant.FromBool(p.Numeric != 0);
                case ValueKind.Int:
                    return DynamicVariant.FromInt((int)p.Numeric);
                case ValueKind.Float:
                    return DynamicVariant.FromFloat((float)p.Numeric);
                case ValueKind.String:
                    return DynamicVariant.FromString(p.Str);
                case ValueKind.Vector2:
                    return DynamicVariant.FromVector2(new Vector2(p.X, p.Y));
                case ValueKind.Vector3:
                    return DynamicVariant.FromVector3(new Vector3(p.X, p.Y, p.Z));
                case ValueKind.Vector4:
                    return DynamicVariant.FromVector4(new Vector4(p.X, p.Y, p.Z, p.W));
                case ValueKind.Color:
                    return DynamicVariant.FromColor(new Color(p.X, p.Y, p.Z, p.W));
                default:
                    return DynamicVariant.Null;
            }
        }

        static ScalarKeyPayload ExportScalar(ScalarKeyRuntime rt)
        {
            var modsList = new List<ScalarModPayload>(8);
            foreach (var s in rt.EnumerateSnapshots())
            {
                modsList.Add(new ScalarModPayload
                {
                    Kind = (byte)s.Kind,
                    Phase = (byte)s.Phase,
                    Value = s.Value,
                    Remain = s.Remain,
                    Layer = s.Layer ?? string.Empty,
                    Tag = s.Tag ?? string.Empty,
                });
            }

            return new ScalarKeyPayload
            {
                KeyId = rt.Key.Id,
                Name = rt.Key.Name ?? string.Empty,
                Baseline = rt.Baseline,
                LocalBase = rt.LocalBase,
                Mods = modsList.Count == 0 ? Array.Empty<ScalarModPayload>() : modsList.ToArray(),
            };
        }

        static void ImportScalar(IBaseScalarService scalar, ScalarKey key, in ScalarKeyPayload payload)
        {
            // Reset current local state for this key
            scalar.ClearAll(key);

            // Ensure runtime exists and set baseline/localbase
            scalar.SetRuntimeBaseline(key, payload.Baseline);
            scalar.SetLocalBase(key, payload.LocalBase);

            if (!scalar.TryGetRuntime(key, out var rt) || rt == null)
                return;

            if (payload.Mods == null || payload.Mods.Length == 0)
                return;

            for (int i = 0; i < payload.Mods.Length; i++)
            {
                var m = payload.Mods[i];
                var kind = (ScalarModKind)m.Kind;
                var phase = (ScalarMulPhase)m.Phase;
                var layer = m.Layer ?? string.Empty;
                var tag = m.Tag;

                if (kind == ScalarModKind.Add)
                {
                    rt.Add(scalar, layer, m.Value, m.Remain, source: null, tag: tag);
                }
                else
                {
                    rt.Mul(scalar, layer, m.Value, phase, m.Remain, source: null, tag: tag);
                }
            }
        }
    }
}

#nullable disable
using Game.Commands;
using Game.Common;
using Game.Spawn;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    /// <summary>
    /// FirePattern 用の DynamicContext。
    /// 入力/FirePoint 由来の値を Vars(IVarStore) に詰めて DynamicValue を解決する。
    /// </summary>
    public sealed class FireDynamicContext : IDynamicContext
    {
        /// <summary>
        /// このコンテキストが Vars に投入する stableKey 一覧。
        /// </summary>
        // Keys populated by this dynamic context for FirePattern evaluation.
        // Use camelCase keys for clarity and consistency with code conventions.
        public static readonly string[] ContextKeys =
        {
            "spawnIndex",
            "spawnPosition",
            "spawnDirection",
            "spawnSpeed",
            "spawnDelayTime",
            "spawnCount",
            "emitIndex",
            "emitCount",
            "distanceFromEmitter",
            "emitterPosition",

            "fireIndex",
            "fireCount",
            "fireNormalized",
            "hasTarget",
            "targetDistance",
            "targetHitCount",
            "firePosition",
            "fireBaseDirection",
            "fireTargetDirection",
            "fireRepeatIndex",
        };

        public static string ContextKeysText => string.Join("\n", ContextKeys);

        readonly VarStore _vars;
        readonly IScopeNode _scope;
        readonly UnitSpawnContext _input;

        public IVarStore Vars => _vars;
        public IScopeNode Scope => _scope;
        public IScopeNode CommandRootScope => null;

        public FireDynamicContext(in UnitSpawnContext inputContext)
        {
            _vars = new VarStore();
            _input = inputContext;

            _scope = ScopeFeatureInstallerUtility.CaptureSpawnedLifetime(inputContext.UnitResolver).ScopeNode;

            SetFromSpawnContext(in inputContext);
            SetFireDefaults();
        }

        void SetVariant(int varId, in DynamicVariant value)
        {
            if (varId == 0)
                return;
            _vars.TrySetVariant(varId, value);
        }

        void SetVariant(string stableKey, in DynamicVariant value)
        {
            if (!VarIdResolver.TryResolve(stableKey, out var varId) || varId == 0)
                return;
            _vars.TrySetVariant(varId, value);
        }

        void SetFromSpawnContext(in UnitSpawnContext inputContext)
        {
            var b = inputContext.Base;
            var d = b.Data;

            // Populate FirePattern-focused keys (camelCase).
            SetVariant("spawnIndex", DynamicVariant.FromInt(b.Index));
            SetVariant("spawnPosition", DynamicVariant.FromVector3(b.Position));
            SetVariant("spawnDirection", DynamicVariant.FromVector3(d.Direction));
            SetVariant("spawnSpeed", DynamicVariant.FromFloat(d.Speed));
            SetVariant("spawnDelayTime", DynamicVariant.FromFloat(d.DelayTime));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.spawnCount, DynamicVariant.FromInt(b.SpawnCount));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitIndex, DynamicVariant.FromInt(b.EmitIndex));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitCount, DynamicVariant.FromInt(b.EmitCount));
            SetVariant("distanceFromEmitter", DynamicVariant.FromFloat(b.DistanceFromEmitter));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitterPosition, DynamicVariant.FromVector3(b.EmitterPosition));
        }

        void SetFireDefaults()
        {
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireIndex, DynamicVariant.FromInt(0));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireCount, DynamicVariant.FromInt(0));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireNormalized, DynamicVariant.FromFloat(0f));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.hasTarget, DynamicVariant.FromFloat(0f));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.targetDistance, DynamicVariant.FromFloat(0f));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.targetHitCount, DynamicVariant.FromInt(0));
        }

        public void SetFromFirePoint(in FirePoint point)
        {
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireIndex, DynamicVariant.FromInt(point.Index));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireCount, DynamicVariant.FromInt(point.TotalCount));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireNormalized, DynamicVariant.FromFloat(point.NormalizedPosition));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.hasTarget, DynamicVariant.FromFloat(point.HasTarget ? 1f : 0f));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.targetDistance, DynamicVariant.FromFloat(point.TargetDistance));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.targetHitCount, DynamicVariant.FromInt(point.TargetHitCount));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.firePosition, DynamicVariant.FromVector3(point.Position));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireBaseDirection, DynamicVariant.FromVector3(point.BaseDirection));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireTargetDirection, DynamicVariant.FromVector3(point.TargetDirection));
            SetVariant(VarIds.GameLib.SpawnPattern.FirePattern.Common.fireRepeatIndex, DynamicVariant.FromInt(point.FireRepeatIndex));
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter) => null;
    }
}

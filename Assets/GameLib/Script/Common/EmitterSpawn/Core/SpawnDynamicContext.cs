#nullable disable
using Game;
using Game.Commands;
using Game.Common;
using Game.Vars.Generated;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// SpawnPattern 評価用の DynamicContext。
    /// 入力データを Vars(IVarStore) 経由で DynamicValue に渡す。
    /// </summary>
    public sealed class SpawnDynamicContext : IDynamicContext
    {
        /// <summary>
        /// このコンテキストが Vars に投入する stableKey 一覧。
        /// </summary>
        // Use camelCase keys for DynamicValue expressions.
        public static readonly string[] ContextKeys =
        {
            "emitterPosition",
            "emitterRotation",
            "emitIndex",
            "emitCount",

            "index",
            "position",
            "direction",
            "distance",
            "tangent",
            "normalized",
            "spawnCount",
        };

        public static string ContextKeysText => string.Join("\n", ContextKeys);

        readonly VarStore _vars;
        readonly IScopeNode _scope;

        public IVarStore Vars => _vars;
        public IScopeNode Scope => _scope;
        public IScopeNode CommandRootScope => null;

        public SpawnDynamicContext(IScopeNode scope)
        {
            _vars = new VarStore();
            _scope = scope;
        }

        void SetVariant(int varId, in DynamicVariant value)
        {
            if (varId == 0)
                return;
            _vars.TrySetVariant(varId, value);
        }

        public void SetEmitterInfo(Vector3 origin, Quaternion rotation, int emitIndex, int emitCount)
        {
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitterPosition, DynamicVariant.FromVector3(origin));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitterRotation, DynamicVariant.FromVector3(rotation.eulerAngles));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitIndex, DynamicVariant.FromInt(emitIndex));
            SetVariant(VarIds.GameLib.SpawnPattern.Common.emitCount, DynamicVariant.FromInt(emitCount));
        }

        public void SetFromSpawnPoint(in SpawnPoint point)
        {
            SetVariant(VarIds.GameLib.SpawnPattern.Point.index, DynamicVariant.FromInt(point.Index));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.position, DynamicVariant.FromVector3(point.Position));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.direction, DynamicVariant.FromVector3(point.DirectionFromOrigin));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.distance, DynamicVariant.FromFloat(point.DistanceFromOrigin));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.tangent, DynamicVariant.FromVector3(point.TangentDirection));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.normalized, DynamicVariant.FromFloat(point.NormalizedPosition));
            SetVariant(VarIds.GameLib.SpawnPattern.Point.spawnCount, DynamicVariant.FromInt(point.SpawnCount));
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter) => null;
    }
}

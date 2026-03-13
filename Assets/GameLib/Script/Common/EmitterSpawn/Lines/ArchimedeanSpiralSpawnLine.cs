#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class ArchimedeanSpiralSpawnLine : SpawnLineDefinition
    {
        [SerializeField] DynamicValue<float> _startRadius = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _radiusPerTurn = DynamicValueExtensions.FromLiteral(1f);
        [SerializeField] DynamicValue<float> _turns = DynamicValueExtensions.FromLiteral(3f);
        [SerializeField] DynamicValue<int> _segments = DynamicValueExtensions.FromLiteral(100);
        [SerializeField] Vector3 _center = default;

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreateArchimedeanSpiral(
                _startRadius.Resolve(ctx),
                _radiusPerTurn.Resolve(ctx),
                _turns.Resolve(ctx),
                Mathf.Max(2, _segments.Resolve(ctx)),
                _center);
    }
}

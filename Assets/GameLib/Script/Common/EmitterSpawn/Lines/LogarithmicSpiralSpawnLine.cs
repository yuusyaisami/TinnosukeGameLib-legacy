#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class LogarithmicSpiralSpawnLine : SpawnLineDefinition
    {
        [SerializeField] DynamicValue<float> _a = DynamicValueExtensions.FromLiteral(0.1f);
        [SerializeField] DynamicValue<float> _b = DynamicValueExtensions.FromLiteral(0.2f);
        [SerializeField] DynamicValue<float> _turns = DynamicValueExtensions.FromLiteral(3f);
        [SerializeField] DynamicValue<int> _segments = DynamicValueExtensions.FromLiteral(100);
        [SerializeField] Vector3 _center = default;

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreateLogarithmicSpiral(
                _a.Resolve(ctx),
                _b.Resolve(ctx),
                _turns.Resolve(ctx),
                Mathf.Max(2, _segments.Resolve(ctx)),
                _center);
    }
}

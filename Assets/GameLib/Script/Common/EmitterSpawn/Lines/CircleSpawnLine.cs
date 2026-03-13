#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class CircleSpawnLine : SpawnLineDefinition
    {
        [SerializeField] DynamicValue<int> _segments = DynamicValueExtensions.FromLiteral(32);
        [SerializeField] DynamicValue<float> _radius = DynamicValueExtensions.FromLiteral(1f);
        [SerializeField] Vector3 _center = default;

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreateCircle(Mathf.Max(3, _segments.Resolve(ctx)), _radius.Resolve(ctx), _center);
    }
}

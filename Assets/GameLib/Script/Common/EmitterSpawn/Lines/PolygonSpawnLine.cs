#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class PolygonSpawnLine : SpawnLineDefinition
    {
        [SerializeField] DynamicValue<int> _sides = DynamicValueExtensions.FromLiteral(6);
        [SerializeField] DynamicValue<float> _radius = DynamicValueExtensions.FromLiteral(1f);
        [SerializeField] Vector3 _center = default;

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreatePolygon(Mathf.Max(3, _sides.Resolve(ctx)), _radius.Resolve(ctx), _center);
    }
}

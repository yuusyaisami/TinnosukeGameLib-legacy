#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class LineSpawnLine : SpawnLineDefinition
    {
        [SerializeField] DynamicValue<Vector3> _start = DynamicValue<Vector3>.FromSource(LiteralSource.FromVector3(Vector3.left));
        [SerializeField] DynamicValue<Vector3> _end = DynamicValue<Vector3>.FromSource(LiteralSource.FromVector3(Vector3.right));
        [SerializeField] DynamicValue<int> _segments = DynamicValueExtensions.FromLiteral(10);

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreateLine(_start.Resolve(ctx), _end.Resolve(ctx), Mathf.Max(2, _segments.Resolve(ctx)));
    }
}

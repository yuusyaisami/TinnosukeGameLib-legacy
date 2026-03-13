#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class CatmullRomSpawnLine : SpawnLineDefinition
    {
        [SerializeField] Vector3[] _controlPoints = Array.Empty<Vector3>();
        [SerializeField] DynamicValue<int> _segmentsPerCurve = DynamicValueExtensions.FromLiteral(10);
        [SerializeField] bool _closed;

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.CreateCatmullRom(_controlPoints, Mathf.Max(1, _segmentsPerCurve.Resolve(ctx)), _closed);

        public override Vector3[] GetPreviewPoints(int maxPoints) => _controlPoints ?? Array.Empty<Vector3>();
    }
}

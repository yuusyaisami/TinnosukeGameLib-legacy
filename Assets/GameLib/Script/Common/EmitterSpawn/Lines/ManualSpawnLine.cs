#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    [Serializable]
    public sealed class ManualSpawnLine : SpawnLineDefinition
    {
        [SerializeField]
        Vector3[] _points = Array.Empty<Vector3>();

        public override SpawnLine Build(IDynamicContext ctx)
            => SpawnLineFactory.FromPoints(_points ?? Array.Empty<Vector3>());

        public override Vector3[] GetPreviewPoints(int maxPoints) => _points ?? Array.Empty<Vector3>();
    }
}

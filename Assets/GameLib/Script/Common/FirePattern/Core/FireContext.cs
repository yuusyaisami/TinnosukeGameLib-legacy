#nullable enable
using Game.Spawn;
using UnityEngine;

namespace Game.Fire
{
    public readonly struct FireContext
    {
        public readonly FirePoint Point;
        public readonly FireData Data;

        public readonly Vector3 FinalPosition;
        public readonly Vector3 FinalDirection;
        public readonly Vector3 Velocity;

        public readonly UnitSpawnContext InputContext;

        public FireContext(
            FirePoint point,
            FireData data,
            Vector3 finalPosition,
            Vector3 finalDirection,
            Vector3 velocity,
            UnitSpawnContext inputContext)
        {
            Point = point;
            Data = data;
            FinalPosition = finalPosition;
            FinalDirection = finalDirection;
            Velocity = velocity;
            InputContext = inputContext;
        }
    }
}

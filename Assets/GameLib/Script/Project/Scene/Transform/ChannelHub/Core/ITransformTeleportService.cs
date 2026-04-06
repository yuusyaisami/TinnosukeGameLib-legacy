#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    public interface ITransformTeleportService
    {
        bool TryTeleportWorld(Vector3 worldPosition, bool resetVelocity = true);
    }
}

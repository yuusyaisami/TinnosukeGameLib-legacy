#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    public interface ITransformControllerPoseReader
    {
        Transform TargetTransform { get; }
        TransformOutputTarget OutputTarget { get; }
        Vector2 CurrentVelocity { get; }
    }
}

#nullable enable
using System;

namespace Game.Kernel.Layers
{
    public enum KernelLayerKind
    {
        Unknown = 0,
        Application = 10,
        Scene = 20,
    }

    public enum KernelLayerState
    {
        Created = 10,
        Initialized = 20,
        Shutdown = 30,
    }

    public readonly struct SceneKernelHandle : IEquatable<SceneKernelHandle>
    {
        public SceneKernelHandle(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "SceneKernelHandle must be positive.");

            Value = value;
        }

        public int Value { get; }

        public bool Equals(SceneKernelHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is SceneKernelHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "SceneKernelHandle(" + Value + ")";
        }

        public static bool operator ==(SceneKernelHandle left, SceneKernelHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneKernelHandle left, SceneKernelHandle right)
        {
            return !left.Equals(right);
        }
    }
}

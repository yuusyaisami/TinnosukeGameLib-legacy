#nullable enable
using System;

namespace Game.Commands.VNext
{
    public readonly struct CommandKeyId : IEquatable<CommandKeyId>
    {
        public readonly int Value;

        public CommandKeyId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value > 0;

        public bool Equals(CommandKeyId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is CommandKeyId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(CommandKeyId left, CommandKeyId right) => left.Equals(right);
        public static bool operator !=(CommandKeyId left, CommandKeyId right) => !left.Equals(right);
    }
}

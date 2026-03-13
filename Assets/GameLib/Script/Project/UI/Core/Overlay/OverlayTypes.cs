#nullable enable

using System;
using UnityEngine;

namespace Game.UI
{
    [Flags]
    public enum OverlayLayerId
    {
        None = 0,
        Tooltip = 1 << 0,
        Dialog = 1 << 1,
        TopMost = 1 << 2,
        Custom0 = 1 << 3,
        Custom1 = 1 << 4,
    }

    public readonly struct OverlayLayerMask : IEquatable<OverlayLayerMask>
    {
        readonly int _bits;

        public OverlayLayerMask(int bits)
        {
            _bits = bits;
        }

        public bool IsNone => _bits == 0;

        public bool IsSingleBit => _bits != 0 && (_bits & (_bits - 1)) == 0;

        public int Value => _bits;

        public static OverlayLayerMask None => new(0);
        public static OverlayLayerMask Tooltip => new((int)OverlayLayerId.Tooltip);
        public static OverlayLayerMask Dialog => new((int)OverlayLayerId.Dialog);
        public static OverlayLayerMask TopMost => new((int)OverlayLayerId.TopMost);

        public static OverlayLayerMask From(OverlayLayerId id) => new((int)id);

        public static OverlayLayerMask Combine(params OverlayLayerId[] ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var bits = 0;
            foreach (var id in ids)
            {
                bits |= (int)id;
            }

            return new OverlayLayerMask(bits);
        }

        public bool Contains(OverlayLayerId id) => (_bits & (int)id) != 0;

        public bool Intersects(OverlayLayerMask other) => (_bits & other._bits) != 0;

        public bool ContainsAll(OverlayLayerMask other) => (_bits & other._bits) == other._bits;

        public int BitCount => CountBits((uint)_bits);

        public OverlayLayerMask With(OverlayLayerId id) => new(_bits | (int)id);

        static int CountBits(uint value)
        {
            value = value - ((value >> 1) & 0x55555555u);
            value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24);
        }

        public static OverlayLayerMask operator |(OverlayLayerMask left, OverlayLayerMask right)
            => new(left._bits | right._bits);

        public static OverlayLayerMask operator &(OverlayLayerMask left, OverlayLayerMask right)
            => new(left._bits & right._bits);

        public bool Equals(OverlayLayerMask other) => _bits == other._bits;

        public override bool Equals(object? obj) => obj is OverlayLayerMask other && Equals(other);

        public override int GetHashCode() => _bits;

        public override string ToString() => $"OverlayLayerMask({_bits})";
    }

    public readonly struct OverlayLayerDefinition
    {
        public OverlayLayerMask Mask { get; }
        public Transform Root { get; }
        public string Name { get; }
        public int Priority { get; }

        public OverlayLayerDefinition(OverlayLayerMask mask, Transform root, string? name = null, int priority = 0)
        {
            Mask = mask;
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Name = string.IsNullOrWhiteSpace(name) ? "OverlayLayer" : name!;
            Priority = priority;
        }
    }

    public readonly struct UIOverlayLayerOptions
    {
        public Transform LayerRoot { get; }
        public OverlayLayerDefinition[] Layers { get; }

        public UIOverlayLayerOptions(Transform layerRoot, OverlayLayerDefinition[] layers)
        {
            LayerRoot = layerRoot ?? throw new ArgumentNullException(nameof(layerRoot));
            Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        }
    }
}
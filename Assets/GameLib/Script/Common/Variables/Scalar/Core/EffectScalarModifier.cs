using System;
using System.Collections.Generic;

namespace Game.Scalar
{
    /// <summary>
    /// Add 操作に対してレイヤー別の Add/Mul 効果を与える Mod。
    /// </summary>
    public sealed class EffectScalarModifier : IScalarAddModifier
    {
        struct Slot
        {
            public float Add;
            public float Mul1;
            public float Mul2;
        }

        readonly ScalarKeyRuntime _runtime;
        readonly System.Action _invalidate;
        readonly Dictionary<string, Slot> _slots;

        internal EffectScalarModifier(ScalarKeyRuntime runtime, System.Action invalidate)
        {
            _runtime = runtime;
            _invalidate = invalidate ?? (() => { });
            _slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        }

        Slot GetOrCreateSlot(string layer)
        {
            layer ??= string.Empty;

            if (!_slots.TryGetValue(layer, out var slot))
            {
                slot.Add = 0f;
                slot.Mul1 = 1f; // デフォルト
                slot.Mul2 = 1f;
            }

            return slot;
        }

        public void Add(string layer, float add)
        {
            layer ??= string.Empty;
            var slot = GetOrCreateSlot(layer);
            slot.Add = add; // 上書き
            _slots[layer] = slot;
            _invalidate();
        }

        public void Mul(string layer, float factor, ScalarMulPhase phase = ScalarMulPhase.PreAdd)
        {
            layer ??= string.Empty;
            var slot = GetOrCreateSlot(layer);

            if (phase == ScalarMulPhase.PreAdd)
                slot.Mul1 = factor;
            else
                slot.Mul2 = factor;

            _slots[layer] = slot;
            _invalidate();
        }

        public void Clear(string layer = null)
        {
            if (layer == null)
                _slots.Clear();
            else
                _slots.Remove(layer);

            _invalidate();
        }

        public void OnBeforeAdd(ref ScalarAddContext ctx)
        {
            Slot slot;

            if (ctx.Layer != null && _slots.TryGetValue(ctx.Layer, out slot))
            {
                // layer-specific
            }
            else if (_slots.TryGetValue(string.Empty, out slot))
            {
                // global
            }
            else
            {
                return;
            }

            // ((x * Mul1) + Add) * Mul2
            var v = ctx.Value;
            v = v * slot.Mul1;
            v = v + slot.Add;
            v = v * slot.Mul2;

            ctx.Value = v;
        }
    }

}

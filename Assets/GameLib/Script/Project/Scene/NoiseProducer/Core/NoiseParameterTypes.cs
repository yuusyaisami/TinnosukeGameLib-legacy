#nullable enable
using System.Runtime.InteropServices;
using DG.Tweening;
using UnityEngine;

namespace Game.NoiseProducer
{
    // ── NoiseParameterValueKind ─────────────────────────────────

    public enum NoiseParameterValueKind
    {
        Float = 10,
        Vector2 = 20,
        Color = 30,
        Bool = 40,
        Int = 50,
    }

    // ── NoiseParameterValue ─────────────────────────────────────

    [StructLayout(LayoutKind.Explicit)]
    public struct NoiseParameterValue
    {
        [FieldOffset(0)] public NoiseParameterValueKind Kind;
        [FieldOffset(4)] public float FloatValue;
        [FieldOffset(4)] public Vector2 Vector2Value;
        [FieldOffset(4)] public Color ColorValue;
        [FieldOffset(4)] public bool BoolValue;
        [FieldOffset(4)] public int IntValue;

        public static NoiseParameterValue Float(float v)
            => new() { Kind = NoiseParameterValueKind.Float, FloatValue = v };

        public static NoiseParameterValue Vec2(Vector2 v)
            => new() { Kind = NoiseParameterValueKind.Vector2, Vector2Value = v };

        public static NoiseParameterValue Col(Color v)
            => new() { Kind = NoiseParameterValueKind.Color, ColorValue = v };

        public static NoiseParameterValue Bool(bool v)
            => new() { Kind = NoiseParameterValueKind.Bool, BoolValue = v };

        public static NoiseParameterValue Int(int v)
            => new() { Kind = NoiseParameterValueKind.Int, IntValue = v };
    }

    // ── NoiseParameterAddress ───────────────────────────────────

    public readonly struct NoiseParameterAddress
    {
        public readonly string ChannelId;
        public readonly string ParameterKey;
        public readonly string LayerTag;

        public NoiseParameterAddress(string channelId, string parameterKey, string layerTag)
        {
            ChannelId = channelId;
            ParameterKey = parameterKey;
            LayerTag = layerTag;
        }
    }

    // ── NoiseParameterWriteRequest ──────────────────────────────

    public readonly struct NoiseParameterWriteRequest
    {
        public readonly NoiseParameterAddress Address;
        public readonly NoiseParameterValue Value;
        public readonly float Duration;
        public readonly Ease Ease;

        public NoiseParameterWriteRequest(
            in NoiseParameterAddress address,
            NoiseParameterValue value,
            float duration = 0f,
            Ease ease = Ease.Linear)
        {
            Address = address;
            Value = value;
            Duration = duration;
            Ease = ease;
        }
    }

    // ── NoiseChannelState ───────────────────────────────────────

    public readonly struct NoiseChannelState
    {
        public readonly bool IsActive;
        public readonly bool IsTemporalActive;
        public readonly float ChannelTime;
        public readonly int LastRenderedFrame;
        public readonly int ParameterCount;
        public readonly int StageCount;

        public NoiseChannelState(
            bool isActive,
            bool isTemporalActive,
            float channelTime,
            int lastRenderedFrame,
            int parameterCount,
            int stageCount)
        {
            IsActive = isActive;
            IsTemporalActive = isTemporalActive;
            ChannelTime = channelTime;
            LastRenderedFrame = lastRenderedFrame;
            ParameterCount = parameterCount;
            StageCount = stageCount;
        }
    }
}

#nullable enable

namespace Game.NoiseProducer
{
    // ── StageKind ───────────────────────────────────────────────

    public enum NoiseStageKind
    {
        Generator = 10,
        Uv = 20,
        Filter = 30,
        Composite = 40,
    }

    // ── Generator Ops ───────────────────────────────────────────

    public enum NoiseGeneratorOp
    {
        SolidColor = 10,
        GradientLinear = 20,
        GradientRadial = 30,
        ValueNoise = 40,
        PerlinLike = 50,
        SimplexLike = 60,
        Fbm = 70,
    }

    // ── UV Ops ──────────────────────────────────────────────────

    public enum NoiseUvOp
    {
        Scroll = 10,
        Flow = 20,
        Rotate = 30,
        Polar = 40,
    }

    // ── Filter Ops ──────────────────────────────────────────────

    public enum NoiseFilterOp
    {
        Warp = 10,
        Levels = 20,
        Clamp = 30,
        Invert = 40,
        NormalFromHeight = 50,
    }

    // ── Composite Ops ───────────────────────────────────────────

    public enum NoiseCompositeOp
    {
        Blend = 10,
        Add = 20,
        Multiply = 30,
        Min = 40,
        Max = 50,
        MaskBlend = 60,
    }
}

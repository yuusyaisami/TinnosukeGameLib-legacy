// Compatibility aliases for legacy TextFx keys.
// Do not remove without migrating all call sites.

namespace Game.MaterialFx.Generated
{
    public static partial class MaterialFxKeys
    {
        public static partial class BaseShader
        {
            public static partial class TextFx
            {
                // Legacy flat key aliases
                public const string OutlineEnabled = "BaseShader/TextFx/Outline/Enabled";
                public const string OutlineColor = "BaseShader/TextFx/Outline/Color";
                public const string OutlineThickness = "BaseShader/TextFx/Outline/Thickness";
                public const string OutlineSoftness = "BaseShader/TextFx/Outline/Softness";

                public const string ShadowEnabled = "BaseShader/TextFx/Shadow/Enabled";
                public const string ShadowColor = "BaseShader/TextFx/Shadow/Color";
                public const string ShadowOffset = "BaseShader/TextFx/Shadow/Offset";
                public const string ShadowSoftness = "BaseShader/TextFx/Shadow/Softness";

                public const string GlowEnabled = "BaseShader/TextFx/Glow/Enabled";
                public const string GlowColor = "BaseShader/TextFx/Glow/Color";
                public const string GlowThickness = "BaseShader/TextFx/Glow/Thickness";
                public const string GlowSoftness = "BaseShader/TextFx/Glow/Softness";
            }
        }
    }
}

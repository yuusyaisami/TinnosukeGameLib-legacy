#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Channel
{
    public enum TextStyleCommandMode
    {
        UseActive = 0,
        UseDefault = 1,
        OverrideThisTextOnly = 2,
        OverrideAndSetActive = 3,
        SetActiveToDefault = 4,
    }

    [Serializable]
    public sealed class TextStyleOverrideSettings
    {
        [BoxGroup("Font")]
        [LabelText("Apply Font Style")]
        public bool ApplyFontStyle;

        [BoxGroup("Font")]
        [ShowIf(nameof(ApplyFontStyle))]
        [LabelText("Font Style")]
        [ValueDropdown(nameof(GetFontStyleOptions))]
        public FontStyles FontStyle = FontStyles.Normal;

        [BoxGroup("Font")]
        [LabelText("Apply Font Size")]
        public bool ApplyFontSize;

        [BoxGroup("Font")]
        [ShowIf(nameof(ApplyFontSize))]
        [LabelText("Font Size")]
        [Min(0f)]
        public float FontSize = 36f;

        [BoxGroup("Color")]
        [LabelText("Apply Vertex Color")]
        public bool ApplyVertexColor;

        [BoxGroup("Color")]
        [ShowIf(nameof(ApplyVertexColor))]
        [LabelText("Vertex Color")]
        public Color VertexColor = Color.white;

        [BoxGroup("Color")]
        [LabelText("Apply Color Gradient")]
        public bool ApplyColorGradient;

        [BoxGroup("Color")]
        [ShowIf(nameof(ApplyColorGradient))]
        [LabelText("Enable Gradient")]
        public bool EnableColorGradient;

        [BoxGroup("Color")]
        [ShowIf("@ApplyColorGradient && EnableColorGradient")]
        [LabelText("Gradient Preset")]
        public TMP_ColorGradient? ColorGradientPreset;

        [BoxGroup("Spacing")]
        [LabelText("Apply Character Spacing")]
        public bool ApplyCharacterSpacing;

        [BoxGroup("Spacing")]
        [ShowIf(nameof(ApplyCharacterSpacing))]
        [LabelText("Character Spacing")]
        public float CharacterSpacing;

        [BoxGroup("Spacing")]
        [LabelText("Apply Word Spacing")]
        public bool ApplyWordSpacing;

        [BoxGroup("Spacing")]
        [ShowIf(nameof(ApplyWordSpacing))]
        [LabelText("Word Spacing")]
        public float WordSpacing;

        [BoxGroup("Spacing")]
        [LabelText("Apply Line Spacing")]
        public bool ApplyLineSpacing;

        [BoxGroup("Spacing")]
        [ShowIf(nameof(ApplyLineSpacing))]
        [LabelText("Line Spacing")]
        public float LineSpacing;

        [BoxGroup("Spacing")]
        [LabelText("Apply Paragraph Spacing")]
        public bool ApplyParagraphSpacing;

        [BoxGroup("Spacing")]
        [ShowIf(nameof(ApplyParagraphSpacing))]
        [LabelText("Paragraph Spacing")]
        public float ParagraphSpacing;

        [BoxGroup("Layout")]
        [LabelText("Apply Alignment")]
        public bool ApplyAlignment;

        [BoxGroup("Layout")]
        [ShowIf(nameof(ApplyAlignment))]
        [LabelText("Alignment")]
        [ValueDropdown(nameof(GetAlignmentOptions))]
        public TextAlignmentOptions Alignment = TextAlignmentOptions.TopLeft;

        [BoxGroup("Layout")]
        [LabelText("Apply Word Wrapping")]
        public bool ApplyWordWrapping;

        [BoxGroup("Layout")]
        [ShowIf(nameof(ApplyWordWrapping))]
        [LabelText("Enable Word Wrapping")]
        public bool EnableWordWrapping = true;

        [BoxGroup("Layout")]
        [LabelText("Apply Overflow")]
        public bool ApplyOverflow;

        [BoxGroup("Layout")]
        [ShowIf(nameof(ApplyOverflow))]
        [LabelText("Overflow")]
        [ValueDropdown(nameof(GetOverflowOptions))]
        public TextOverflowModes Overflow = TextOverflowModes.Overflow;

        static IEnumerable<ValueDropdownItem<FontStyles>> GetFontStyleOptions()
        {
            yield return new ValueDropdownItem<FontStyles>("Normal", FontStyles.Normal);
            yield return new ValueDropdownItem<FontStyles>("Bold", FontStyles.Bold);
            yield return new ValueDropdownItem<FontStyles>("Italic", FontStyles.Italic);
            yield return new ValueDropdownItem<FontStyles>("Underline", FontStyles.Underline);
            yield return new ValueDropdownItem<FontStyles>("Lowercase", FontStyles.LowerCase);
            yield return new ValueDropdownItem<FontStyles>("Uppercase", FontStyles.UpperCase);
            yield return new ValueDropdownItem<FontStyles>("Small Caps", FontStyles.SmallCaps);
            yield return new ValueDropdownItem<FontStyles>("Strikethrough", FontStyles.Strikethrough);
            yield return new ValueDropdownItem<FontStyles>("Highlight", FontStyles.Highlight);
        }

        static IEnumerable<ValueDropdownItem<TextAlignmentOptions>> GetAlignmentOptions()
        {
            yield return new ValueDropdownItem<TextAlignmentOptions>("Top Left", TextAlignmentOptions.TopLeft);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Top Center", TextAlignmentOptions.Top);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Top Right", TextAlignmentOptions.TopRight);

            yield return new ValueDropdownItem<TextAlignmentOptions>("Middle Left", TextAlignmentOptions.Left);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Middle Center", TextAlignmentOptions.Center);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Middle Right", TextAlignmentOptions.Right);

            yield return new ValueDropdownItem<TextAlignmentOptions>("Bottom Left", TextAlignmentOptions.BottomLeft);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Bottom Center", TextAlignmentOptions.Bottom);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Bottom Right", TextAlignmentOptions.BottomRight);

            yield return new ValueDropdownItem<TextAlignmentOptions>("Baseline Left", TextAlignmentOptions.BaselineLeft);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Baseline", TextAlignmentOptions.Baseline);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Baseline Right", TextAlignmentOptions.BaselineRight);

            yield return new ValueDropdownItem<TextAlignmentOptions>("Justified", TextAlignmentOptions.Justified);
            yield return new ValueDropdownItem<TextAlignmentOptions>("Flush", TextAlignmentOptions.Flush);
        }

        static IEnumerable<ValueDropdownItem<TextOverflowModes>> GetOverflowOptions()
        {
            yield return new ValueDropdownItem<TextOverflowModes>("Overflow", TextOverflowModes.Overflow);
            yield return new ValueDropdownItem<TextOverflowModes>("Ellipsis", TextOverflowModes.Ellipsis);
            yield return new ValueDropdownItem<TextOverflowModes>("Masking", TextOverflowModes.Masking);
            yield return new ValueDropdownItem<TextOverflowModes>("Truncate", TextOverflowModes.Truncate);
            yield return new ValueDropdownItem<TextOverflowModes>("ScrollRect", TextOverflowModes.ScrollRect);
            yield return new ValueDropdownItem<TextOverflowModes>("Page", TextOverflowModes.Page);
            yield return new ValueDropdownItem<TextOverflowModes>("Linked", TextOverflowModes.Linked);
        }
    }

    [Serializable]
    public struct TextStyleCommandOptions
    {
        [LabelText("Enable Style Command")]
        public bool Enabled;

        [LabelText("Style Mode"), ShowIf(nameof(Enabled))]
        [EnumToggleButtons]
        public TextStyleCommandMode Mode;

        [ShowIf("@Enabled && (Mode == TextStyleCommandMode.OverrideThisTextOnly || Mode == TextStyleCommandMode.OverrideAndSetActive)")]
        [InlineProperty]
        public TextStyleOverrideSettings Override;
    }
}

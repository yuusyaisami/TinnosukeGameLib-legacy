// Game.Animation.Channel

using System.Collections.Generic;
using UnityEngine;
using Game.MaterialFx;

namespace Game.Channel
{
    /// <summary>チャネルの論理名（tag）を表す共通インターフェース。</summary>
    public interface IChannelIdentity
    {
        string Tag { get; }
    }

    /// <summary>Sprite/Image 用チャネル定義。</summary>
    public interface IChannelSprite
    {
        SpriteRenderer SpriteRenderer { get; }
        UnityEngine.UI.Image Image { get; }

        AnimationSpritePreset SpritePreset { get; }
    }

    /// <summary>Text 用チャネル定義。</summary>
    public interface IChannelText
    {
        TMPro.TMP_Text Text { get; }
    }

    /// <summary>Transform / RectTransform 用チャネル定義。</summary>
    public interface IChannelTransform
    {
        Transform Transform { get; }
        RectTransform RectTransform { get; }

        ITransformAnimationPreset TransformPreset { get; }
    }

    /// <summary>BodyFx / Shader 系の定義。</summary>
    public interface IChannelMaterialFx
    {
        IReadOnlyList<MaterialFxPresetEntry> MaterialFxPresetEntries { get; }
    }
}

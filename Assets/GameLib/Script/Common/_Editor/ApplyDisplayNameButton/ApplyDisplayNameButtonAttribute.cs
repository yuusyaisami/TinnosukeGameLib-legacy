using System;
using UnityEngine;

public enum ApplyDisplayNameMode
{
    Button,
    AutoOnChange,
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ApplyDisplayNameButtonAttribute : PropertyAttribute
{
    public string ButtonText { get; }
    public ApplyDisplayNameMode Mode { get; }

    /// <param name="buttonText">ボタン表示用のラベル。Mode が Button のときのみ使用。</param>
    /// <param name="mode">Button: ボタンを押したときに更新。AutoOnChange: 値変更時に自動適用（重複名があればスキップ）。</param>
    public ApplyDisplayNameButtonAttribute(ApplyDisplayNameMode mode = ApplyDisplayNameMode.Button, string buttonText = "Apply")
    {
        ButtonText = string.IsNullOrEmpty(buttonText) ? "Apply" : buttonText;
        Mode = mode;
    }
}

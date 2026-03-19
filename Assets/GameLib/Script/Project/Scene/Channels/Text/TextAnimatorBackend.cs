#nullable enable
using System;
using Febucci.TextAnimatorCore.Settings;
using Febucci.TextAnimatorCore.Time;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEngine;
using GameTimeScaleBehavior = Game.Times.TimeScaleBehavior;

namespace Game.Channel
{
    internal interface ITextBackend
    {
        TMP_Text Target { get; }
        bool SupportsRichAnimation { get; }
        bool SupportsTypewriter { get; }
        bool IsTypewriterRunning { get; }
        TypewriterComponent? Typewriter { get; }

        void SetText(string text, bool useTypewriter);
        void Append(string text, bool useTypewriter);
        void Clear();
        void Skip();
        void SetVisible(bool visible);
        void SetFontSize(float size);

        /// <summary>
        /// TextAnimator の TimeScale を LTS の TimeScaleBehavior に合わせて設定します。
        /// </summary>
        void SetTimeScaleBehavior(GameTimeScaleBehavior behavior);
    }

    /// <summary>
    /// TMP_Text のみを使用するシンプルなバックエンド。
    /// TextAnimator を使用しない場合のフォールバック。
    /// </summary>
    internal sealed class TmpTextBackend : ITextBackend
    {
        public TMP_Text Target { get; }
        public bool SupportsRichAnimation => false;
        public bool SupportsTypewriter => false;
        public bool IsTypewriterRunning => false;
        public TypewriterComponent? Typewriter => null;

        public TmpTextBackend(TMP_Text target) => Target = target;

        public void SetText(string text, bool useTypewriter) => Target.text = text ?? "";
        public void Append(string text, bool useTypewriter) => Target.text += text ?? "";
        public void Clear() => Target.text = "";
        public void Skip() { }
        public void SetVisible(bool visible) => Target.gameObject.SetActive(visible);
        public void SetFontSize(float size) => Target.fontSize = size;

        // TMP_Text単体では TimeScale 設定は不要
        public void SetTimeScaleBehavior(GameTimeScaleBehavior behavior) { }
    }

    /// <summary>
    /// TextAnimator を使用するバックエンド。
    /// TextAnimator_TMP と TypewriterComponent を直接参照する。
    /// </summary>
    internal sealed class TextAnimatorBackend : ITextBackend
    {
        public TMP_Text Target { get; }
        public bool SupportsRichAnimation => _animator != null;
        public bool SupportsTypewriter => _typewriter != null;
        public bool IsTypewriterRunning => _typewriter != null && _typewriter.IsShowingText;
        public TypewriterComponent? Typewriter => _typewriter;

        readonly TextAnimator_TMP? _animator;
        readonly TypewriterComponent? _typewriter;

        public TextAnimatorBackend(TMP_Text target, bool ensureComponents)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));

            var go = target.gameObject;

            // コンポーネントを取得（ensureComponents の場合は自動追加）
            if (!go.TryGetComponent(out _animator))
            {
                if (ensureComponents)
                    _animator = go.AddComponent<TextAnimator_TMP>();
            }

            if (!go.TryGetComponent(out _typewriter))
            {
                if (ensureComponents && _animator != null)
                    _typewriter = go.AddComponent<TypewriterComponent>();
            }
        }

        public void SetText(string text, bool useTypewriter)
        {
            text ??= "";

            if (useTypewriter && SupportsTypewriter)
            {
                if (_typewriter != null)
                {
                    _typewriter.ShowText(text);
                    return;
                }
            }

            if (_animator != null)
            {
                _animator.SetText(text);
                return;
            }

            Target.text = text; // フォールバック
        }

        public void Append(string text, bool useTypewriter)
        {
            text ??= "";

            if (_animator != null)
            {
                var useTypewriterAppend = useTypewriter && SupportsTypewriter;
                _animator.AppendText(text, hideText: useTypewriterAppend);

                if (useTypewriterAppend && _typewriter != null)
                {
                    _typewriter.StartShowingText(restart: false);
                }

                return;
            }

            Target.text += text;
        }

        public void Clear() => SetText("", useTypewriter: false);

        public void Skip()
        {
            _typewriter?.SkipTypewriter();
        }

        public void SetVisible(bool visible) => Target.gameObject.SetActive(visible);
        public void SetFontSize(float size) => Target.fontSize = size;

        /// <summary>
        /// TextAnimator の localSettings.timeScale を LTS の TimeScaleBehavior に合わせて設定します。
        /// </summary>
        public void SetTimeScaleBehavior(GameTimeScaleBehavior behavior)
        {
            if (_animator == null)
                return;

            // TextAnimator の localSettings.timeScale を設定
            // Febucci.TextAnimatorCore.Time.TimeScale: Scaled=0, Unscaled=1
            // Game.Times.TimeScaleBehavior: Scaled=0, Unscaled=1
            // 値は同じなので直接変換可能
            var febucciTimeScale = behavior == GameTimeScaleBehavior.Unscaled
                ? TimeScale.Unscaled
                : TimeScale.Scaled;

            _animator.localSettings.timeScale = febucciTimeScale;
        }
    }
}

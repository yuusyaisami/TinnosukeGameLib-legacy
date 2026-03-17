#nullable enable
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Febucci.TextAnimatorCore.Text;
using Febucci.TextAnimatorCore.Typing;
using Febucci.TextAnimatorForUnity;
using Game.Common;
using Game.Times;
using Game.MaterialFx;
using TMPro;
using UnityEngine;
using Game.Commands.VNext;

namespace Game.Channel
{
    public enum TextPlayMode
    {
        Instant,
        Typewriter,
        Count,
    }

    public interface ITextChannelPlayer : IDisposable
    {
        string Tag { get; }
        TMP_Text Target { get; }

        /// <summary>
        /// The MaterialFx service associated with this player (may be null if not initialized).
        /// Use this to apply shader-driven effects instead of directly setting material properties.
        /// </summary>
        IMaterialFxService? MaterialFx { get; }

        bool SupportsRichAnimation { get; }   // TextAnimator等
        bool SupportsTypewriter { get; }
        bool SupportsCounter { get; }

        void SetText(string text, TextPlayMode mode = TextPlayMode.Instant);
        void SetText(string text, TextPlayMode mode, in SetTextSettings settings);
        void Append(string text, TextPlayMode mode = TextPlayMode.Instant);
        void Clear();
        void Skip();                 // typewriter skip
        void SkipCounter();          // counter skip
        void SetVisible(bool visible);
        void SetFontSize(float size);
        void ApplyStyleCommand(in TextStyleCommandOptions options);
        void SetTypewriterEventCommands(TypewriterEventCommandRuntimeConfig? config, in TypewriterEventCommandRuntimeContext runtimeContext);
        UniTask WaitForTypewriterCompleteAsync(CancellationToken ct = default);

        UniTask PlayPresetAsync(ITextAnimationPreset preset, IVarStore variables, CancellationToken ct = default);
        void StopPreset();

        /// <summary>
        /// TextAnimator の TimeScale を LTS の TimeScaleBehavior に合わせて設定します。
        /// </summary>
        void SetTimeScaleBehavior(TimeScaleBehavior behavior);
    }
    public sealed class TextChannelPlayer : ITextChannelPlayer
    {
        struct TextStyleState
        {
            public FontStyles FontStyle;
            public float FontSize;
            public Color VertexColor;
            public bool EnableColorGradient;
            public TMP_ColorGradient? ColorGradientPreset;
            public float CharacterSpacing;
            public float WordSpacing;
            public float LineSpacing;
            public float ParagraphSpacing;
            public TextAlignmentOptions Alignment;
            public TextWrappingModes TextWrappingMode;
            public TextOverflowModes Overflow;
        }

        public string Tag { get; }
        public TMP_Text Target => _backend.Target;

        public IMaterialFxService? MaterialFx => _materialFxService;

        public bool SupportsRichAnimation => _backend.SupportsRichAnimation;
        public bool SupportsTypewriter => _backend.SupportsTypewriter;
        public bool SupportsCounter => _defaultUseCounter;

        readonly ITextBackend _backend;
        readonly bool _defaultUseTypewriter;
        readonly bool _defaultUseCounter;
        readonly IMaterialFxService? _materialFxService;
        readonly IScopeNode _scope;
        TextStyleState _defaultStyle;
        TextStyleState _activeStyle;
        bool _hasPendingOneShotStyleRestore;

        readonly TextCounterProcessor _counterProcessor = new();
        string _prevText = string.Empty;
        CancellationTokenSource? _counterCts;

        CancellationTokenSource? _presetCts;
        TypewriterEventCommandRuntimeConfig? _pendingTypewriterEventCommands;
        TypewriterEventCommandRuntimeContext _pendingTypewriterEventContext;
        TypewriterEventCommandDispatcher? _typewriterEventDispatcher;

        public TextChannelPlayer(
            string tag,
            TMP_Text target,
            bool preferTextAnimator,
            bool defaultUseTypewriter,
            bool defaultUseCounter,
            bool ensureAnimatorComponents,
            IScopeNode scope,
            TimeScaleBehavior timeScaleBehavior = TimeScaleBehavior.Scaled,
            IMaterialFxServiceFactory? materialFxFactory = null,
            IReadOnlyList<MaterialFxPresetEntry>? materialFxPresetEntries = null,
            SetTextSettings? defaultCounterSettings = null)
        {
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            if (!target) throw new ArgumentNullException(nameof(target));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            _defaultUseTypewriter = defaultUseTypewriter;
            _defaultUseCounter = defaultUseCounter;
            _defaultCounterSettings = defaultCounterSettings ?? SetTextSettings.Default;

            // preferTextAnimator が true なら TextAnimatorBackend を試し、ダメなら TMP に落ちる
            if (preferTextAnimator)
            {
                var ta = new TextAnimatorBackend(target, ensureAnimatorComponents);
                _backend = ta.SupportsRichAnimation ? ta : new TmpTextBackend(target);
            }
            else
            {
                _backend = new TmpTextBackend(target);
            }

            // LTS の TimeScaleBehavior を TextAnimator に適用
            _backend.SetTimeScaleBehavior(timeScaleBehavior);

            _defaultStyle = CaptureStyleState();
            _activeStyle = _defaultStyle;

            // MaterialFx 初期化
            if (materialFxFactory != null)
            {
                _materialFxService = materialFxFactory.CreateForTmpText(target);
                if (materialFxPresetEntries != null && materialFxPresetEntries.Count > 0)
                {
                    _materialFxService.ApplyPreset("default", materialFxPresetEntries);
                }
            }
        }

        sealed class TypewriterEventCommandDispatcher : IDisposable
        {
            readonly TypewriterComponent _typewriter;
            readonly TypewriterEventCommandRuntimeConfig _config;
            readonly TypewriterEventCommandRuntimeContext _runtimeContext;
            readonly TMP_Text _target;

            readonly UnityEngine.Events.UnityAction _onTypewriterStart;
            readonly UnityEngine.Events.UnityAction _onTextShowed;
            readonly UnityEngine.Events.UnityAction _onTextDisappeared;
            readonly UnityEngine.Events.UnityAction<CharacterData> _onCharacterVisible;
            readonly UnityEngine.Events.UnityAction<CharacterData, WaitMode> _onCharacterWaitStarted;
            readonly UnityEngine.Events.UnityAction<CharacterData, WaitMode> _onCharacterWaitFinished;
            readonly UnityEngine.Events.UnityAction<EventMarker> _onMessage;

            public TypewriterEventCommandDispatcher(
                TypewriterComponent typewriter,
                TypewriterEventCommandRuntimeConfig config,
                TypewriterEventCommandRuntimeContext runtimeContext,
                TMP_Text target)
            {
                _typewriter = typewriter;
                _config = config;
                _runtimeContext = runtimeContext;
                _target = target;

                _onTypewriterStart = () => Execute(TypewriterEventCommandHook.TypewriterStart);
                _onTextShowed = () => Execute(TypewriterEventCommandHook.TextShowed);
                _onTextDisappeared = () => Execute(TypewriterEventCommandHook.TextDisappeared);
                _onCharacterVisible = _ => Execute(TypewriterEventCommandHook.CharacterVisible);
                _onCharacterWaitStarted = (_, _) => Execute(TypewriterEventCommandHook.CharacterWaitStarted);
                _onCharacterWaitFinished = (_, _) => Execute(TypewriterEventCommandHook.CharacterWaitFinished);
                _onMessage = _ => Execute(TypewriterEventCommandHook.Message);

                _typewriter.onTypewriterStart.AddListener(_onTypewriterStart);
                _typewriter.onTextShowed.AddListener(_onTextShowed);
                _typewriter.onTextDisappeared.AddListener(_onTextDisappeared);
                _typewriter.onCharacterVisible.AddListener(_onCharacterVisible);
                _typewriter.onCharacterWaitStarted.AddListener(_onCharacterWaitStarted);
                _typewriter.onCharacterWaitFinished.AddListener(_onCharacterWaitFinished);
                _typewriter.onMessage.AddListener(_onMessage);
            }

            void Execute(TypewriterEventCommandHook hook)
            {
                var list = _config.Resolve(hook);
                if (list == null || list.Count == 0)
                    return;

                UniTask.Void(async () =>
                {
                    try
                    {
                        var commandContext = _runtimeContext.CreateCommandContext();
                        var result = await _runtimeContext.Runner.ExecuteListAsync(list, commandContext, CancellationToken.None, _runtimeContext.Options);
                        if (result.Status == CommandRunStatus.Error)
                            Debug.LogError($"[TextChannelPlayer] Typewriter event command failed. Hook={hook} Message={result.Message}", _target);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TextChannelPlayer] Typewriter event command exception. Hook={hook} Message={ex.Message}", _target);
                    }
                });
            }

            public void Dispose()
            {
                _typewriter.onTypewriterStart.RemoveListener(_onTypewriterStart);
                _typewriter.onTextShowed.RemoveListener(_onTextShowed);
                _typewriter.onTextDisappeared.RemoveListener(_onTextDisappeared);
                _typewriter.onCharacterVisible.RemoveListener(_onCharacterVisible);
                _typewriter.onCharacterWaitStarted.RemoveListener(_onCharacterWaitStarted);
                _typewriter.onCharacterWaitFinished.RemoveListener(_onCharacterWaitFinished);
                _typewriter.onMessage.RemoveListener(_onMessage);
            }
        }

        readonly SetTextSettings _defaultCounterSettings;
        public void SetText(string text, TextPlayMode mode = TextPlayMode.Instant)
        {
            SetText(text, mode, _defaultCounterSettings);
        }

        public void SetText(string text, TextPlayMode mode, in SetTextSettings settings)
        {
            var useTypewriter = mode == TextPlayMode.Typewriter && (_defaultUseTypewriter || SupportsTypewriter);
            if (!useTypewriter)
                ClearTypewriterEventCommands();

            if (mode == TextPlayMode.Typewriter && string.Equals(_prevText, text, StringComparison.Ordinal))
            {
                return;
            }

            if (mode != TextPlayMode.Count)
            {
                var currentText = _backend.Target.text;
                if (string.Equals(currentText, text, StringComparison.Ordinal))
                {
                    _prevText = text ?? string.Empty;
                    return;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (mode == TextPlayMode.Count)
            {
                //Debug.Log($"[TextChannelPlayer] SetText tag={Tag} mode={mode} text='{text}' prev='{_prevText}' useCounter={settings.UseCounter} duration={settings.CounterDurationSeconds}");
            }
#endif
            if (mode == TextPlayMode.Count && string.Equals(text, _prevText, StringComparison.Ordinal))
            {
                var backendText = _backend.Target.text;
                if (_counterProcessor.IsAnimating || string.Equals(backendText, text, StringComparison.Ordinal))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[TextChannelPlayer] Ignore Count SetText tag={Tag} target='{text}' (same as prev)");
#endif
                    return;
                }

                _backend.SetText(text, useTypewriter: false);
                return;
            }

            StopCounter();
            PrepareTypewriterEventCommands(useTypewriter);

            var effectiveSettings = settings;
            if (mode == TextPlayMode.Count)
                effectiveSettings.UseCounter = true;

            if (mode == TextPlayMode.Count && string.IsNullOrEmpty(_prevText))
            {
                var currentText = _backend.Target.text;
                if (!string.IsNullOrEmpty(currentText))
                {
                    _prevText = currentText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[TextChannelPlayer] Seed prevText from current Target.text='{_prevText}'");
#endif
                }
            }

            if (mode == TextPlayMode.Count && effectiveSettings.UseCounter)
            {
                var prevForLog = _prevText;
                if (_counterProcessor.Begin(_prevText, text, effectiveSettings))
                {
                    _prevText = text;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[TextChannelPlayer] Counter begin tag={Tag} prev='{prevForLog}' target='{text}' duration={effectiveSettings.CounterDurationSeconds}");
#endif
                    StartCounterAnimation(effectiveSettings.CounterUseUnscaledTime);
                    return;
                }
            }

            _prevText = text;
            _backend.SetText(text, useTypewriter: useTypewriter);
        }

        public void Append(string text, TextPlayMode mode = TextPlayMode.Instant)
        {
            var useTypewriter = mode == TextPlayMode.Typewriter && (_defaultUseTypewriter || SupportsTypewriter);
            PrepareTypewriterEventCommands(useTypewriter);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[TextChannelPlayer] Append tag={Tag} mode={mode} text={PreviewText(text)}");
#endif
            _backend.Append(text, useTypewriter: useTypewriter);
        }

        public void Clear()
        {
            ClearTypewriterEventCommands();
            StopCounter();
            _prevText = string.Empty;
            _backend.Clear();
        }

        public void Skip() => _backend.Skip();

        public void SkipCounter()
        {
            if (_counterProcessor.IsAnimating)
            {
                _counterProcessor.Complete();
                _backend.SetText(_counterProcessor.FinalText, useTypewriter: false);
            }
            StopCounter();
        }

        public void SetVisible(bool visible) => _backend.SetVisible(visible);

        public void SetTypewriterEventCommands(TypewriterEventCommandRuntimeConfig? config, in TypewriterEventCommandRuntimeContext runtimeContext)
        {
            _pendingTypewriterEventCommands = config;
            _pendingTypewriterEventContext = runtimeContext;
        }

        public void SetFontSize(float size)
        {
            if (size <= 0f)
                return;
            _backend.SetFontSize(size);
            _activeStyle.FontSize = size;
        }

        public void ApplyStyleCommand(in TextStyleCommandOptions options)
        {
            if (!options.Enabled)
            {
                if (_hasPendingOneShotStyleRestore)
                {
                    _hasPendingOneShotStyleRestore = false;
                    ApplyStyleState(_activeStyle);
                }
                return;
            }

            switch (options.Mode)
            {
                case TextStyleCommandMode.UseDefault:
                    _hasPendingOneShotStyleRestore = false;
                    ApplyStyleState(_defaultStyle);
                    return;

                case TextStyleCommandMode.SetActiveToDefault:
                    _hasPendingOneShotStyleRestore = false;
                    _activeStyle = _defaultStyle;
                    ApplyStyleState(_activeStyle);
                    return;

                case TextStyleCommandMode.OverrideThisTextOnly:
                    {
                        var style = BuildOverrideState(_activeStyle, options.Override);
                        ApplyStyleState(style);
                        _hasPendingOneShotStyleRestore = true;
                        return;
                    }

                case TextStyleCommandMode.OverrideAndSetActive:
                    {
                        _hasPendingOneShotStyleRestore = false;
                        var style = BuildOverrideState(_activeStyle, options.Override);
                        _activeStyle = style;
                        ApplyStyleState(_activeStyle);
                        return;
                    }

                case TextStyleCommandMode.UseActive:
                default:
                    _hasPendingOneShotStyleRestore = false;
                    ApplyStyleState(_activeStyle);
                    return;
            }
        }

        TextStyleState CaptureStyleState()
        {
            var t = _backend.Target;
            if (t == null)
                return default;

            return new TextStyleState
            {
                FontStyle = t.fontStyle,
                FontSize = t.fontSize,
                VertexColor = t.color,
                EnableColorGradient = t.enableVertexGradient,
                ColorGradientPreset = t.colorGradientPreset,
                CharacterSpacing = t.characterSpacing,
                WordSpacing = t.wordSpacing,
                LineSpacing = t.lineSpacing,
                ParagraphSpacing = t.paragraphSpacing,
                Alignment = t.alignment,
                TextWrappingMode = t.textWrappingMode,
                Overflow = t.overflowMode,
            };
        }

        static TextStyleState BuildOverrideState(in TextStyleState baseState, TextStyleOverrideSettings? settings)
        {
            if (settings == null)
                return baseState;

            var next = baseState;

            if (settings.ApplyFontStyle)
                next.FontStyle = settings.FontStyle;
            if (settings.ApplyFontSize)
                next.FontSize = settings.FontSize;
            if (settings.ApplyVertexColor)
                next.VertexColor = settings.VertexColor;
            if (settings.ApplyColorGradient)
            {
                next.EnableColorGradient = settings.EnableColorGradient;
                if (settings.ColorGradientPreset != null)
                    next.ColorGradientPreset = settings.ColorGradientPreset;
            }
            if (settings.ApplyCharacterSpacing)
                next.CharacterSpacing = settings.CharacterSpacing;
            if (settings.ApplyWordSpacing)
                next.WordSpacing = settings.WordSpacing;
            if (settings.ApplyLineSpacing)
                next.LineSpacing = settings.LineSpacing;
            if (settings.ApplyParagraphSpacing)
                next.ParagraphSpacing = settings.ParagraphSpacing;
            if (settings.ApplyAlignment)
                next.Alignment = settings.Alignment;
            if (settings.ApplyWordWrapping)
                next.TextWrappingMode = settings.EnableWordWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            if (settings.ApplyOverflow)
                next.Overflow = settings.Overflow;

            return next;
        }

        void ApplyStyleState(in TextStyleState state)
        {
            var t = _backend.Target;
            if (t == null)
                return;

            t.fontStyle = state.FontStyle;
            if (state.FontSize > 0f)
                t.fontSize = state.FontSize;
            t.color = state.VertexColor;
            t.enableVertexGradient = state.EnableColorGradient;
            if (state.ColorGradientPreset != null)
                t.colorGradientPreset = state.ColorGradientPreset;
            t.characterSpacing = state.CharacterSpacing;
            t.wordSpacing = state.WordSpacing;
            t.lineSpacing = state.LineSpacing;
            t.paragraphSpacing = state.ParagraphSpacing;
            t.alignment = state.Alignment;
            t.textWrappingMode = state.TextWrappingMode;
            t.overflowMode = state.Overflow;
        }

        public async UniTask WaitForTypewriterCompleteAsync(CancellationToken ct = default)
        {
            if (!_backend.SupportsTypewriter)
                return;

            while (_backend.IsTypewriterRunning)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        void StopCounter()
        {
            if (_counterCts != null)
            {
                try { _counterCts.Cancel(); }
                catch { /* ignore */ }
                _counterCts.Dispose();
                _counterCts = null;
            }
            _counterProcessor.Complete();
        }

        void StartCounterAnimation(bool useUnscaledTime)
        {
            if (_counterCts != null)
            {
                try { _counterCts.Cancel(); }
                catch { /* ignore */ }
                _counterCts.Dispose();
                _counterCts = null;
            }
            _counterCts = new CancellationTokenSource();
            RunCounterLoop(useUnscaledTime, _counterCts.Token).Forget();
        }

        async UniTaskVoid RunCounterLoop(bool useUnscaledTime, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _counterProcessor.IsAnimating)
                {
                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    if (_counterProcessor.Update(dt, out var currentText))
                    {
                        _backend.SetText(currentText, useTypewriter: false);
                    }
                    else
                    {
                        _backend.SetText(_counterProcessor.FinalText, useTypewriter: false);
                        break;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        public void StopPreset()
        {
            if (_presetCts != null)
            {
                try { _presetCts.Cancel(); }
                catch { }
                _presetCts.Dispose();
                _presetCts = null;
            }
        }

        public async UniTask PlayPresetAsync(ITextAnimationPreset preset, IVarStore variables, CancellationToken ct = default)
        {
            StopPreset();

            if (preset == null)
                return;

            var steps = preset.Steps;
            if (steps == null || steps.Count == 0)
                return;

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _presetCts = linkedCts;
            var token = linkedCts.Token;
            var ctx = new SimpleDynamicContext(variables ?? NullVarStore.Instance, _scope);

            try
            {
                if (preset.Loop && preset.LoopCount < 0)
                {
                    while (!token.IsCancellationRequested)
                    {
                        for (int i = 0; i < steps.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            await PlayStepAsync(steps[i], ctx, token);
                        }
                    }
                }
                else
                {
                    var count = !preset.Loop ? 1 : Mathf.Max(1, preset.LoopCount);
                    for (int k = 0; k < count && !token.IsCancellationRequested; k++)
                    {
                        for (int i = 0; i < steps.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            await PlayStepAsync(steps[i], ctx, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancel is expected.
            }
            finally
            {
                if (ReferenceEquals(_presetCts, linkedCts))
                {
                    _presetCts.Dispose();
                    _presetCts = null;
                }
            }
        }

        async UniTask PlayStepAsync(ITextAnimationStep step, IDynamicContext ctx, CancellationToken ct)
        {
            if (step == null)
                return;

            var delay = step.DelaySeconds.GetOrDefault(ctx, 0f);
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            switch (step.Action)
            {
                case TextChannelCommandAction.SetText:
                    if (step.ApplyText)
                        SetText(step.Text.Resolve(ctx), step.PlayMode);
                    break;
                case TextChannelCommandAction.Append:
                    if (step.ApplyText)
                        Append(step.Text.Resolve(ctx), step.PlayMode);
                    break;
                case TextChannelCommandAction.Clear:
                    if (step.ApplyText)
                        Clear();
                    break;
                case TextChannelCommandAction.Skip:
                    if (step.ApplyText)
                        Skip();
                    break;
                case TextChannelCommandAction.SetVisible:
                    SetVisible(step.Visible);
                    break;
            }
        }

        public void SetTimeScaleBehavior(TimeScaleBehavior behavior)
            => _backend.SetTimeScaleBehavior(behavior);

        public void Dispose()
        {
            ClearTypewriterEventCommands();
            StopPreset();
            if (_backend is IDisposable d)
                d.Dispose();

            _materialFxService?.Dispose();
        }

        void PrepareTypewriterEventCommands(bool useTypewriter)
        {
            ClearTypewriterEventCommands();

            if (!useTypewriter)
            {
                _pendingTypewriterEventCommands = null;
                return;
            }

            var config = _pendingTypewriterEventCommands;
            _pendingTypewriterEventCommands = null;
            if (config == null || !config.HasAnyCommands())
                return;

            var typewriter = _backend.Typewriter;
            if (typewriter == null)
                return;

            _typewriterEventDispatcher = new TypewriterEventCommandDispatcher(
                typewriter,
                config,
                _pendingTypewriterEventContext,
                _backend.Target);
        }

        void ClearTypewriterEventCommands()
        {
            if (_typewriterEventDispatcher == null)
                return;

            _typewriterEventDispatcher.Dispose();
            _typewriterEventDispatcher = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string PreviewText(string text)
        {
            if (text == null)
                return "<null>";

            var normalized = text.Replace("\r", string.Empty).Replace("\n", "\\n");
            const int MaxLen = 80;
            if (normalized.Length <= MaxLen)
                return $"\"{normalized}\"";

            return $"\"{normalized.Substring(0, MaxLen)}...\"";
        }
#endif
    }
}

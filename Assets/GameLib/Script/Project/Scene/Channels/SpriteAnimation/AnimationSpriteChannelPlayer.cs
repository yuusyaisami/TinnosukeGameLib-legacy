// Game.Channel.AnimationSpriteChannelPlayer.cs
// BaseShader-TransitionSystem-v1.0 準拠
// DOTween ベースの FlipX / CrossFade 実装

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using Game.Animation;
using VNext = Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using Game.MaterialFx;
using VContainer;
using Game.MaterialFx.Generated;

namespace Game.Channel
{
    /// <summary>
    /// 1チャネル分の Sprite/Image アニメーションプレイヤー。
    /// AnimationData をタグごとに再生する。
    /// </summary>
    /// <remarks>
    /// <para>FlipX 仕様 (TransitionSystem-v1.0):</para>
    /// <list type="bullet">
    ///   <item>FlipX は float 値（0.0 = 通常, 180.0 = 反転）</item>
    ///   <item>AdvancedFlip2D.EulerDeg.Y への Euler 角度として送信</item>
    ///   <item>DOTween で補間アニメーション</item>
    ///   <item>Stop() 呼出時は FlipX を即座にターゲット値へスナップ</item>
    /// </list>
    /// <para>CrossFade 仕様:</para>
    /// <list type="bullet">
    ///   <item>Transition System と連携して from → to を時間で遷移</item>
    ///   <item>fromSprite.texture を _ExtTexA にバインド</item>
    ///   <item>Progress を 0→1 にアニメーション</item>
    ///   <item>完了時に Transition.Enabled=0 に自動リセット</item>
    /// </list>
    /// </remarks>
    public interface IAnimationSpriteChannelPlayer
    {
        string Tag { get; }

        IAnimationData? CurrentClip { get; }

        /// <summary>
        /// The SpriteRenderer target if this channel uses SpriteRenderer, otherwise null.
        /// </summary>
        SpriteRenderer? SpriteRenderer { get; }

        /// <summary>
        /// The Image target if this channel uses uGUI Graphic, otherwise null.
        /// </summary>
        UnityEngine.UI.Image? Image { get; }

        /// <summary>
        /// The MaterialFx service associated with this player (may be null if not initialized).
        /// Use this to apply shader-driven effects instead of directly setting material properties.
        /// </summary>
        Game.MaterialFx.IMaterialFxService? MaterialFx { get; }
        float PlaybackSpeedMultiplier { get; }
        void SetPlaybackSpeedMultiplier(float value);
        bool UsePlaybackSpeedMultiplier { get; }
        void SetUsePlaybackSpeedMultiplier(bool value);

        void SetFlipX(bool flipX);
        void SetFlipXAngle(float eulerY);
        void TriggerFlipX();
        void SetSortingOrder(int sortingOrder);

        /// <summary>
        /// アニメーションを再生する。
        /// </summary>
        UniTask PlayAsync(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            bool flipX,
            CancellationToken ct);

        /// <summary>
        /// アニメーションを再生する (TransitionProfile 指定)。
        /// </summary>
        UniTask PlayAsync(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            bool flipX,
            ITransitionProfile? crossFadeProfile,
            CancellationToken ct);

        UniTask PlayPresetAsync(AnimationSpritePreset preset, CancellationToken ct);

        void Stop();
    }

    public sealed class AnimationSpriteChannelPlayer : IAnimationSpriteChannelPlayer, IMaterialFxReceiver, IDisposable
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Constants
        // ═══════════════════════════════════════════════════════════════════════════

        const string LogTag = "[AnimationSpriteChannelPlayer]";
        const string FlipLayerName = "AnimationSpriteChannelPlayer.FlipX";
        const string ClipSwitchFlipLayerName = "AnimationSpriteChannelPlayer.ClipSwitchFlip";
        const string TransitionLayerName = "AnimationSpriteChannelPlayer.Transition";
        const string BaseShaderPresetContextTag = "AnimationSprite.BaseShaderPreset";

        /// <summary>FlipX アニメーションのデフォルト duration（秒）</summary>
        const float DefaultFlipDuration = 0.15f;
        const float DefaultClipSwitchFlipDuration = 0.2f;
        const int FlipLayerPriority = 1000;
        const int ClipSwitchFlipLayerPriority = 1100;

        /// <summary>CrossFade のデフォルト duration（秒）</summary>
        const float DefaultCrossFadeDuration = 0.3f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Dependencies
        // ═══════════════════════════════════════════════════════════════════════════

        readonly AnimationSpriteChannelDef _def;
        readonly IScopeNode _scope;
        readonly VNext.ICommandRunner _commandRunner;
        bool _playedOnSpawn;
        readonly IMaterialFxService? _materialFxService;

        // ═══════════════════════════════════════════════════════════════════════════
        // Playback State
        // ═══════════════════════════════════════════════════════════════════════════

        enum PlaybackPhase
        {
            Idle,
            Starting,
            Playing,
            Ending
        }

        CancellationTokenSource? _sessionCts;
        readonly CancellationTokenSource _lifetimeCts = new();
        CancellationToken _playToken;
        UniTaskCompletionSource? _playTcs;

        readonly SpriteClipPlayer _clipPlayer = new();
        readonly HookRunner _hookRunner;

        IAnimationData? _currentClip;
        IAnimationData? _nextClip;
        bool _nextClipLoop;
        AnimationDataSource? _loopRandomSource;
        bool _loopRandomReselectOnCycle;
        bool _useClipSwitchFlip;
        float _clipSwitchFlipDuration = DefaultClipSwitchFlipDuration;
        bool _clipSwitchFlipFullRotation;
        bool _awaitFrameCommands;
        UniTask _frameCommandTask;

        PlaybackPhase _phase;
        UniTask _hookTask;

        Sprite? _lastAppliedSprite;

        // ═══════════════════════════════════════════════════════════════════════════
        // FlipX State
        // ═══════════════════════════════════════════════════════════════════════════

        readonly string _flipLayerContext;
        readonly FlipController _flipController;
        readonly string _clipSwitchFlipLayerContext;
        readonly FlipController _clipSwitchFlipController;
        bool _nativeFlipX;

        // ═══════════════════════════════════════════════════════════════════════════
        // Transition State
        // ═══════════════════════════════════════════════════════════════════════════

        readonly string _transitionLayerContext;
        readonly TransitionController _transitionController;

        // ═══════════════════════════════════════════════════════════════════════════
        // Public Properties
        // ═══════════════════════════════════════════════════════════════════════════

        public string Tag => _def.Tag;
        public IAnimationData? CurrentClip => _currentClip;

        public SpriteRenderer? SpriteRenderer => _def.SpriteRenderer;
        public UnityEngine.UI.Image? Image => _def.Image;
        public Game.MaterialFx.IMaterialFxService? MaterialFx => _materialFxService;
        public float PlaybackSpeedMultiplier => _clipPlayer.PlaybackSpeedMultiplier;
        public bool UsePlaybackSpeedMultiplier => _clipPlayer.UsePlaybackSpeedMultiplier;

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public AnimationSpriteChannelPlayer(
            AnimationSpriteChannelDef def,
            IScopeNode scope,
            VNext.ICommandRunner commandRunner,
            IMaterialFxServiceFactory? materialFxFactory)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrWhiteSpace(_def.Tag))
                throw new ArgumentException("AnimationSprite channel tag must be specified.", nameof(def));

            if (string.Equals(_def.Tag.Trim(), "default", StringComparison.Ordinal))
                throw new ArgumentException("AnimationSprite channel tag must be an explicit non-default value.", nameof(def));

            if (_def.SpriteRenderer == null && _def.Image == null)
                throw new InvalidOperationException($"AnimationSprite channel '{_def.Tag}' requires an explicit SpriteRenderer or Image target.");

            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

            _hookRunner = new HookRunner(_scope, _commandRunner);

            // MaterialFx 初期化
            if (materialFxFactory != null)
            {
                // SpriteRenderer 優先、なければ Image (uGUI) を試す
                if (_def.SpriteRenderer != null)
                {
                    _materialFxService = InitializeMaterialFx(_def, materialFxFactory);
                }
                else if (_def.Image != null)
                {
                    _materialFxService = materialFxFactory.CreateForGraphic(_def.Image);
                    if (_def.MaterialFxPresetEntries != null && _def.MaterialFxPresetEntries.Count > 0)
                    {
                        _materialFxService.ApplyPreset("default", ResolveMaterialFxEntries(_def.MaterialFxPresetEntries));
                    }
                    ApplyBaseShaderPreset(_materialFxService);
                }
            }
            else if (materialFxFactory == null)
            {
                Debug.LogWarning($"{LogTag} MaterialFxServiceFactory is null for channel '{def.Tag}'. MaterialFx features disabled.");
            }

            // FlipX 初期状態
            // per-instance flip layer context to avoid cross-instance conflicts
            var instanceId = 0;
            if (_def.SpriteRenderer != null)
            {
                instanceId = _def.SpriteRenderer.GetInstanceID();
            }
            else if (_def.Image != null)
            {
                instanceId = _def.Image.GetInstanceID();
            }

            _flipLayerContext = FlipLayerName + "." + _def.Tag + "." + instanceId;

            _flipController = new FlipController(_materialFxService, _flipLayerContext, FlipLayerPriority);
            _clipSwitchFlipLayerContext = ClipSwitchFlipLayerName + "." + _def.Tag + "." + instanceId;
            _clipSwitchFlipController = new FlipController(_materialFxService, _clipSwitchFlipLayerContext, ClipSwitchFlipLayerPriority);
            _nativeFlipX = GetNativeFlipX();

            // Transition 初期状態
            _transitionLayerContext = TransitionLayerName + "." + _def.Tag + "." + instanceId;
            _transitionController = new TransitionController(_materialFxService, _transitionLayerContext);
        }

        IMaterialFxService InitializeMaterialFx(AnimationSpriteChannelDef def, IMaterialFxServiceFactory factory)
        {
            var service = factory.CreateForSpriteRenderer(def.SpriteRenderer);

            if (def.MaterialFxPresetEntries != null && def.MaterialFxPresetEntries.Count > 0)
            {
                service.ApplyPreset("default", ResolveMaterialFxEntries(def.MaterialFxPresetEntries));
            }

            ApplyBaseShaderPreset(service);

            return service;
        }

        void ApplyBaseShaderPreset(IMaterialFxService service)
        {
            if (service == null)
                return;
            var basePreset = _def.BaseShaderPreset;
            if (basePreset == null)
                return;

            basePreset.RefreshEntries();
            service.ApplyPreset(BaseShaderPresetContextTag, ResolveMaterialFxEntries(basePreset.Entries));
        }

        IReadOnlyList<MaterialFxPresetEntry> ResolveMaterialFxEntries(IReadOnlyList<MaterialFxPresetEntry> entries)
        {
            var vars = _scope.Resolver != null && _scope.Resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null
                ? resolvedVars
                : NullVarStore.Instance;
            var context = new SimpleDynamicContext(vars, _scope);
            var resolved = new MaterialFxPresetEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                resolved[i] = entries[i].Resolve(context);
            return resolved;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Public API: PlayPresetAsync
        // ═══════════════════════════════════════════════════════════════════════════

        public UniTask PlayPresetAsync(AnimationSpritePreset preset, CancellationToken ct)
        {
            if (preset == null)
                return UniTask.CompletedTask;

            _loopRandomReselectOnCycle =
                preset.playMode == AnimationPlayMode.Loop &&
                preset.animationA != null &&
                (preset.animationA.kind == AnimationDataSourceKind.RandomInline ||
                 preset.animationA.kind == AnimationDataSourceKind.RandomAsset);
            _loopRandomSource = _loopRandomReselectOnCycle ? preset.animationA : null;
            _useClipSwitchFlip = preset.useClipSwitchFlip;
            _clipSwitchFlipDuration = Mathf.Max(0.01f, preset.switchFlipDuration);
            _clipSwitchFlipFullRotation = preset.switchFlipFullRotation;

            AnimationDataSource.TryGet(preset.animationA, out var clipA);
            AnimationDataSource.TryGet(preset.animationB, out var clipB);

            SetUsePlaybackSpeedMultiplier(preset.usePlaybackSpeedMultiplier);
            return PlayInternal(clipA, clipB, preset.playMode, preset.flipX, preset.crossFadeProfile, ct, keepLoopRandomConfig: true);
        }

        internal void TryPlayOnSpawn()
        {
            if (_playedOnSpawn)
                return;
            if (!_def.PlayOnSpawn)
                return;

            _playedOnSpawn = true;
            var preset = _def.SpritePreset;
            if (preset == null)
                return;

            PlayPresetAsync(preset, CancellationToken.None).Forget();
        }

        internal void ResetPlayOnSpawn()
        {
            _playedOnSpawn = false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Public API: PlayAsync
        // ═══════════════════════════════════════════════════════════════════════════

        public UniTask PlayAsync(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            bool flipX,
            CancellationToken ct)
        {
            // Profile なしオーバーロード → null で呼び出し
            return PlayInternal(clipA, clipB, mode, flipX, null, ct, keepLoopRandomConfig: false);
        }

        public UniTask PlayAsync(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            bool flipX,
            ITransitionProfile? crossFadeProfile,
            CancellationToken ct)
        {
            return PlayInternal(clipA, clipB, mode, flipX, crossFadeProfile, ct, keepLoopRandomConfig: false);
        }

        UniTask PlayInternal(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            bool flipX,
            ITransitionProfile? crossFadeProfile,
            CancellationToken ct,
            bool keepLoopRandomConfig)
        {
            if (!keepLoopRandomConfig)
            {
                _loopRandomSource = null;
                _loopRandomReselectOnCycle = false;
                _useClipSwitchFlip = false;
                _clipSwitchFlipDuration = DefaultClipSwitchFlipDuration;
                _clipSwitchFlipFullRotation = false;
            }

            // Flip-only preset support:
            // If no playable clip can start for the selected mode, still apply flip
            // and complete without touching current playback state.
            var hasPlayableA = SpriteClipPlayer.IsPlayable(clipA);
            var hasPlayableB = SpriteClipPlayer.IsPlayable(clipB);
            var canStartPlayback = mode switch
            {
                AnimationPlayMode.Once => hasPlayableA,
                AnimationPlayMode.Loop => hasPlayableA,
                AnimationPlayMode.OnceToLoop => hasPlayableA || hasPlayableB,
                AnimationPlayMode.CrossFade => hasPlayableA || hasPlayableB,
                _ => hasPlayableA,
            };
            if (!canStartPlayback)
            {
                ApplyFlipTarget(flipX, duration: 0f);
                return UniTask.CompletedTask;
            }

            // 既存の再生をキャンセル
            Stop();

            // Sprite / Image どちらも無いチャネルなら何もしない
            if (_def.SpriteRenderer == null && _def.Image == null)
                return UniTask.CompletedTask;

            // FlipX ターゲット設定（bool → float 変換）
            ApplyFlipTarget(flipX, DefaultFlipDuration);

            BeginNewSession(ct);
            _playTcs = new UniTaskCompletionSource();
            var playTask = _playTcs.Task;

            StartPlaybackWithOptionalClipSwitchFlip(clipA, clipB, mode, crossFadeProfile, flipX).Forget();
            return playTask;
        }

        void ApplyFlipTarget(bool flipX, float duration)
        {
            if (UseShaderFlipController())
            {
                _flipController?.SetTarget(flipX, duration, Ease.OutQuad);
            }
            else
            {
                ApplyNativeFlipX(flipX);
            }
        }

        async UniTaskVoid StartPlaybackWithOptionalClipSwitchFlip(
            IAnimationData? clipA,
            IAnimationData? clipB,
            AnimationPlayMode mode,
            ITransitionProfile? crossFadeProfile,
            bool targetFlipX)
        {
            var shouldPlaySwitchFlip =
                _useClipSwitchFlip &&
                UseShaderFlipController() &&
                mode != AnimationPlayMode.CrossFade &&
                SpriteClipPlayer.IsPlayable(clipA) &&
                _playToken.CanBeCanceled &&
                !_playToken.IsCancellationRequested &&
                _phase == PlaybackPhase.Idle;

            if (!shouldPlaySwitchFlip)
            {
                StartPlayback(clipA, clipB, mode, crossFadeProfile);
                return;
            }

            var baseAngle = targetFlipX ? 180f : 0f;
            var halfDuration = Mathf.Max(0.005f, _clipSwitchFlipDuration * 0.5f);
            var secondTarget = _clipSwitchFlipFullRotation ? baseAngle + 360f : baseAngle;
            var delayType = _materialFxService != null && _materialFxService.UseUnscaledTime
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            _clipSwitchFlipController?.SetTargetAngle(baseAngle + 90f, halfDuration, Ease.OutQuad);
            var canceledHalf = await UniTask.Delay(
                TimeSpan.FromSeconds(halfDuration),
                delayType,
                cancellationToken: _playToken).SuppressCancellationThrow();
            if (canceledHalf || _playToken.IsCancellationRequested)
                return;

            StartPlayback(clipA, clipB, mode, crossFadeProfile);
            _clipSwitchFlipController?.SetTargetAngle(secondTarget, halfDuration, Ease.InQuad);
            var canceledEnd = await UniTask.Delay(
                TimeSpan.FromSeconds(halfDuration),
                delayType,
                cancellationToken: _playToken).SuppressCancellationThrow();
            if (canceledEnd)
                return;

            _clipSwitchFlipController?.StopAndSnap(disableLayer: true);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Public API: Stop
        // ═══════════════════════════════════════════════════════════════════════════

        void BeginNewSession(CancellationToken external)
        {
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(external, _lifetimeCts.Token);
            _playToken = _sessionCts.Token;
        }

        public void SetFlipX(bool flipX)
        {
            if (UseShaderFlipController())
            {
                _flipController?.SetTarget(flipX, DefaultFlipDuration, Ease.OutQuad);
            }
            else
            {
                ApplyNativeFlipX(flipX);
            }
        }

        public void SetFlipXAngle(float eulerY)
        {
            if (UseShaderFlipController())
            {
                _flipController?.SetTargetAngle(eulerY, DefaultFlipDuration, Ease.OutQuad);
            }
            else
            {
                ApplyNativeFlipX(IsFlippedAngle(eulerY));
            }
        }

        public void TriggerFlipX()
        {
            if (UseShaderFlipController())
            {
                _flipController?.Trigger(DefaultFlipDuration, Ease.OutQuad);
            }
            else
            {
                ApplyNativeFlipX(!_nativeFlipX);
            }
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (_def.SpriteRenderer != null)
                _def.SpriteRenderer.sortingOrder = sortingOrder;
        }

        public void SetPlaybackSpeedMultiplier(float value)
        {
            _clipPlayer.SetPlaybackSpeedMultiplier(value);
        }

        public void SetUsePlaybackSpeedMultiplier(bool value)
        {
            _clipPlayer.SetUsePlaybackSpeedMultiplier(value);
        }

        public void Stop()
        {
            _sessionCts?.Cancel();

            // Stop は軽く冪等に。待機中の Play は即完了し、後始末(onCanceled)は lifetime で裏実行。
            CancelPlaybackAndReset();

            // 見た目の残留を止める（Tween Kill + スナップ/無効化）
            _flipController?.StopAndSnap(disableLayer: true);
            _clipSwitchFlipController?.StopAndSnap(disableLayer: true);
            _transitionController?.Stop();
        }

        internal void Tick(float deltaTime)
        {
            if (_phase == PlaybackPhase.Idle)
                return;

            if (_playToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            switch (_phase)
            {
                case PlaybackPhase.Starting:
                    if (!TryCompleteHookTask())
                        return;

                    if (_phase != PlaybackPhase.Starting)
                        return; // Hook 内で遷移済み（Stop/Fail 等）

                    EnterPlaying();
                    break;

                case PlaybackPhase.Playing:
                    TickFrames(deltaTime);
                    break;

                case PlaybackPhase.Ending:
                    if (!TryCompleteHookTask())
                        return;

                    if (_phase != PlaybackPhase.Ending)
                        return;

                    AfterEndHook();
                    break;
            }
        }
        // ═══════════════════════════════════════════════════════════════════════════
        // Internal Playback: CrossFade (Transition System via Profile)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// CrossFade 再生。profile が null の場合はデフォルト設定を使用。
        /// </summary>
        void StartCrossFade(IAnimationData? toClip, ITransitionProfile? profile)
        {
            if (toClip == null)
            {
                CompletePlaybackSuccess();
                return;
            }

            // MaterialFx 無しなら即切替
            if (_materialFxService == null)
            {
                StartClip(toClip, loop: true, nextClip: null, nextClipLoop: false);
                return;
            }

            // from = 現在表示中のスプライト
            Sprite? fromSprite = GetCurrentSprite();

            if (fromSprite != null)
            {
                _transitionController?.Start(fromSprite, profile, DefaultCrossFadeDuration, Ease.OutQuad);

                // to 再生を開始（loop 継続）
                StartClip(toClip, loop: true, nextClip: null, nextClipLoop: false);
            }
            else
            {
                // fromSprite が無い場合は直接再生
                StartClip(toClip, loop: true, nextClip: null, nextClipLoop: false);
            }
        }

        Sprite? GetCurrentSprite()
        {
            if (_def.SpriteRenderer != null)
                return _def.SpriteRenderer.sprite;
            if (_def.Image != null)
                return _def.Image.sprite;
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Internal Playback: Core Tick State Machine
        // ═══════════════════════════════════════════════════════════════════════════

        void StartPlayback(IAnimationData? clipA, IAnimationData? clipB, AnimationPlayMode mode, ITransitionProfile? crossFadeProfile)
        {
            _hookTask = default;
            _nextClip = null;
            _nextClipLoop = false;
            _lastAppliedSprite = null;
            _phase = PlaybackPhase.Idle;
            _clipPlayer.Reset();

            switch (mode)
            {
                case AnimationPlayMode.Loop:
                    StartClip(clipA, loop: !_loopRandomReselectOnCycle, nextClip: null, nextClipLoop: false);
                    break;

                case AnimationPlayMode.OnceToLoop:
                    if (!SpriteClipPlayer.IsPlayable(clipA))
                    {
                        StartClip(clipB, loop: true, nextClip: null, nextClipLoop: false);
                    }
                    else
                    {
                        StartClip(clipA, loop: false, nextClip: clipB, nextClipLoop: true);
                    }
                    break;

                case AnimationPlayMode.CrossFade:
                    StartCrossFade(clipA ?? clipB, crossFadeProfile);
                    break;

                case AnimationPlayMode.Once:
                default:
                    StartClip(clipA, loop: false, nextClip: null, nextClipLoop: false);
                    break;
            }

            if (_playToken.IsCancellationRequested && _phase != PlaybackPhase.Idle)
            {
                Stop();
            }
        }

        void StartClip(IAnimationData? clip, bool loop, IAnimationData? nextClip, bool nextClipLoop)
        {
            if (!SpriteClipPlayer.IsPlayable(clip))
            {
                if (SpriteClipPlayer.IsPlayable(nextClip))
                {
                    if (_playToken.IsCancellationRequested)
                    {
                        CompletePlaybackSuccess();
                        return;
                    }
                    StartClip(nextClip, loop: nextClipLoop, nextClip: null, nextClipLoop: false);
                    return;
                }

                CompletePlaybackSuccess();
                return;
            }

            if (clip == null)
            {
                CompletePlaybackSuccess();
                return;
            }

            _currentClip = clip;
            _clipPlayer.Start(clip, loop);
            _nextClip = nextClip;
            _nextClipLoop = nextClipLoop;
            _awaitFrameCommands = clip.AwaitFrameCommands;
            _frameCommandTask = UniTask.CompletedTask;

            _phase = PlaybackPhase.Starting;

            var onStart = clip.OnStart;
            if (onStart == null || onStart.Count == 0)
            {
                EnterPlaying();
                return;
            }

            _hookTask = _hookRunner.RunAsync(onStart, _playToken);
        }

        void CancelPlaybackAndReset()
        {
            if (_phase == PlaybackPhase.Idle)
                return;

            var tcs = _playTcs;
            var onCanceled = _currentClip?.OnCanceled;

            // まず待機中の Play を即座に完了させる
            tcs?.TrySetResult();

            // Tick/状態を即座に落とす（Stop を軽く冪等にする）
            ResetPlaybackState();

            // onCanceled は「演出/後始末」なので lifetime token で裏実行（次の Play で殺されない）
            if (onCanceled != null && onCanceled.Count > 0)
            {
                _hookRunner.RunFireAndForget(onCanceled, _lifetimeCts.Token);
            }
        }

        bool TryCompleteHookTask()
        {
            if (_hookTask.Status == UniTaskStatus.Pending)
                return false;

            try
            {
                _hookTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Stop();
                return false;
            }
            catch (Exception e)
            {
                FailPlayback(e);
                return false;
            }

            _hookTask = default;
            return true;
        }

        void EnterPlaying()
        {
            if (!_clipPlayer.IsActive)
            {
                BeginEnd();
                return;
            }

            _phase = PlaybackPhase.Playing;

            if (_clipPlayer.TryGetCurrentFrame(out var frame))
            {
                if (frame != null)
                {
                    ApplyFrame(frame);
                    RunFrameCommands(frame);
                }
            }
        }

        void TickFrames(float deltaTime)
        {
            if (!_clipPlayer.IsActive)
            {
                BeginEnd();
                return;
            }

            if (!TryCompleteFrameCommandTask())
                return;

            if (_playToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            _clipPlayer.Tick(deltaTime, out var frameChanged, out var changedFrame, out var ended);
            if (ended)
            {
                BeginEnd();
                return;
            }

            if (frameChanged && changedFrame != null)
            {
                ApplyFrame(changedFrame);
                RunFrameCommands(changedFrame);
            }
        }

        void BeginEnd()
        {
            if (_currentClip == null)
            {
                CompletePlaybackSuccess();
                return;
            }

            _phase = PlaybackPhase.Ending;

            var onEnd = _currentClip.OnEnd;
            if (onEnd == null || onEnd.Count == 0)
            {
                _hookTask = UniTask.CompletedTask;
                return;
            }

            _hookTask = _hookRunner.RunAsync(onEnd, _playToken);
        }

        void AfterEndHook()
        {
            if (SpriteClipPlayer.IsPlayable(_nextClip))
            {
                // PlayOnceToLoop: clipA が正常終了した後に cancellation が来た場合は onCanceled しない。
                if (_playToken.IsCancellationRequested)
                {
                    CompletePlaybackSuccess();
                    return;
                }

                var next = _nextClip;
                var nextLoop = _nextClipLoop;
                _nextClip = null;
                _nextClipLoop = false;

                StartClip(next, loop: nextLoop, nextClip: null, nextClipLoop: false);
                return;
            }

            if (TryStartRandomLoopCycle())
                return;

            CompletePlaybackSuccess();
        }

        bool TryStartRandomLoopCycle()
        {
            if (!_loopRandomReselectOnCycle || _loopRandomSource == null)
                return false;

            if (_playToken.IsCancellationRequested)
            {
                CompletePlaybackSuccess();
                return true;
            }

            if (!AnimationDataSource.TryGet(_loopRandomSource, out var nextRandomClip) || !SpriteClipPlayer.IsPlayable(nextRandomClip))
            {
                CompletePlaybackSuccess();
                return true;
            }

            StartClip(nextRandomClip, loop: false, nextClip: null, nextClipLoop: false);
            return true;
        }

        void CompletePlaybackSuccess()
        {
            _playTcs?.TrySetResult();
            ResetPlaybackState();
        }

        void FailPlayback(Exception ex)
        {
            _playTcs?.TrySetException(ex);
            ResetPlaybackState();
        }

        void ResetPlaybackState()
        {
            _phase = PlaybackPhase.Idle;
            _hookTask = default;

            _clipPlayer.Reset();

            _nextClip = null;
            _nextClipLoop = false;
            _loopRandomSource = null;
            _loopRandomReselectOnCycle = false;
            _awaitFrameCommands = false;
            _frameCommandTask = default;

            _currentClip = null;
            _lastAppliedSprite = null;

            _playTcs = null;
            _playToken = default;

            if (_sessionCts != null)
            {
                _sessionCts.Dispose();
                _sessionCts = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Frame Application
        // ═══════════════════════════════════════════════════════════════════════════

        void ApplyFrame(IAnimationFrame frame)
        {
            if (frame == null)
                return;

            var sprite = frame.Sprite;
            if (ReferenceEquals(sprite, _lastAppliedSprite))
                return;

            _lastAppliedSprite = sprite;

            if (_def.SpriteRenderer != null)
            {
                _def.SpriteRenderer.sprite = sprite;
                if (_materialFxService is IMaterialFxSpriteSync spriteSync)
                {
                    spriteSync.NotifySpriteChanged(sprite);
                }
            }
            else if (_def.Image != null)
            {
                _def.Image.sprite = sprite;
                // Image チャネルでも MaterialFx があれば同期通知を行う（即時 Apply を期待）
                try
                {
                    if (_materialFxService is IMaterialFxSpriteSync spriteSync)
                    {
                        spriteSync.NotifySpriteChanged(sprite);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Dispose()
        {
            Stop();

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            _materialFxService?.Dispose();

            _sessionCts?.Dispose();
            _sessionCts = null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Command Execution
        // ═══════════════════════════════════════════════════════════════════════════

        void RunFrameCommandsFireAndForget(IAnimationFrame frame)
        {
            if (_commandRunner == null || frame == null)
                return;

            var commands = frame.Commands;
            if (commands == null || commands.Count == 0)
                return;

            _hookRunner.RunFireAndForget(commands, _playToken, suppressCancelLog: true);
        }

        void RunFrameCommands(IAnimationFrame frame)
        {
            if (!_awaitFrameCommands)
            {
                RunFrameCommandsFireAndForget(frame);
                return;
            }

            if (_commandRunner == null || frame == null)
                return;

            var commands = frame.Commands;
            if (commands == null || commands.Count == 0)
                return;

            _frameCommandTask = _hookRunner.RunAsync(commands, _playToken, suppressCancelLog: true);
        }

        bool TryCompleteFrameCommandTask()
        {
            if (!_awaitFrameCommands)
                return true;

            if (_frameCommandTask.Status == UniTaskStatus.Pending)
                return false;

            try
            {
                _frameCommandTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Stop();
                return false;
            }
            catch (Exception ex)
            {
                FailPlayback(ex);
                return false;
            }

            _frameCommandTask = UniTask.CompletedTask;
            return true;
        }

        static bool IsFlippedAngle(float eulerY)
        {
            return Mathf.Abs(Mathf.DeltaAngle(eulerY, 180f)) <= 90f;
        }

        bool UseShaderFlipController()
        {
            return _materialFxService != null;
        }

        bool GetNativeFlipX()
        {
            if (_def.SpriteRenderer != null)
                return _def.SpriteRenderer.flipX;

            if (_def.Image != null)
                return _def.Image.rectTransform.localScale.x < 0f;

            return false;
        }

        void ApplyNativeFlipX(bool flipX)
        {
            _nativeFlipX = flipX;

            if (_def.SpriteRenderer != null)
            {
                _def.SpriteRenderer.flipX = flipX;

                if (_materialFxService is IMaterialFxSpriteSync spriteSync)
                {
                    spriteSync.NotifyFlipChanged(flipX, _def.SpriteRenderer.flipY);
                }
                return;
            }

            if (_def.Image == null)
                return;

            var rect = _def.Image.rectTransform;
            var scale = rect.localScale;
            var absX = Mathf.Abs(scale.x);
            scale.x = flipX ? -absX : absX;
            rect.localScale = scale;
        }
    }
}

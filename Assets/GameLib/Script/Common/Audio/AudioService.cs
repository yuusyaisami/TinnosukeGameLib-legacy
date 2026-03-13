using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Game;
using Game.Times;
using Game.Commands.VNext;

namespace Game.Audio
{
    // ================================================================
    // AudioService - オーディオ再生サービス
    // ================================================================
    //
    // ## 概要
    //
    // ゲーム内のすべてのサウンド再生を管理する。
    // 公開 API は PlaySound のみ。Stop/FadeOut も PlaySound 経由で実現。
    //
    // ## 機能
    //
    // - バス別 AudioSource プール
    // - フェードイン/アウト
    // - TimeScale によるピッチ影響
    // - タグによる BGM 管理
    //
    // ================================================================

    /// <summary>
    /// オーディオ再生サービスのインターフェース。
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// サウンドを再生する。
        /// cue=null の場合は request で指定した対象を Stop/FadeOut する。
        /// </summary>
        void PlaySound(IAudioCue cue, SoundRequest request = default);
    }

    /// <summary>
    /// オーディオ再生サービスの実装。
    /// </summary>
    public sealed class AudioService : IAudioService, ITickable, IDisposable, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ----------------------------------------------------------------
        // 内部クラス
        // ----------------------------------------------------------------

        sealed class ActiveSound
        {
            public IAudioCue Cue;
            public AudioSource Source;
            public Transform Follow;
            public Vector3 Offset;
            public string Tag;

            public float LocalVolumeScale;
            public float BasePitch;
            public float BasePlaybackSpeed;
            public bool IsLocalPlayback;
            public float MinDistance;
            public float MaxDistance;
            public AudioRolloffMode RolloffMode;

            // フェード
            public float Fade;
            public float FadeFrom;
            public float FadeTo;
            public float FadeTime;
            public float FadeDuration;
            public bool IsFading;

            public bool AffectedByTimeScale;
            public bool AffectedPlaybackSpeedByTimeScale;
            public bool ApplyPlaybackSpeedToPitch;
            public float PitchInfluence;

            public bool WasPlayingBeforeSuspend;
            public bool HasStarted;
            public int NotPlayingFrames;
            public bool LoggedNotPlaying;
            public bool LoggedLoadWait;
            public bool LoggedLoadFailed;
            public bool LoggedLoopRetry;
        }

        sealed class BusMixer
        {
            public readonly AudioBusConfig Config;
            public readonly List<AudioSource> Pool = new();
            public readonly List<ActiveSound> Active = new();

            public BusMixer(AudioBusConfig config) => Config = config;
        }

        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        readonly IAudioVolumeProvider _volumes;
        ITimeService _timeService;
        IScopeNode _listenerScope;
        ActorSource _listenerTarget;
        ActorSourceResolveCache _listenerTargetCache;
        AudioListener _cachedSceneListener;
        bool _useCustomListenerTarget;
        bool _use2DDistanceAttenuation = true;

        GameObject _root;
        readonly Dictionary<AudioBusKind, BusMixer> _buses = new();
        bool _isAppSuspended;
        bool _isFocused = true;
        bool _isPaused;
        bool _appEventsRegistered;
        AppEventProxy _appEventProxy;

        const float MaxPitch = 3f;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public AudioService(IAudioVolumeProvider volumes)
        {
            _volumes = volumes ?? new ConstantAudioVolumeProvider();
        }

        public void BindTimeService(ITimeService timeService)
        {
            _timeService = timeService;
        }

        // ----------------------------------------------------------------
        // IScopeAcquireHandler / IScopeReleaseHandler / IDisposable
        // ----------------------------------------------------------------

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            RegisterAppEvents();
        }

        void EnsureRoot()
        {
            if (_root == null)
            {
                _root = new GameObject("[AudioService]");
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }
        }

        public void Dispose()
        {
            UnregisterAppEvents();
            _cachedSceneListener = null;
            _listenerTargetCache = default;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            _buses.Clear();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnregisterAppEvents();
            _isAppSuspended = false;
            _isFocused = true;
            _isPaused = false;
            _cachedSceneListener = null;
            _listenerTargetCache = default;
            ClearSuspendFlags();
        }

        // ----------------------------------------------------------------
        // 設定
        // ----------------------------------------------------------------

        /// <summary>
        /// バス設定を適用する。
        /// </summary>
        public void ConfigureBuses(IEnumerable<AudioBusConfig> configs)
        {
            _buses.Clear();

            if (configs == null)
                configs = Array.Empty<AudioBusConfig>();

            foreach (var cfg in configs)
            {
                var mixer = new BusMixer(cfg);
                _buses[cfg.Bus] = mixer;
                EnsurePool(mixer, Mathf.Max(0, cfg.InitialPoolSize));
            }

            // 設定漏れ対策：未設定のバスはデフォルトで生成
            foreach (AudioBusKind bus in Enum.GetValues(typeof(AudioBusKind)))
            {
                if (_buses.ContainsKey(bus)) continue;
                var mixer = new BusMixer(AudioBusConfig.Default(bus));
                _buses[bus] = mixer;
                EnsurePool(mixer, mixer.Config.InitialPoolSize);
            }
        }

        public void ConfigureListenerTarget(
            IScopeNode scope,
            bool useCustomListenerTarget,
            ActorSource customListenerTarget,
            bool use2DDistanceAttenuation)
        {
            _listenerScope = scope;
            _useCustomListenerTarget = useCustomListenerTarget;
            _listenerTarget = customListenerTarget;
            _use2DDistanceAttenuation = use2DDistanceAttenuation;
            _listenerTargetCache = default;
            _cachedSceneListener = null;
        }

        // ----------------------------------------------------------------
        // 公開 API
        // ----------------------------------------------------------------

        public void PlaySound(IAudioCue cue, SoundRequest request = default)
        {
            var bus = request.BusOverride ?? cue?.Bus ?? AudioBusKind.Sfx;

            if (!_buses.TryGetValue(bus, out var mixer))
            {
                mixer = new BusMixer(AudioBusConfig.Default(bus));
                _buses.Add(bus, mixer);
                EnsurePool(mixer, mixer.Config.InitialPoolSize);
            }

            // cue==null -> Stop/FadeOut
            if (cue == null)
            {
                LogDebug($"Stop request. Bus={bus} Tag={request.Tag ?? "null"} FadeOut={request.FadeOutSeconds:0.###}");
                StopByRequest(mixer, request);
                return;
            }

            var clip = cue.PickClip();
            if (clip == null) return;

            var tag = !string.IsNullOrEmpty(request.Tag) ? request.Tag : cue.DefaultTag;
            var playbackStartOffset = Mathf.Max(0f, request.PlaybackStartOffsetSeconds);
            if (!ValidatePlaybackStartOffset(cue, clip, playbackStartOffset, tag))
                return;

            LogDebug($"PlaySound. {DescribeCue(cue)} Tag={tag ?? "null"} Clip={clip.name} Bus={bus} SingleByTag={mixer.Config.SingleInstanceByTag}");

            // 多重再生禁止チェック
            if (!cue.AllowMultipleInstances)
            {
                for (int i = 0; i < mixer.Active.Count; i++)
                {
                    var a = mixer.Active[i];
                    if (a.Source == null) continue;
                    if (!a.Source.isPlaying) continue;
                    if (!ReferenceEquals(a.Cue, cue)) continue;
                    LogDebug($"Skip: already playing same cue. {DescribeActive(a)}");
                    return; // 既に再生中
                }
            }

            // タグ単一制御（BGM等）
            if (mixer.Config.SingleInstanceByTag && !string.IsNullOrEmpty(tag))
            {
                var existing = FindByTag(mixer, tag);
                if (existing != null)
                {
                    // 同一クリップで restart 禁止なら更新のみ
                    if (existing.Source != null &&
                        existing.Source.isPlaying &&
                        existing.Source.clip == clip &&
                        !cue.RestartIfAlreadyPlaying)
                    {
                        if (request.IgnoreIfAlreadyPlaying)
                        {
                            LogDebug($"Skip: tag already playing (ignore). {DescribeActive(existing)}");
                            return;
                        }

                        // パラメータ更新
                        existing.Cue = cue;
                        existing.Follow = request.FollowTarget;
                        existing.Offset = request.LocalOffset;
                        existing.LocalVolumeScale = cue.VolumeMultiplier * request.GetVolumeScale();
                        existing.BasePitch = Mathf.Max(0f, cue.BasePitch) * PickPitchRandom(cue);
                        existing.BasePlaybackSpeed = ResolvePlaybackSpeed(cue, request);
                        existing.IsLocalPlayback = request.IsLocalPlayback;
                        existing.MinDistance = Mathf.Max(0.01f, cue.MinDistance);
                        existing.MaxDistance = Mathf.Max(existing.MinDistance, cue.MaxDistance);
                        existing.RolloffMode = cue.RolloffMode;
                        existing.ApplyPlaybackSpeedToPitch = ResolveApplyPlaybackSpeedToPitch(cue, request);
                        existing.AffectedPlaybackSpeedByTimeScale = ResolveAffectedPlaybackSpeedByTimeScale(mixer, cue, request);
                        existing.AffectedByTimeScale = ResolveAffectedPitchByTimeScale(mixer, cue, request);
                        existing.PitchInfluence = ResolvePitchInfluence(mixer, cue, request);
                        ConfigureSourceForCue(existing.Source, cue, request);
                        LogDebug($"Update: tag already playing, parameters updated. {DescribeActive(existing)}");
                        ApplyVolumeAndPitch(mixer, existing);
                        return;
                    }

                    // 上書き：既存をフェードアウト
                    LogDebug($"Replace: tag already playing, fade out existing. {DescribeActive(existing)}");
                    StartFadeOut(existing, ResolveFadeOut(mixer, request));
                }
            }

            // 新規再生
            var src = GetAvailableSource(mixer);
            ConfigureSourceForCue(src, cue, request);

            src.clip = clip;
            src.loop = cue.Loop;

            var active = new ActiveSound
            {
                Cue = cue,
                Source = src,
                Follow = request.FollowTarget,
                Offset = request.LocalOffset,
                Tag = tag,

                LocalVolumeScale = cue.VolumeMultiplier * request.GetVolumeScale(),
                BasePitch = Mathf.Max(0f, cue.BasePitch) * PickPitchRandom(cue),
                BasePlaybackSpeed = ResolvePlaybackSpeed(cue, request),
                IsLocalPlayback = request.IsLocalPlayback,
                MinDistance = Mathf.Max(0.01f, cue.MinDistance),
                MaxDistance = Mathf.Max(Mathf.Max(0.01f, cue.MinDistance), cue.MaxDistance),
                RolloffMode = cue.RolloffMode,

                Fade = 1f,
                IsFading = false,

                ApplyPlaybackSpeedToPitch = ResolveApplyPlaybackSpeedToPitch(cue, request),
                AffectedPlaybackSpeedByTimeScale = ResolveAffectedPlaybackSpeedByTimeScale(mixer, cue, request),
                AffectedByTimeScale = ResolveAffectedPitchByTimeScale(mixer, cue, request),
                PitchInfluence = ResolvePitchInfluence(mixer, cue, request),
            };

            // フェードイン
            var fadeIn = Mathf.Max(0f, request.FadeInSeconds);
            if (fadeIn > 0f)
            {
                active.Fade = 0f;
                StartFade(active, from: 0f, to: 1f, duration: fadeIn);
            }

            ApplyVolumeAndPitch(mixer, active);

            if (playbackStartOffset > 0f)
                src.time = playbackStartOffset;

            src.Play();
            mixer.Active.Add(active);

            if (_isAppSuspended)
            {
                active.WasPlayingBeforeSuspend = true;
                src.Pause();
            }
        }

        // ----------------------------------------------------------------
        // ITickable
        // ----------------------------------------------------------------

        public void Tick()
        {
            if (_buses.Count == 0) return;
            if (_isAppSuspended) return;

            float udt = UnityEngine.Time.unscaledDeltaTime;

            foreach (var kv in _buses)
            {
                var mixer = kv.Value;

                for (int i = mixer.Active.Count - 1; i >= 0; --i)
                {
                    var a = mixer.Active[i];

                    if (a.Source == null)
                    {
                        mixer.Active.RemoveAt(i);
                        continue;
                    }

                    // 追従
                    if (a.Follow)
                    {
                        a.Source.transform.position = a.Follow.position + a.Offset;
                    }

                    // フェード更新（unscaled）
                    if (a.IsFading)
                    {
                        a.FadeTime += udt;
                        float t = (a.FadeDuration <= 0f) ? 1f : Mathf.Clamp01(a.FadeTime / a.FadeDuration);
                        a.Fade = Mathf.Lerp(a.FadeFrom, a.FadeTo, t);
                        if (t >= 1f)
                        {
                            a.IsFading = false;
                            a.Fade = a.FadeTo;
                        }
                    }

                    ApplyVolumeAndPitch(mixer, a);

                    if (a.Source.isPlaying)
                    {
                        a.HasStarted = true;
                        a.NotPlayingFrames = 0;
                    }
                    else
                    {
                        var clip = a.Source.clip;
                        if (clip != null)
                        {
                            if (clip.loadState == AudioDataLoadState.Unloaded)
                                clip.LoadAudioData();

                            if (clip.loadState == AudioDataLoadState.Loading || clip.loadState == AudioDataLoadState.Unloaded)
                            {
                                if (!a.LoggedLoadWait)
                                {
                                    a.LoggedLoadWait = true;
                                    LogDebug($"Waiting for clip load. {DescribeActive(a)} LoadState={clip.loadState}");
                                }
                                a.NotPlayingFrames = 0;
                                continue;
                            }

                            if (clip.loadState == AudioDataLoadState.Failed)
                            {
                                if (!a.LoggedLoadFailed)
                                {
                                    a.LoggedLoadFailed = true;
                                    LogDebug($"Clip load failed -> drop. {DescribeActive(a)}");
                                }
                                ResetSource(a.Source);
                                mixer.Active.RemoveAt(i);
                                continue;
                            }
                        }

                        a.NotPlayingFrames++;

                        if (!a.HasStarted)
                        {
                            if (a.NotPlayingFrames <= 2)
                                continue;
                            if (!a.LoggedNotPlaying)
                            {
                                a.LoggedNotPlaying = true;
                                LogDebug($"Not playing after start window -> drop. {DescribeActive(a)} Frames={a.NotPlayingFrames}");
                            }
                        }
                        else if (a.Source.loop && !a.IsFading && a.Fade > 0f)
                        {
                            if (a.NotPlayingFrames == 1 || a.NotPlayingFrames % 60 == 0)
                            {
                                a.Source.Play();
                                if (!a.LoggedLoopRetry)
                                {
                                    a.LoggedLoopRetry = true;
                                    LogDebug($"Loop stopped -> retry Play. {DescribeActive(a)} Frames={a.NotPlayingFrames}");
                                }
                            }
                            continue;
                        }

                        ResetSource(a.Source);
                        mixer.Active.RemoveAt(i);
                        continue;
                    }

                    // フェードアウト完了 -> 強制停止
                    if (!a.IsFading && a.Fade <= 0f)
                    {
                        a.Source.Stop();
                        ResetSource(a.Source);
                        mixer.Active.RemoveAt(i);
                    }
                }
            }
        }

        // ----------------------------------------------------------------
        // 内部メソッド
        // ----------------------------------------------------------------

        void LogDebug(string message)
        {
            //Debug.Log($"[AudioService] {message}");
        }

        static string DescribeActive(ActiveSound a)
        {
            if (a == null) return "ActiveSound=null";
            var cueName = a.Cue is UnityEngine.Object obj ? obj.name : a.Cue?.ToString();
            var clipName = a.Source != null && a.Source.clip != null ? a.Source.clip.name : "null";
            return $"Cue={cueName ?? "null"} Tag={a.Tag ?? "null"} Clip={clipName} Loop={a.Source?.loop ?? false} Fade={a.Fade:0.00}";
        }

        static string DescribeCue(IAudioCue cue)
        {
            if (cue == null) return "Cue=null";
            var cueName = cue is UnityEngine.Object obj ? obj.name : cue.ToString();
            return $"Cue={cueName ?? "null"} Bus={cue.Bus} Loop={cue.Loop} AllowMulti={cue.AllowMultipleInstances}";
        }

        void StopByRequest(BusMixer mixer, SoundRequest request)
        {
            var tag = request.Tag;
            float fadeOut = ResolveFadeOut(mixer, request);

            if (string.IsNullOrEmpty(tag))
            {
                // バス全体を停止
                for (int i = mixer.Active.Count - 1; i >= 0; --i)
                {
                    StartFadeOut(mixer.Active[i], fadeOut);
                }
                return;
            }

            var a = FindByTag(mixer, tag);
            if (a != null)
            {
                StartFadeOut(a, fadeOut);
            }
        }

        ActiveSound FindByTag(BusMixer mixer, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            for (int i = 0; i < mixer.Active.Count; i++)
            {
                var a = mixer.Active[i];
                if (a?.Source == null) continue;
                if (a.Tag == tag)
                    return a;
            }
            return null;
        }

        float ResolveFadeOut(BusMixer mixer, SoundRequest request)
        {
            if (request.FadeOutSeconds > 0f)
                return request.FadeOutSeconds;
            return Mathf.Max(0f, mixer.Config.DefaultFadeOutSeconds);
        }

        void StartFadeOut(ActiveSound a, float duration)
        {
            if (a == null) return;
            StartFade(a, a.Fade, 0f, Mathf.Max(0f, duration));
        }

        void StartFade(ActiveSound a, float from, float to, float duration)
        {
            a.IsFading = true;
            a.FadeFrom = Mathf.Clamp01(from);
            a.FadeTo = Mathf.Clamp01(to);
            a.FadeDuration = duration;
            a.FadeTime = 0f;
        }

        void ApplyVolumeAndPitch(BusMixer mixer, ActiveSound a)
        {
            if (a.Source == null) return;

            // ボリューム計算
            float master = Mathf.Clamp01(_volumes.GetMaster());
            float busVol = Mathf.Clamp01(_volumes.GetBus(mixer.Config.Bus));
            float baseVol = Mathf.Clamp01(master * busVol);
            float distanceAttenuation = ResolveDistanceAttenuation(a);
            float vol = Mathf.Clamp01(baseVol * Mathf.Max(0f, a.LocalVolumeScale) * Mathf.Clamp01(a.Fade) * distanceAttenuation);
            a.Source.volume = vol;

            float timeScale = GetCurrentTimeScale();
            float playbackTimeScale = a.AffectedPlaybackSpeedByTimeScale ? Mathf.Max(0f, timeScale) : 1f;
            float playbackSpeed = Mathf.Max(0f, a.BasePlaybackSpeed) * playbackTimeScale;

            float pitchScale = a.AffectedByTimeScale ? Mathf.Max(0f, timeScale) : 1f;
            float influence = Mathf.Clamp01(a.PitchInfluence);
            float timeScalePitchMul = (influence <= 0f) ? 1f : Mathf.Pow(pitchScale, influence);

            float pitch = a.BasePitch * timeScalePitchMul;
            if (a.ApplyPlaybackSpeedToPitch)
                pitch *= playbackSpeed;

            pitch = Mathf.Clamp(pitch, 0f, MaxPitch);
            a.Source.pitch = pitch;
        }

        float ResolveDistanceAttenuation(ActiveSound a)
        {
            if (a == null || !a.IsLocalPlayback || a.Source == null)
                return 1f;

            if (!TryResolveListenerTransform(out var listenerTransform) || listenerTransform == null)
                return 1f;

            var sourcePosition = a.Source.transform.position;
            var listenerPosition = listenerTransform.position;
            if (_use2DDistanceAttenuation)
            {
                sourcePosition.z = 0f;
                listenerPosition.z = 0f;
            }

            var distance = Vector3.Distance(sourcePosition, listenerPosition);
            return EvaluateDistanceAttenuation(distance, a.MinDistance, a.MaxDistance, a.RolloffMode);
        }

        static float EvaluateDistanceAttenuation(float distance, float minDistance, float maxDistance, AudioRolloffMode rolloffMode)
        {
            minDistance = Mathf.Max(0f, minDistance);
            maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);

            if (distance <= minDistance)
                return 1f;

            if (distance >= maxDistance)
                return 0f;

            var normalized = Mathf.InverseLerp(minDistance, maxDistance, distance);
            return rolloffMode switch
            {
                AudioRolloffMode.Logarithmic => Mathf.Clamp01(1f - Mathf.Log10(1f + (9f * normalized))),
                AudioRolloffMode.Linear => Mathf.Clamp01(1f - normalized),
                _ => Mathf.Clamp01(1f - normalized),
            };
        }

        bool TryResolveListenerTransform(out Transform transform)
        {
            transform = null;

            if (_useCustomListenerTarget &&
                _listenerScope != null &&
                TryResolveCustomListenerTransform(out transform) &&
                transform != null)
            {
                return true;
            }

            return TryResolveSceneAudioListenerTransform(out transform) && transform != null;
        }

        bool TryResolveCustomListenerTransform(out Transform transform)
        {
            transform = null;
            if (_listenerScope == null)
                return false;

            var scope = ActorSourceFastResolver.ResolveCached(_listenerScope, _listenerTarget, ref _listenerTargetCache, _listenerScope);
            return TryGetScopeTransform(scope, out transform) && transform != null;
        }

        bool TryResolveSceneAudioListenerTransform(out Transform transform)
        {
            transform = null;
            if (_cachedSceneListener != null && _cachedSceneListener.isActiveAndEnabled)
            {
                transform = _cachedSceneListener.transform;
                return true;
            }

            _cachedSceneListener = null;
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener == null || !listener.isActiveAndEnabled)
                    continue;

                _cachedSceneListener = listener;
                transform = listener.transform;
                return true;
            }

            return false;
        }

        static bool TryGetScopeTransform(IScopeNode scope, out Transform transform)
        {
            transform = null;
            if (scope == null)
                return false;

            var identityTransform = scope.Identity?.SelfTransform;
            if (identityTransform != null)
            {
                transform = identityTransform;
                return true;
            }

            if (scope is Component component && component != null)
            {
                transform = component.transform;
                return true;
            }

            return false;
        }

        float GetCurrentTimeScale()
        {
            if (_timeService != null)
                return Mathf.Max(0f, _timeService.UnityTimeScale);
            return Mathf.Max(0f, UnityEngine.Time.timeScale);
        }

        static float ResolvePlaybackSpeed(IAudioCue cue, SoundRequest request)
        {
            if (request.PlaybackSpeedScaleOverride.HasValue)
                return Mathf.Max(0f, request.PlaybackSpeedScaleOverride.Value);
            return Mathf.Max(0f, cue.BasePlaybackSpeed);
        }

        static bool ResolveApplyPlaybackSpeedToPitch(IAudioCue cue, SoundRequest request)
        {
            if (request.ApplyPlaybackSpeedToPitchOverride.HasValue)
                return request.ApplyPlaybackSpeedToPitchOverride.Value;
            return cue.ApplyPlaybackSpeedToPitch;
        }

        static bool ResolveAffectedPlaybackSpeedByTimeScale(BusMixer mixer, IAudioCue cue, SoundRequest request)
        {
            if (request.ApplyTimeScaleToPlaybackSpeedOverride.HasValue)
                return request.ApplyTimeScaleToPlaybackSpeedOverride.Value;

            if (cue.UseBusTimeScaleSettings)
                return mixer.Config.ApplyTimeScaleToPlaybackSpeed;

            return cue.ApplyTimeScaleToPlaybackSpeed;
        }

        static bool ResolveAffectedPitchByTimeScale(BusMixer mixer, IAudioCue cue, SoundRequest request)
        {
            if (request.ApplyTimeScaleToPitchOverride.HasValue)
                return request.ApplyTimeScaleToPitchOverride.Value;

            if (request.AffectedByTimeScaleOverride.HasValue)
                return request.AffectedByTimeScaleOverride.Value;

            if (cue.UseBusTimeScaleSettings)
                return mixer.Config.AffectedByTimeScale;

            return cue.ApplyTimeScaleToPitch;
        }

        static float ResolvePitchInfluence(BusMixer mixer, IAudioCue cue, SoundRequest request)
        {
            if (request.PitchInfluenceOverride.HasValue)
                return Mathf.Clamp01(request.PitchInfluenceOverride.Value);

            if (cue.UseBusTimeScaleSettings)
                return Mathf.Clamp01(mixer.Config.PitchInfluence);

            return Mathf.Clamp01(cue.TimeScalePitchInfluence);
        }

        static bool ValidatePlaybackStartOffset(IAudioCue cue, AudioClip clip, float playbackStartOffset, string tag)
        {
            if (clip == null)
                return false;

            if (playbackStartOffset <= 0f)
                return true;

            var clipLength = Mathf.Max(0f, clip.length);
            if (playbackStartOffset < clipLength)
                return true;

            var cueName = cue is UnityEngine.Object cueObject ? cueObject.name : cue?.ToString();
            var tagLabel = string.IsNullOrEmpty(tag) ? "<default>" : tag;
            Debug.LogError(
                $"[AudioService] Playback start offset exceeds clip length. Cue={cueName ?? "null"} Tag={tagLabel} Clip={clip.name} ClipLength={clipLength:0.###} Offset={playbackStartOffset:0.###}");
            return false;
        }

        void EnsurePool(BusMixer mixer, int count)
        {
            while (mixer.Pool.Count < count)
            {
                mixer.Pool.Add(CreateAudioSource($"{mixer.Config.Bus}_Source_{mixer.Pool.Count}"));
            }
        }

        AudioSource GetAvailableSource(BusMixer mixer)
        {
            for (int i = 0; i < mixer.Pool.Count; i++)
            {
                var s = mixer.Pool[i];
                if (s == null)
                    continue;
                if (IsSourceInUse(mixer, s))
                    continue;
                if (!s.isPlaying)
                    return s;
            }

            var src = CreateAudioSource($"{mixer.Config.Bus}_Source_{mixer.Pool.Count}");
            mixer.Pool.Add(src);
            return src;
        }

        static bool IsSourceInUse(BusMixer mixer, AudioSource source)
        {
            for (int i = 0; i < mixer.Active.Count; i++)
            {
                var a = mixer.Active[i];
                if (a?.Source == null) continue;
                if (ReferenceEquals(a.Source, source))
                    return true;
            }
            return false;
        }

        AudioSource CreateAudioSource(string name)
        {
            var go = new GameObject(name);
            EnsureRoot();
            go.transform.SetParent(_root.transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            src.loop = false;
            src.spatialize = false;
            src.spatialBlend = 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            return src;
        }

        void ConfigureSourceForCue(AudioSource src, IAudioCue cue, SoundRequest req)
        {
            bool spatial = req.IsLocalPlayback && (req.SpatializeOverride ?? cue.Spatialize);
            src.spatialize = spatial;
            src.spatialBlend = spatial ? Mathf.Clamp01(cue.SpatialBlend) : 0f;

            src.minDistance = Mathf.Max(0.01f, cue.MinDistance);
            src.maxDistance = Mathf.Max(src.minDistance, cue.MaxDistance);
            src.rolloffMode = cue.RolloffMode;
            src.loop = cue.Loop;

            if (req.WorldPosition.HasValue)
            {
                src.transform.position = req.WorldPosition.Value;
            }
            else if (req.FollowTarget)
            {
                src.transform.position = req.FollowTarget.position + req.LocalOffset;
            }
            else
            {
                src.transform.position = Vector3.zero;
            }
        }

        void ResetSource(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            src.clip = null;
            src.loop = false;
            src.pitch = 1f;
            src.volume = 1f;
            src.spatialize = false;
            src.spatialBlend = 0f;
            src.transform.localPosition = Vector3.zero;
        }

        static float PickPitchRandom(IAudioCue cue)
        {
            float min = cue.PitchRandomMin;
            float max = cue.PitchRandomMax;
            if (max <= 0f) max = 1f;
            if (min <= 0f) min = 1f;
            if (min > max) (min, max) = (max, min);
            return UnityEngine.Random.Range(min, max);
        }

        void RegisterAppEvents()
        {
            if (_appEventsRegistered)
                return;

            _appEventsRegistered = true;
            _isFocused = Application.isFocused;
            _isPaused = false;
            UpdateAppSuspended();

            EnsureRoot();
            if (_appEventProxy == null)
            {
                _appEventProxy = _root.AddComponent<AppEventProxy>();
                _appEventProxy.Initialize(this);
            }
        }

        void UnregisterAppEvents()
        {
            if (!_appEventsRegistered)
                return;
            _appEventsRegistered = false;

            if (_appEventProxy != null)
            {
                UnityEngine.Object.Destroy(_appEventProxy);
                _appEventProxy = null;
            }
        }

        void OnFocusChanged(bool hasFocus)
        {
            _isFocused = hasFocus;
            UpdateAppSuspended();
        }

        void OnPauseStateChanged(bool paused)
        {
            _isPaused = paused;
            UpdateAppSuspended();
        }

        void UpdateAppSuspended()
        {
            bool suspended = !_isFocused || _isPaused;
            if (_isAppSuspended == suspended)
                return;

            _isAppSuspended = suspended;
            if (_isAppSuspended)
                PauseAllActive();
            else
                ResumeAllActive();
        }

        void PauseAllActive()
        {
            foreach (var kv in _buses)
            {
                var mixer = kv.Value;
                for (int i = 0; i < mixer.Active.Count; i++)
                {
                    var a = mixer.Active[i];
                    if (a?.Source == null) continue;
                    a.WasPlayingBeforeSuspend = a.Source.isPlaying;
                    if (a.WasPlayingBeforeSuspend)
                        a.Source.Pause();
                }
            }
        }

        void ResumeAllActive()
        {
            foreach (var kv in _buses)
            {
                var mixer = kv.Value;
                for (int i = 0; i < mixer.Active.Count; i++)
                {
                    var a = mixer.Active[i];
                    if (a?.Source == null) continue;
                    if (!a.WasPlayingBeforeSuspend) continue;
                    a.WasPlayingBeforeSuspend = false;
                    a.Source.UnPause();
                }
            }
        }

        void ClearSuspendFlags()
        {
            foreach (var kv in _buses)
            {
                var mixer = kv.Value;
                for (int i = 0; i < mixer.Active.Count; i++)
                {
                    var a = mixer.Active[i];
                    if (a == null) continue;
                    a.WasPlayingBeforeSuspend = false;
                }
            }
        }

        sealed class AppEventProxy : MonoBehaviour
        {
            AudioService _owner;

            public void Initialize(AudioService owner)
            {
                _owner = owner;
            }

            void OnApplicationFocus(bool hasFocus)
            {
                _owner?.OnFocusChanged(hasFocus);
            }

            void OnApplicationPause(bool paused)
            {
                _owner?.OnPauseStateChanged(paused);
            }
        }
    }
}

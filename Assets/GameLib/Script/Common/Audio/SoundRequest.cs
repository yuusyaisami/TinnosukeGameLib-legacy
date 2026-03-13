using UnityEngine;

namespace Game.Audio
{
    // ================================================================
    // SoundRequest - 再生リクエスト
    // ================================================================
    //
    // ## 概要
    //
    // PlaySound の唯一の公開リクエスト構造体。
    // 位置、ボリューム、フェード、TimeScale 影響などを指定可能。
    //
    // ## Stop/FadeOut の実現
    //
    // cue=null で PlaySound を呼ぶと Stop/FadeOut として機能する。
    // Tag を指定して対象を特定する。
    //
    // ================================================================

    /// <summary>
    /// サウンド再生リクエスト。
    /// </summary>
    public struct SoundRequest
    {
        // ----------------------------------------------------------------
        // 位置指定
        // ----------------------------------------------------------------

        /// <summary>ワールド座標（指定時は固定位置で再生）</summary>
        public Vector3? WorldPosition;

        /// <summary>追従対象 Transform</summary>
        public Transform FollowTarget;

        /// <summary>追従時のローカルオフセット</summary>
        public Vector3 LocalOffset;

        /// <summary>距離減衰対象のローカル再生か</summary>
        public bool IsLocalPlayback;

        // ----------------------------------------------------------------
        // 再生設定
        // ----------------------------------------------------------------

        /// <summary>ボリュームスケール（0以下なら1として扱う）</summary>
        public float VolumeScale;

        /// <summary>再生速度スケールのオーバーライド（null なら Cue 設定を使用）</summary>
        public float? PlaybackSpeedScaleOverride;

        /// <summary>再生開始位置オフセット（秒）</summary>
        public float PlaybackStartOffsetSeconds;

        /// <summary>空間化のオーバーライド（null なら Cue 設定を使用）</summary>
        public bool? SpatializeOverride;

        /// <summary>バスのオーバーライド（null なら Cue 設定を使用）</summary>
        public AudioBusKind? BusOverride;

        // ----------------------------------------------------------------
        // タグ・重複制御
        // ----------------------------------------------------------------

        /// <summary>
        /// 再生を識別するタグ。
        /// BGM の切り替えや Stop/FadeOut の対象指定に使用。
        /// </summary>
        public string Tag;

        /// <summary>
        /// 同一タグが再生中の場合、何もしない
        /// </summary>
        public bool IgnoreIfAlreadyPlaying;

        // ----------------------------------------------------------------
        // フェード
        // ----------------------------------------------------------------

        /// <summary>フェードイン時間（秒）</summary>
        public float FadeInSeconds;

        /// <summary>フェードアウト時間（秒、Stop/上書き時。未指定なら Bus 既定）</summary>
        public float FadeOutSeconds;

        // ----------------------------------------------------------------
        // TimeScale 影響
        // ----------------------------------------------------------------

        /// <summary>
        /// Time.timeScale 変化時にピッチに影響を受けるか。
        /// null なら Bus 既定を使用。
        /// </summary>
        public bool? AffectedByTimeScaleOverride;

        /// <summary>
        /// TimeScale を再生速度へ反映するか。
        /// null なら Cue 設定、Cue が Bus 既定利用なら Bus 既定を使用。
        /// </summary>
        public bool? ApplyTimeScaleToPlaybackSpeedOverride;

        /// <summary>
        /// TimeScale をピッチへ反映するか。
        /// null なら Cue 設定、Cue が Bus 既定利用なら Bus 既定を使用。
        /// </summary>
        public bool? ApplyTimeScaleToPitchOverride;

        /// <summary>
        /// 再生速度を AudioSource.pitch に反映するか。
        /// Unity 標準 AudioSource では OFF の場合、再生速度変更自体も適用されない。
        /// </summary>
        public bool? ApplyPlaybackSpeedToPitchOverride;

        /// <summary>
        /// ピッチへの影響度（0..1）。
        /// 0=無影響（常に1倍）、1=scale通り、0.5=sqrt(scale)。
        /// null なら Bus 既定を使用。
        /// </summary>
        public float? PitchInfluenceOverride;

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        /// <summary>
        /// 実効ボリュームスケールを取得。
        /// </summary>
        public float GetVolumeScale() => VolumeScale <= 0f ? 1f : VolumeScale;

        /// <summary>
        /// ワールド座標指定のリクエストを作成。
        /// </summary>
        public static SoundRequest At(Vector3 pos, float volumeScale = 1f)
            => new SoundRequest { WorldPosition = pos, VolumeScale = volumeScale, IsLocalPlayback = true };

        /// <summary>
        /// 追従対象指定のリクエストを作成。
        /// </summary>
        public static SoundRequest Follow(Transform t, Vector3 offset = default, float volumeScale = 1f)
            => new SoundRequest { FollowTarget = t, LocalOffset = offset, VolumeScale = volumeScale, IsLocalPlayback = true };

        /// <summary>
        /// 指定タグの Stop リクエストを作成。
        /// </summary>
        public static SoundRequest Stop(string tag, float fadeOutSeconds = 0f)
            => new SoundRequest { Tag = tag, FadeOutSeconds = fadeOutSeconds };

        /// <summary>
        /// 指定バスの Stop リクエストを作成。
        /// </summary>
        public static SoundRequest StopBus(AudioBusKind bus, float fadeOutSeconds = 0f)
            => new SoundRequest { BusOverride = bus, FadeOutSeconds = fadeOutSeconds };
    }
}

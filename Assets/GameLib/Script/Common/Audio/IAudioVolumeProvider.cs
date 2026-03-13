using System;

namespace Game.Audio
{
    // ================================================================
    // IAudioVolumeProvider - ボリューム提供インターフェース
    // ================================================================
    //
    // ## 概要
    //
    // マスターボリュームとバス別ボリュームを提供する。
    // Scalar システムとの連携は ScalarAudioVolumeProvider で実装。
    //
    // ## 実装例
    //
    // - ConstantAudioVolumeProvider: 固定値
    // - ScalarAudioVolumeProvider: ScalarKey から取得
    //
    // ================================================================

    /// <summary>
    /// ボリューム変更時の引数。
    /// </summary>
    public readonly struct VolumeChangedArgs
    {
        public readonly AudioBusKind? Bus; // null = Master
        public readonly float OldValue;
        public readonly float NewValue;

        public VolumeChangedArgs(AudioBusKind? bus, float oldValue, float newValue)
        {
            Bus = bus;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public bool IsMaster => !Bus.HasValue;
    }

    /// <summary>
    /// オーディオボリュームを提供するインターフェース。
    /// </summary>
    public interface IAudioVolumeProvider
    {
        /// <summary>マスターボリューム（0..1）</summary>
        float GetMaster();

        /// <summary>バス別ボリューム（0..1）</summary>
        float GetBus(AudioBusKind bus);

        /// <summary>ボリューム変更イベント（オプショナル）</summary>
        event Action<VolumeChangedArgs> OnVolumeChanged;
    }

    /// <summary>
    /// 固定値を返すボリュームプロバイダ。
    /// </summary>
    public sealed class ConstantAudioVolumeProvider : IAudioVolumeProvider
    {
        readonly float _master;
        readonly float _bus;

        public ConstantAudioVolumeProvider(float master = 1f, float bus = 1f)
        {
            _master = master;
            _bus = bus;
        }

        public float GetMaster() => _master;
        public float GetBus(AudioBusKind bus) => _bus;

        // 固定値なので変更イベントは発火しない
        public event Action<VolumeChangedArgs> OnVolumeChanged { add { } remove { } }
    }
}

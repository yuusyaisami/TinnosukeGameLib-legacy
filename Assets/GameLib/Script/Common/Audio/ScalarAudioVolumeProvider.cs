using System;
using Game.Scalar;
using Game.Scalar.Generated;

namespace Game.Audio
{
    // ================================================================
    // ScalarAudioVolumeProvider - Scalar 連携ボリュームプロバイダ
    // ================================================================
    //
    // ## 概要
    //
    // ScalarKey を使用してマスターボリュームとバス別ボリュームを取得する。
    // IProjectScalarService（または IGlobalScalarService）から値を取得。
    // Scalar のイベントを購読し、ボリューム変更を通知する。
    //
    // ## 使用する ScalarKey（ScalarKeys.g.cs から生成）
    //
    // - GameLib.Audio.Master.Volume : マスターボリューム (0..1)
    // - GameLib.Audio.Bgm.Volume    : BGM ボリューム (0..1)
    // - GameLib.Audio.Sfx.Volume    : 効果音ボリューム (0..1)
    // - GameLib.Audio.Voice.Volume  : ボイスボリューム (0..1)
    // - GameLib.Audio.System.Volume : システム効果音ボリューム (0..1)
    //
    // ================================================================

    /// <summary>
    /// Scalar 連携でボリュームを取得するプロバイダ。
    /// </summary>
    public sealed class ScalarAudioVolumeProvider : IAudioVolumeProvider, IDisposable
    {
        readonly IBaseScalarService _scalar;

        // 購読トークン
        IDisposable _subMaster;
        IDisposable _subBgm;
        IDisposable _subSfx;
        IDisposable _subVoice;
        IDisposable _subSystem;

        // ScalarKey（生成されたキーを使用）
        static readonly ScalarKey KeyMaster = ScalarKeys.GameLib.Audio.Master.Volume;
        static readonly ScalarKey KeyBgm = ScalarKeys.GameLib.Audio.Bgm.Volume;
        static readonly ScalarKey KeySfx = ScalarKeys.GameLib.Audio.Sfx.Volume;
        static readonly ScalarKey KeyVoice = ScalarKeys.GameLib.Audio.Voice.Volume;
        static readonly ScalarKey KeySystem = ScalarKeys.GameLib.Audio.System.Volume;

        public event Action<VolumeChangedArgs> OnVolumeChanged;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="scalar">スカラーサービス（Project または Global 推奨）</param>
        public ScalarAudioVolumeProvider(IBaseScalarService scalar)
        {
            _scalar = scalar;
            SubscribeToScalar();
        }

        void SubscribeToScalar()
        {
            if (_scalar == null) return;

            _subMaster = _scalar.GlobalSubscribe(KeyMaster, args =>
            {
                OnVolumeChanged?.Invoke(new VolumeChangedArgs(null, args.OldValue, args.NewValue));
            });

            _subBgm = _scalar.GlobalSubscribe(KeyBgm, args =>
            {
                OnVolumeChanged?.Invoke(new VolumeChangedArgs(AudioBusKind.Bgm, args.OldValue, args.NewValue));
            });

            _subSfx = _scalar.GlobalSubscribe(KeySfx, args =>
            {
                OnVolumeChanged?.Invoke(new VolumeChangedArgs(AudioBusKind.Sfx, args.OldValue, args.NewValue));
            });

            _subVoice = _scalar.GlobalSubscribe(KeyVoice, args =>
            {
                OnVolumeChanged?.Invoke(new VolumeChangedArgs(AudioBusKind.Voice, args.OldValue, args.NewValue));
            });

            _subSystem = _scalar.GlobalSubscribe(KeySystem, args =>
            {
                OnVolumeChanged?.Invoke(new VolumeChangedArgs(AudioBusKind.System, args.OldValue, args.NewValue));
            });
        }

        public float GetMaster()
        {
            if (_scalar == null) return 1f;
            if (_scalar.GlobalTryGet(KeyMaster, out var v))
                return UnityEngine.Mathf.Clamp01(v);
            return 1f;
        }

        public float GetBus(AudioBusKind bus)
        {
            if (_scalar == null) return 1f;

            var key = bus switch
            {
                AudioBusKind.Bgm => KeyBgm,
                AudioBusKind.Sfx => KeySfx,
                AudioBusKind.Voice => KeyVoice,
                AudioBusKind.System => KeySystem,
                _ => default,
            };

            if (key.Id == 0) return 1f;

            if (_scalar.GlobalTryGet(key, out var v))
                return UnityEngine.Mathf.Clamp01(v);
            return 1f;
        }

        public void Dispose()
        {
            _subMaster?.Dispose();
            _subBgm?.Dispose();
            _subSfx?.Dispose();
            _subVoice?.Dispose();
            _subSystem?.Dispose();

            _subMaster = null;
            _subBgm = null;
            _subSfx = null;
            _subVoice = null;
            _subSystem = null;
        }
    }
}

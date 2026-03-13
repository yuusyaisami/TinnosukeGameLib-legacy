#nullable enable
using System;

namespace Game.MaterialFx
{
    /// <summary>
    /// FinalValue を GPU に送信するサービス。
    /// Sender に応じて BaseShader Sink または KernelService へ振り分ける。
    /// </summary>
    public interface IMaterialFxDispatchService : IDisposable
    {
        /// <summary>FinalValue を GPU に送信</summary>
        void Dispatch(string stableKey, MaterialFxTypedValue value);

        /// <summary>全 dirty キーを一括送信</summary>
        void FlushAll(IMaterialFxLayerService layerService);

        /// <summary>変更を適用（SetPropertyBlock 等）</summary>
        void Apply();
    }
}

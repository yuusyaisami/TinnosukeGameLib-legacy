#nullable enable
using System.Collections.Generic;

namespace Game.MaterialFx
{
    /// <summary>
    /// 毎フレーム Tick を一括管理し、各 MaterialFxService を駆動するシステムサービス。
    /// </summary>
    public interface IMaterialFxSystemService
    {
        /// <summary>
        /// MaterialFxService を登録（通常は MaterialFxService 側から呼ばれる）
        /// </summary>
        void Register(IMaterialFxService service);

        /// <summary>
        /// MaterialFxService を登録解除（通常は Dispose 時）
        /// </summary>
        void Unregister(IMaterialFxService service);

        /// <summary>
        /// 毎フレーム呼び出し（LateUpdate 等から）。
        /// 登録済みの全 MaterialFxService を Tick する。
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 毎フレーム呼び出し（LateUpdate 等から）。
        /// deltaTime と unscaledDeltaTime を両方渡し、各サービスの設定に応じて使い分ける。
        /// </summary>
        void Tick(float deltaTime, float unscaledDeltaTime);
    }
}

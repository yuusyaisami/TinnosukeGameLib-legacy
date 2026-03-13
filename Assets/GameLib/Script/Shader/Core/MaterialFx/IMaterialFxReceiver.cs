#nullable enable
namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFxServiceを保持するオブジェクト共通インターフェース。
    /// AnimationSpriteChannelPlayer、LineDraw等が実装。
    /// </summary>
    public interface IMaterialFxReceiver
    {
        /// <summary>
        /// このレシーバーに関連付けられたMaterialFxService。
        /// 未初期化や対象が存在しない場合はnullを返す。
        /// </summary>
        IMaterialFxService? MaterialFx { get; }
    }
}

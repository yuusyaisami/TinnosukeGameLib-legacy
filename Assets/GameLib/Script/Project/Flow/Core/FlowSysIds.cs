#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// 定義済みのシステムコール ID をまとめた定数クラス。
    /// </summary>
    public static class FlowSysIds
    {
        /// <summary>UI ダイアログで選択肢を表示して結果を待つシステムコール</summary>
        public const int UiDialogChoice = 100;
        /// <summary>vNext コマンドを実行するシステムコール</summary>
        public const int RunCommand = 201;
    }
}

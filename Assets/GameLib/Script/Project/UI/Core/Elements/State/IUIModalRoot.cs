#nullable enable

using Game;

namespace Game.UI
{
    /// <summary>
    /// Modal Stack に登録できるルート要素が実装するインターフェース。
    /// 
    /// IUIModalRoot は UI の「入力クランプ境界」を表す。
    /// 判定は Transform 階層ではなく IScopeNode 階層に基づく。
    /// </summary>
    public interface IUIModalRoot
    {
        /// <summary>このモーダルルートの識別子（ログ/デバッグ用）。</summary>
        string ModalId { get; }

        /// <summary>このモーダルルートが現在アクティブかどうか。</summary>
        bool IsActive { get; }

        /// <summary>このモーダルルートに紐づくスコープノード。</summary>
        IScopeNode? OwnerScope { get; }

        /// <summary>このモーダルルートに紐づく graph handle。</summary>
        UINodeHandle OwnerHandle { get; }

        /// <summary>
        /// 指定したスコープがこのモーダルルートの子孫かどうか。
        /// </summary>
        bool IsDescendant(IScopeNode? target);

        /// <summary>
        /// 指定した handle がこのモーダルルートの子孫かどうか。
        /// </summary>
        bool IsDescendant(UINodeHandle target);
    }
}

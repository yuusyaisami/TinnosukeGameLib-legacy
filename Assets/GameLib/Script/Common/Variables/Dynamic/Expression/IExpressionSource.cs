// Game.Common.IExpressionSource.cs
//
// 式ベースの IDynamicSource 用インターフェース。
// GetDependentKeys() を提供して EventDriven モードをサポート。

using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>
    /// 式ベースの IDynamicSource が実装するインターフェース。
    /// MonitorChannelHub の EventDriven モードで依存キー収集に使用。
    /// </summary>
    public interface IExpressionSource
    {
        /// <summary>
        /// 式中で実際に使用された識別子一覧を取得。
        /// </summary>
        /// <returns>使用識別子のリスト。識別子がない場合は null。</returns>
        IReadOnlyList<string> GetDependentKeys();
    }
}

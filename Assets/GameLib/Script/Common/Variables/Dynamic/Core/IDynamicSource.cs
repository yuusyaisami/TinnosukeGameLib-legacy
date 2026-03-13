// Game.Common.IDynamicSource.cs
//
// IDynamicSource - 動的値ソースの抽象インターフェース
//
// 設計決定:
// - [SerializeReference] で多態化（型×Kind爆発を防止）
// - 評価結果は DynamicVariant で統一
// - Source が増えても DynamicValue 型は増えない

using System;
using Game;
using Game.Commands;

#nullable enable

namespace Game.Common
{
    /// <summary>
    /// 動的値ソースの抽象インターフェース。
    /// [SerializeReference] で多態化し、型×Kind爆発を防止。
    /// </summary>
    public interface IDynamicSource
    {
        /// <summary>
        /// 値を評価して DynamicVariant として返す。
        /// </summary>
        /// <param name="context">評価コンテキスト</param>
        /// <returns>評価結果</returns>
        DynamicVariant Evaluate(IDynamicContext context);

        /// <summary>
        /// ソースの種別名（デバッグ/Editor表示用）。
        /// </summary>
        string SourceTypeName { get; }

        /// <summary>
        /// KeyやValueなどの、データ(デバッグ用)
        /// </summary>
        string GetDebugData { get; }
    }

    /// <summary>
    /// 動的値評価のコンテキスト。
    /// VarStore/Blackboard/Scalar 等へのアクセスを提供。
    /// </summary>
    public interface IDynamicContext
    {
        /// <summary>
        /// VarStore へのアクセス。
        /// </summary>
        IVarStore Vars { get; }

        /// <summary>自スコープの LifetimeScope。</summary>
        IScopeNode Scope { get; }

        /// <summary>
        /// コマンド実行チェーンの起点スコープ。
        /// CommandContext 以外では null を返してよい。
        /// </summary>
        IScopeNode? CommandRootScope { get; }

        /// <summary>
        /// 別スコープを解決（OtherScalar/OtherBlackboard用）。
        /// </summary>
        IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter);
    }
}

// Game.StateMachine.IStateMachine.cs

using System;

namespace Game.StateMachine
{
    /// <summary>
    /// StateMachine の読み取り専用インターフェース。
    /// </summary>
    public interface IStateMachineReadOnly
    {
        // ────────────────────────────────────────────────────────────
        //  Current State
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 全 Layer を通じた最終選択 State のフル StateKey。
        /// どの Layer にも Active な State がない場合は null。
        /// </summary>
        string CurrentState { get; }

        /// <summary>
        /// <see cref="CurrentState"/> が属する LayerKey。
        /// CurrentState が null の場合は null。
        /// </summary>
        string CurrentLayer { get; }

        // ────────────────────────────────────────────────────────────
        //  Layer / State Query
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定 Layer で現在選択（最高優先度）されている State の StateLeaf。
        /// Active な State がない場合は null。
        /// </summary>
        /// <param name="layerKey">対象の LayerKey</param>
        string GetSelectedStateLeaf(string layerKey);

        /// <summary>
        /// 指定 StateKey が現在 Active かどうか。
        /// </summary>
        bool IsStateActive(string stateKey);

        /// <summary>
        /// 指定 StateKey が指定 tag に紐付いて Active かどうか。
        /// </summary>
        bool IsStateActive(string stateKey, string tag);

        /// <summary>
        /// 指定 LayerKey が存在し、かつ Active な State を持つか。
        /// </summary>
        bool IsLayerActive(string layerKey);

        // ────────────────────────────────────────────────────────────
        //  Option
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// GlobalOption の現在値 (OptionValue) を取得する。
        /// 未設定の場合は null。
        /// </summary>
        /// <param name="optionKey">OptionKey (e.g. <c>Movement.Direction</c>)</param>
        string GetGlobalOption(string optionKey);

        /// <summary>
        /// LocalOption の現在値 (OptionValue) を取得する。
        /// 未設定の場合は null。
        /// </summary>
        /// <param name="layerKey">対象 LayerKey</param>
        /// <param name="optionKey">OptionKey (e.g. <c>Movement.Direction</c>)</param>
        string GetLocalOption(string layerKey, string optionKey);

        /// <summary>
        /// Option を解決する。
        /// Local(currentLayer) → Global の順で検索し、最初に見つかった値を返す。
        /// </summary>
        /// <param name="optionKey">OptionKey</param>
        /// <returns>解決された OptionValue。見つからない場合は null。</returns>
        string ResolveOption(string optionKey);

        /// <summary>
        /// Option を解決する（検索開始 Layer を指定）。
        /// Local(指定Layer) → Global の順で検索。
        /// </summary>
        /// <param name="layerKey">検索を開始する LayerKey。null の場合は Global のみ検索。</param>
        /// <param name="optionKey">OptionKey</param>
        /// <returns>解決された OptionValue。見つからない場合は null。</returns>
        string ResolveOption(string layerKey, string optionKey);

        // ────────────────────────────────────────────────────────────
        //  Revision (変更監視)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// StateMachine 全体の Revision。
        /// State の Acquire/Release、Option 変更、Pulse 発火時に増加する。
        /// 変更検知用。比較するだけで変化を検出可能。
        /// </summary>
        uint MachineRevision { get; }

        /// <summary>
        /// GlobalOption の Revision。GlobalOption が変更されるたびに増加。
        /// </summary>
        uint GlobalOptionRevision { get; }

        /// <summary>
        /// 指定 Layer の LocalOption Revision。該当 Layer の LocalOption 変更で増加。
        /// </summary>
        uint GetLayerOptionRevision(string layerKey);

        // ────────────────────────────────────────────────────────────
        //  Pulse
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定 State の Pulse カウントを取得する。
        /// Pulse は同一 State への再発火イベント（攻撃連打等）で使用。
        /// State が非 Active になるとリセットされる。
        /// </summary>
        /// <param name="stateKey">対象 StateKey</param>
        uint GetPulseCount(string stateKey);
    }

    /// <summary>
    /// StateMachine の書き込みインターフェース（トークン方式）。
    /// </summary>
    public interface IStateMachine : IStateMachineReadOnly
    {
        // ────────────────────────────────────────────────────────────
        //  State 制御
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// State を有効化し、制御トークンを返す。
        /// 対象の Layer / State が未登録の場合は自動で生成・登録される (オンデマンド登録)。
        /// </summary>
        /// <param name="stateKey">StateKey (e.g. <c>Movement.Idle</c>)</param>
        /// <param name="ownerId">トークン所有者の識別子 (デバッグ用)</param>
        /// <returns>State 制御トークン。Dispose または Release で解放。</returns>
        /// <exception cref="ArgumentException">stateKey が無効な形式の場合</exception>
        IStateToken AcquireState(string stateKey, string ownerId);

        /// <summary>
        /// トークンを使用して State を無効化する。
        /// 二重解放や無効なトークンでも例外を投げず、安全に無視される。
        /// </summary>
        /// <param name="token">解放するトークン</param>
        void ReleaseState(IStateToken token);

        /// <summary>
        /// Fire-and-forget で State を有効化する。tag で管理し、ReleaseStatesByTag/ReleaseState(stateKey, tag) で解放する。
        /// </summary>
        /// <param name="stateKey">StateKey</param>
        /// <param name="tag">管理用 tag</param>
        /// <param name="ownerId">デバッグ用 ownerId</param>
        void SetState(string stateKey, string tag, string ownerId = "");

        /// <summary>
        /// 指定 tag に紐付いた State をすべて解放する。
        /// </summary>
        /// <param name="tag">対象 tag</param>
        void ReleaseStatesByTag(string tag);

        /// <summary>
        /// 指定 tag かつ stateKey に紐付いた State を解放する。
        /// </summary>
        /// <param name="stateKey">対象 StateKey</param>
        /// <param name="tag">対象 tag</param>
        void ReleaseState(string stateKey, string tag);

        // ────────────────────────────────────────────────────────────
        //  Pulse
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pulse を発火する（同 State 再開始イベント）。
        /// 対象 State が Active でない場合は無視される。
        /// </summary>
        /// <param name="stateKey">対象 StateKey</param>
        void FirePulse(string stateKey);

        /// <summary>
        /// 指定 tag で Active な State に限定して Pulse を発火する。
        /// </summary>
        /// <param name="stateKey">対象 StateKey</param>
        /// <param name="requiredTag">Active 判定に使う tag</param>
        void FirePulse(string stateKey, string requiredTag);

        // ────────────────────────────────────────────────────────────
        //  Option 制御
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// GlobalOption を設定する。
        /// </summary>
        /// <param name="optionKey">OptionKey (e.g. <c>Movement.Direction</c>)</param>
        /// <param name="value">OptionValue (e.g. <c>Movement.Direction.Left</c>)。null で削除。</param>
        void SetGlobalOption(string optionKey, string value);

        /// <summary>
        /// LocalOption を設定する。
        /// </summary>
        /// <param name="layerKey">対象 LayerKey</param>
        /// <param name="optionKey">OptionKey</param>
        /// <param name="value">OptionValue。null で削除。</param>
        void SetLocalOption(string layerKey, string optionKey, string value);
    }
}

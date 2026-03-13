// Game.StateMachine.StateKeyUtils.cs

using System;

namespace Game.StateMachine
{
    /// <summary>
    /// StateKey / OptionValue の解剖ユーティリティ。
    /// StateKey は任意のドット区切り階層を許容し、
    /// 「親パス = LayerKey」「末端セグメント = StateLeaf」として二分する。
    /// </summary>
    /// <remarks>
    /// <para>例:</para>
    /// <list type="bullet">
    ///   <item><c>Movement.Idle</c> → LayerKey=<c>Movement</c>, StateLeaf=<c>Idle</c></item>
    ///   <item><c>UI.Button.Click</c> → LayerKey=<c>UI.Button</c>, StateLeaf=<c>Click</c></item>
    ///   <item><c>UI.ListBox.Items.Selected</c> → LayerKey=<c>UI.ListBox.Items</c>, StateLeaf=<c>Selected</c></item>
    /// </list>
    /// </remarks>
    public static class StateKeyUtils
    {
        public const char Separator = '.';

        /// <summary>
        /// StateKey を LayerKey と StateLeaf に分解する。
        /// </summary>
        /// <param name="stateKey">StateKey (e.g. <c>Movement.Idle</c>, <c>UI.Button.Click</c>)</param>
        /// <param name="layerKey">親パス (e.g. <c>Movement</c>, <c>UI.Button</c>)</param>
        /// <param name="stateLeaf">末端セグメント (e.g. <c>Idle</c>, <c>Click</c>)</param>
        /// <returns>分解に成功した場合 true。セパレータが含まれない場合は false。</returns>
        public static bool SplitLayerAndLeaf(string stateKey, out string layerKey, out string stateLeaf)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                layerKey = null;
                stateLeaf = null;
                return false;
            }

            int lastDot = stateKey.LastIndexOf(Separator);
            if (lastDot < 0)
            {
                // セパレータなし → 単一セグメントのみ
                // 仕様上、StateKey は最低でも二層（Layer.State）を要求するため false
                layerKey = null;
                stateLeaf = null;
                return false;
            }

            layerKey = stateKey.Substring(0, lastDot);
            stateLeaf = stateKey.Substring(lastDot + 1);
            return !string.IsNullOrEmpty(layerKey) && !string.IsNullOrEmpty(stateLeaf);
        }

        /// <summary>
        /// StateKey から LayerKey を取得する。
        /// </summary>
        /// <param name="stateKey">StateKey</param>
        /// <returns>LayerKey。分解失敗時は null。</returns>
        public static string GetLayerKey(string stateKey)
        {
            SplitLayerAndLeaf(stateKey, out var layerKey, out _);
            return layerKey;
        }

        /// <summary>
        /// StateKey から StateLeaf を取得する。
        /// </summary>
        /// <param name="stateKey">StateKey</param>
        /// <returns>StateLeaf。分解失敗時は null。</returns>
        public static string GetStateLeaf(string stateKey)
        {
            SplitLayerAndLeaf(stateKey, out _, out var stateLeaf);
            return stateLeaf;
        }

        /// <summary>
        /// OptionValue を OptionKey と ValueLeaf に分解する。
        /// StateKey と同じロジックを使用。
        /// </summary>
        /// <param name="optionValue">OptionValue (e.g. <c>Movement.Direction.Left</c>)</param>
        /// <param name="optionKey">親パス (e.g. <c>Movement.Direction</c>)</param>
        /// <param name="valueLeaf">末端セグメント (e.g. <c>Left</c>)</param>
        /// <returns>分解に成功した場合 true。</returns>
        public static bool SplitOptionKeyAndValue(string optionValue, out string optionKey, out string valueLeaf)
        {
            return SplitLayerAndLeaf(optionValue, out optionKey, out valueLeaf);
        }

        /// <summary>
        /// OptionValue から OptionKey を取得する。
        /// </summary>
        public static string GetOptionKey(string optionValue)
        {
            SplitOptionKeyAndValue(optionValue, out var optionKey, out _);
            return optionKey;
        }

        /// <summary>
        /// LayerKey と StateLeaf を結合して StateKey を生成する。
        /// </summary>
        public static string CombineStateKey(string layerKey, string stateLeaf)
        {
            if (string.IsNullOrEmpty(layerKey) || string.IsNullOrEmpty(stateLeaf))
                return null;
            return $"{layerKey}{Separator}{stateLeaf}";
        }

        /// <summary>
        /// 指定文字列が有効な StateKey 形式かどうかを検証する。
        /// </summary>
        public static bool IsValidStateKey(string stateKey)
        {
            return SplitLayerAndLeaf(stateKey, out _, out _);
        }
    }
}

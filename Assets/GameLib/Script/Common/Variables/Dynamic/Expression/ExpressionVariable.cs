// Game.Common.ExpressionVariable.cs
//
// 式で使用する変数定義。
// BoolExpressionSource / FloatExpressionSource で使用。

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Common
{
    /// <summary>
    /// 式で使用する変数の定義。
    /// DynamicValue をソースとして使用し、ExpectedKind で型ヒントを持つ。
    /// </summary>
    [Serializable]
    public sealed class ExpressionVariable
    {
        [LabelText("$" + nameof(_variableLabel))] // Use property name for label (Odin expression)
        [SerializeField]
        DynamicValue _value;

        [LabelText("Use Scalar Leaf Key")]
        [SerializeField]
        bool _useScalarLeafKey;

        [LabelText("Use Custom Key")]
        [SerializeField]
        bool _useCustomKey;

        [LabelText("Custom Key")]
        [SerializeField, ShowIf(nameof(_useCustomKey))]
        string _customKey;

        [LabelText("Expected Type")]
        [SerializeField, FormerlySerializedAs("_expectedKind")]
        ValueKind _expectedType = ValueKind.Auto;

        /// <summary>
        /// 変数のキー（DynamicValue の DebugData から取得）
        /// </summary>
        public string Key => _value.HasSource ? _value.DebugData : string.Empty;

        /// <summary>
        /// 式で使用する変数キー。
        /// - Custom Key が有効ならそれ
        /// - それ以外で Scalar の場合、Use Scalar Leaf Key が有効なら leaf 名
        /// - それ以外は Key
        /// </summary>
        public string ExpressionKey
        {
            get
            {
                if (_useCustomKey)
                {
                    var trimmed = _customKey?.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        return trimmed;
                }

                var key = Key;
                if (_useScalarLeafKey && IsScalarSource && !string.IsNullOrEmpty(key))
                    return ExtractLeafKey(key);

                return key;
            }
        }

        /// <summary>
        /// 変数のソース値
        /// </summary>
        public DynamicValue Value => _value;



        /// <summary>
        /// 期待される値の種別
        /// </summary>
        public ValueKind ExpectedKind => _expectedType;

        /// <summary>
        /// ソースが設定されているか
        /// </summary>
        public bool HasSource => _value.HasSource;

        bool IsScalarSource
        {
            get
            {
                if (!_value.HasSource)
                    return false;
                return _value.TryGetSource<SelfScalarSource>(out _) || _value.TryGetSource<OtherScalarSource>(out _);
            }
        }

        static string ExtractLeafKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var lastSepIndex = key.LastIndexOfAny(new[] { '.', '/', '\\' });
            if (lastSepIndex < 0 || lastSepIndex >= key.Length - 1)
                return key;

            return key.Substring(lastSepIndex + 1);
        }

        private string _variableLabel => string.IsNullOrEmpty(ExpressionKey) ? "<None>" : ExpressionKey;
    }
}

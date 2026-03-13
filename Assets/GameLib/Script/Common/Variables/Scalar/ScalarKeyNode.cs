using System;
using UnityEngine;
using Game.Registry;

namespace Game.Scalar
{
    /// <summary>
    /// ScalarKey のノード。
    /// </summary>
    [Serializable]
    public sealed class ScalarKeyNode : HierarchyNodeBase
    {
        [Tooltip("キー文字列を明示的に指定する場合に使用。空ならパスから自動生成。")]
        [SerializeField] string explicitKey;

        [Tooltip("将来的に削除予定のキーならチェック。")]
        [SerializeField] bool obsolete;

        [Tooltip("検索用タグ。任意。")]
        [SerializeField] string[] tags;

        /// <summary>明示的なキー（空なら自動生成）</summary>
        public string ExplicitKey
        {
            get => explicitKey;
            set => explicitKey = value ?? string.Empty;
        }

        /// <summary>廃止予定かどうか</summary>
        public bool Obsolete
        {
            get => obsolete;
            set => obsolete = value;
        }

        /// <summary>検索用タグ</summary>
        public string[] Tags
        {
            get => tags ?? Array.Empty<string>();
            set => tags = value;
        }
    }
}

using System;
using UnityEngine;

namespace Game.Registry
{
    // ================================================================
    // HierarchyNodeBase - 階層ノードの基底クラス
    // ================================================================

    /// <summary>
    /// 階層ノードの基底クラス。
    /// 純粋なデータクラスであり、Editor/Runtime 両方で使用可能。
    /// </summary>
    [Serializable]
    public abstract class HierarchyNodeBase
    {
        [SerializeField] protected string id;
        [SerializeField] protected string parentId;
        [SerializeField] protected string name;
        [SerializeField] protected bool isFolder;
        [SerializeField, TextArea] protected string description;

        /// <summary>ノードの一意識別子（GUID）</summary>
        public string Id => id;

        /// <summary>親ノードの ID（空文字列ならルート直下）</summary>
        public string ParentId
        {
            get => parentId;
            set => parentId = value ?? string.Empty;
        }

        /// <summary>セグメント名（フォルダ名またはキー名）</summary>
        public string Name
        {
            get => name;
            set => name = value ?? string.Empty;
        }

        /// <summary>フォルダかどうか</summary>
        public bool IsFolder
        {
            get => isFolder;
            set => isFolder = value;
        }

        /// <summary>説明文</summary>
        public string Description
        {
            get => description;
            set => description = value;
        }

        /// <summary>新規ノードを初期化する。</summary>
        public void Initialize(string parentId, string name, bool isFolder)
        {
            this.id = Guid.NewGuid().ToString("N");
            this.parentId = parentId ?? string.Empty;
            this.name = name ?? string.Empty;
            this.isFolder = isFolder;
            this.description = string.Empty;
        }
    }
}

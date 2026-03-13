#if UNITY_EDITOR
using Game.Editor.CodeGen;
using Game.Editor.Foundation;
using UnityEngine;
using UnityEditor;

namespace Game.Editor.Registry
{
    // ================================================================
    // RegistrySettingsBase - Registry の Explorer / CodeGen 統合設定 SO の基底クラス
    // ================================================================
    //
    // ## 概要
    //
    // 各 Registry の Explorer Window と CodeGenerator の設定を
    // 1つの ScriptableObject で管理する基底クラス。
    //
    // ## 設定項目
    //
    // - Explorer 設定: windowTitle, minSize, folderIcon, leafIcon
    // - Tree 設定: treeVisual (RowHeight, IndentWidth, IconSizeRatio など)
    // - CodeGen 設定: namespaceName, rootClassName, outputPath
    //
    // ================================================================

    /// <summary>
    /// Registry の Explorer / CodeGen 統合設定 SO の基底クラス。
    /// </summary>
    public abstract class RegistrySettingsBase : ScriptableObject, ICodeGenSettings
    {
        [Header("Explorer Settings")]
        [SerializeField] protected string windowTitle = "Registry Explorer";
        [SerializeField] protected Vector2 minSize = new(600, 400);
        [SerializeField] protected Texture2D folderIcon;
        [SerializeField] protected Texture2D leafIcon;

        [Header("Tree Behavior")]
        [Tooltip("ツリー表示時にノードをソートするか（フォルダ優先・名前順）")]
        [SerializeField] protected bool enableTreeSort = false;

        [Header("Tree Visual Settings")]
        [SerializeField] protected TreeVisualSettings treeVisual = new();

        [Header("Code Generation")]
        [SerializeField] protected string namespaceName = "Game.Generated";
        [SerializeField] protected string rootClassName = "Keys";
        [SerializeField] protected string outputPath = "Assets/Game/Script/Generated/Keys.g.cs";

        // ------------------------------------------------------------
        // Explorer 設定
        // ------------------------------------------------------------

        /// <summary>ウィンドウタイトル</summary>
        public string WindowTitle => windowTitle;

        /// <summary>ウィンドウ最小サイズ</summary>
        public Vector2 MinSize => minSize;

        /// <summary>フォルダアイコン</summary>
        public Texture2D FolderIcon => folderIcon;

        /// <summary>リーフアイコン</summary>
        public Texture2D LeafIcon => leafIcon;

        /// <summary>ツリー表示時にソートを行うか</summary>
        public bool EnableTreeSort => enableTreeSort;

        /// <summary>ツリー表示設定</summary>
        public TreeVisualSettings TreeVisual => treeVisual;

        // ------------------------------------------------------------
        // CodeGen 設定 (ICodeGenSettings)
        // ------------------------------------------------------------

        /// <summary>コード生成名前空間</summary>
        public string NamespaceName => namespaceName;

        /// <summary>コード生成クラス名</summary>
        public string RootClassName => rootClassName;

        /// <summary>コード生成出力パス</summary>
        public string OutputPath => outputPath;

        // ------------------------------------------------------------
        // アイコン取得ヘルパー
        // ------------------------------------------------------------

        /// <summary>
        /// フォルダアイコンを取得（設定がなければデフォルト）
        /// </summary>
        public Texture2D GetFolderIcon()
        {
            return folderIcon != null ? folderIcon : EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
        }

        /// <summary>
        /// リーフアイコンを取得（設定がなければデフォルト）
        /// </summary>
        public Texture2D GetLeafIcon()
        {
            return leafIcon != null ? leafIcon : EditorGUIUtility.IconContent("d_ScriptableObject Icon").image as Texture2D;
        }
    }
}
#endif

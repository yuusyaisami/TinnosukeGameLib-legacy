#if UNITY_EDITOR
using Game.Editor.CodeGen;
using Game.Editor.Foundation;
using UnityEngine;
using UnityEditor;

namespace Game.Editor.Registry
{
    // ================================================================
    // RegistrySettingsBase - Registry 縺ｮ Explorer / CodeGen 邨ｱ蜷郁ｨｭ螳・SO 縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // 蜷・Registry 縺ｮ Explorer Window 縺ｨ CodeGenerator 縺ｮ險ｭ螳壹ｒ
    // 1縺､縺ｮ ScriptableObject 縺ｧ邂｡逅・☆繧句渕蠎輔け繝ｩ繧ｹ縲・
    //
    // ## 險ｭ螳夐・岼
    //
    // - Explorer 險ｭ螳・ windowTitle, minSize, folderIcon, leafIcon
    // - Tree 險ｭ螳・ treeVisual (RowHeight, IndentWidth, IconSizeRatio 縺ｪ縺ｩ)
    // - CodeGen 險ｭ螳・ namespaceName, rootClassName, outputPath
    //
    // ================================================================

    /// <summary>
    /// Registry 縺ｮ Explorer / CodeGen 邨ｱ蜷郁ｨｭ螳・SO 縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// </summary>
    public abstract class RegistrySettingsBase : ScriptableObject, ICodeGenSettings
    {
        [Header("Explorer Settings")]
        [SerializeField] protected string windowTitle = "Registry Explorer";
        [SerializeField] protected Vector2 minSize = new(600, 400);
        [SerializeField] protected Texture2D folderIcon;
        [SerializeField] protected Texture2D leafIcon;

        [Header("Tree Behavior")]
        [Tooltip("Inspector setting.")]
        [SerializeField] protected bool enableTreeSort = false;

        [Header("Tree Visual Settings")]
        [SerializeField] protected TreeVisualSettings treeVisual = new();

        [Header("Code Generation")]
        [SerializeField] protected string namespaceName = "Game.Generated";
        [SerializeField] protected string rootClassName = "Keys";
        [SerializeField] protected string outputPath = "Assets/Game/Script/Generated/Keys.g.cs";

        // ------------------------------------------------------------
        // Explorer 險ｭ螳・
        // ------------------------------------------------------------

        /// <summary>繧ｦ繧｣繝ｳ繝峨え繧ｿ繧､繝医Ν</summary>
        public string WindowTitle => windowTitle;

        /// <summary>繧ｦ繧｣繝ｳ繝峨え譛蟆上し繧､繧ｺ</summary>
        public Vector2 MinSize => minSize;

        /// <summary>繝輔か繝ｫ繝繧｢繧､繧ｳ繝ｳ</summary>
        public Texture2D FolderIcon => folderIcon;

        /// <summary>繝ｪ繝ｼ繝輔い繧､繧ｳ繝ｳ</summary>
        public Texture2D LeafIcon => leafIcon;

        /// <summary>繝・Μ繝ｼ陦ｨ遉ｺ譎ゅ↓繧ｽ繝ｼ繝医ｒ陦後≧縺・/summary>
        public bool EnableTreeSort => enableTreeSort;

        /// <summary>繝・Μ繝ｼ陦ｨ遉ｺ險ｭ螳・/summary>
        public TreeVisualSettings TreeVisual => treeVisual;

        // ------------------------------------------------------------
        // CodeGen 險ｭ螳・(ICodeGenSettings)
        // ------------------------------------------------------------

        /// <summary>繧ｳ繝ｼ繝臥函謌仙錐蜑咲ｩｺ髢・/summary>
        public string NamespaceName => namespaceName;

        /// <summary>繧ｳ繝ｼ繝臥函謌舌け繝ｩ繧ｹ蜷・/summary>
        public string RootClassName => rootClassName;

        /// <summary>繧ｳ繝ｼ繝臥函謌仙・蜉帙ヱ繧ｹ</summary>
        public string OutputPath => outputPath;

        // ------------------------------------------------------------
        // 繧｢繧､繧ｳ繝ｳ蜿門ｾ励・繝ｫ繝代・
        // ------------------------------------------------------------

        /// <summary>
        /// 繝輔か繝ｫ繝繧｢繧､繧ｳ繝ｳ繧貞叙蠕暦ｼ郁ｨｭ螳壹′縺ｪ縺代ｌ縺ｰ繝・ヵ繧ｩ繝ｫ繝茨ｼ・
        /// </summary>
        public Texture2D GetFolderIcon()
        {
            return folderIcon != null ? folderIcon : EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
        }

        /// <summary>
        /// 繝ｪ繝ｼ繝輔い繧､繧ｳ繝ｳ繧貞叙蠕暦ｼ郁ｨｭ螳壹′縺ｪ縺代ｌ縺ｰ繝・ヵ繧ｩ繝ｫ繝茨ｼ・
        /// </summary>
        public Texture2D GetLeafIcon()
        {
            return leafIcon != null ? leafIcon : EditorGUIUtility.IconContent("d_ScriptableObject Icon").image as Texture2D;
        }
    }
}
#endif

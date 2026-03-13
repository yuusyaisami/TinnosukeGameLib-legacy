#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.CodeGen
{
    // ================================================================
    // CodeGenSettingsBase - コード生成設定の基底クラス
    // ================================================================

    /// <summary>
    /// コード生成設定の基底クラス。
    /// ICodeGenSettings を実装した ScriptableObject。
    /// 
    /// ## 使用方法
    /// 
    /// 各ジェネレーター用の設定クラスはこのクラスを継承し、
    /// 必要に応じて追加フィールドを定義する。
    /// </summary>
    public abstract class CodeGenSettingsBase : ScriptableObject, ICodeGenSettings
    {
        [Header("Code Generation")]
        [Tooltip("生成されるコードの名前空間")]
        [SerializeField]
        protected string namespaceName = "Game.Generated";

        [Tooltip("生成されるルートクラス名")]
        [SerializeField]
        protected string rootClassName = "GeneratedKeys";

        [Header("Output")]
        [Tooltip("出力ファイルパス（Assets からの相対パス）")]
        [SerializeField]
        protected string outputPath = "Assets/GameLib/Script/Generated/Keys.g.cs";

        /// <inheritdoc/>
        public virtual string NamespaceName => namespaceName;

        /// <inheritdoc/>
        public virtual string RootClassName => rootClassName;

        /// <inheritdoc/>
        public virtual string OutputPath => outputPath;
    }
}
#endif

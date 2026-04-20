#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.CodeGen
{
    // ================================================================
    // CodeGenSettingsBase - 繧ｳ繝ｼ繝臥函謌占ｨｭ螳壹・蝓ｺ蠎輔け繝ｩ繧ｹ
    // ================================================================

    /// <summary>
    /// 繧ｳ繝ｼ繝臥函謌占ｨｭ螳壹・蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// ICodeGenSettings 繧貞ｮ溯｣・＠縺・ScriptableObject縲・
    /// 
    /// ## 菴ｿ逕ｨ譁ｹ豕・
    /// 
    /// 蜷・ず繧ｧ繝阪Ξ繝ｼ繧ｿ繝ｼ逕ｨ縺ｮ險ｭ螳壹け繝ｩ繧ｹ縺ｯ縺薙・繧ｯ繝ｩ繧ｹ繧堤ｶ呎価縺励・
    /// 蠢・ｦ√↓蠢懊§縺ｦ霑ｽ蜉繝輔ぅ繝ｼ繝ｫ繝峨ｒ螳夂ｾｩ縺吶ｋ縲・
    /// </summary>
    public abstract class CodeGenSettingsBase : ScriptableObject, ICodeGenSettings
    {
        [Header("Code Generation")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        protected string namespaceName = "Game.Generated";

        [Tooltip("Inspector setting.")]
        [SerializeField]
        protected string rootClassName = "GeneratedKeys";

        [Header("Output")]
        [Tooltip("Inspector setting.")]
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

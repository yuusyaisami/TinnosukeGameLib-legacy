// Game.Profile.IProfileValueBinding.cs
//
// ProfileValue の Blackboard/Scalar へのバインディングインターフェース

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Kernel.IR;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Blackboard/Scalar へのバインディングを表現するインターフェース。
    /// BaseProfileSO のリフレクションベースフィールド列挙で使用。
    /// </summary>
    public interface IProfileValueBinding
    {
        /// <summary>
        /// Inspector の ListElementLabelName で使用する行ラベル
        /// </summary>
        string ProfileBindingListLabel { get; }

        /// <summary>
        /// Blackboard にバインドする VarId（0 の場合は Blackboard にバインドしない）
        /// </summary>
        int BlackboardKey { get; }

        /// <summary>
        /// Scalar にバインドするキー（default の場合は Scalar にバインドしない）
        /// </summary>
        ScalarKey ScalarKey { get; }

        /// <summary>
        /// Blackboard への書き込みポリシー
        /// </summary>
        BlackboardBindPolicy BlackboardPolicy { get; }

        /// <summary>
        /// Scalar への書き込みポリシー
        /// </summary>
        ScalarBindPolicy ScalarPolicy { get; }

        /// <summary>
        /// Blackboard vocabulary に対応する value seam へ値を書き込む
        /// </summary>
        void WriteToBlackboard(IVarStore blackboard);

        /// <summary>
        /// Scalar に値を書き込む
        /// </summary>
        void WriteToScalar(IBaseScalarService scalar);

        /// <summary>
        /// バインディングが有効かどうか（少なくとも1つのキーが設定されている）
        /// </summary>
        bool HasAnyBinding { get; }

        // ================================================================
        // Save メタ情報
        // ================================================================

        /// <summary>
        /// Scalar の Save が有効か
        /// </summary>
        bool ScalarSaveEnabled { get; }

        /// <summary>
        /// Scalar の SaveLayer
        /// </summary>
        SaveLayer ScalarSaveLayer { get; }

        /// <summary>
        /// Blackboard の Save が有効か
        /// </summary>
        bool BlackboardSaveEnabled { get; }

        /// <summary>
        /// Blackboard の SaveLayer
        /// </summary>
        SaveLayer BlackboardSaveLayer { get; }

        /// <summary>
        /// この Binding から SaveEntry を収集する
        /// </summary>
        /// <param name="entries">出力先リスト</param>
        /// <param name="scopeIdentity">Scope の安定 ID</param>
        /// <param name="profileTypeName">Profile の型名（デバッグ用）</param>
        void CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName);
    }

    /// <summary>
    /// Blackboard への書き込みポリシー
    /// </summary>
    public enum BlackboardBindPolicy
    {
        /// <summary>常に上書き</summary>
        Overwrite,

        /// <summary>既存キーがあればスキップ</summary>
        SkipIfExists,

        /// <summary>既存キーの値を尊重（上書きしない）</summary>
        RespectExistingNoOverwrite
    }

    /// <summary>
    /// Scalar への書き込みポリシー
    /// </summary>
    public enum ScalarBindPolicy
    {
        /// <summary>Baseline を更新</summary>
        UpdateBaseline,

        /// <summary>RuntimeConfig ごと置き換え</summary>
        ReplaceRuntime,

        /// <summary>既に Runtime が存在すればスキップ</summary>
        SkipIfExists
    }

    public interface IScalarDeclarationAuthoring
    {
        bool TryCreateScalarDeclaration(
            ScalarOwnerIdentity owner,
            string profileTypeName,
            SourceLocationIR source,
            out ScalarDeclarationInput declaration,
            out string failureReason);
    }

    public static class ProfileScalarDeclarationProjection
    {
        public static bool TryCreateScalarDeclaration(
            ScalarOwnerIdentity owner,
            ScalarKey key,
            ScalarBindPolicy applyPolicy,
            float baseValue,
            bool useEffectMod,
            bool useRoundMod,
            int roundDigits,
            bool useClampMod,
            ScalarClamp clamp,
            bool hasLocalBase,
            float localBaseValue,
            bool saveEnabled,
            SaveLayer saveLayer,
            string profileTypeName,
            string bindingTypeName,
            SourceLocationIR source,
            out ScalarDeclarationInput declaration,
            out string failureReason)
        {
            if (!TryMapApplyPolicy(applyPolicy, out ScalarDeclarationApplyPolicy declarationApplyPolicy, out failureReason))
            {
                declaration = default;
                return false;
            }

            string stableContext = (profileTypeName ?? string.Empty) + ":" + (bindingTypeName ?? string.Empty);
            string debugContext = string.IsNullOrWhiteSpace(bindingTypeName)
                ? (profileTypeName ?? string.Empty)
                : (profileTypeName ?? string.Empty) + "/" + bindingTypeName.Trim();

            return ScalarDeclarationProjection.TryCreateDeclaration(
                owner,
                key,
                declarationApplyPolicy,
                baseValue,
                useEffectMod,
                useRoundMod,
                roundDigits,
                useClampMod,
                clamp,
                hasLocalBase,
                localBaseValue,
                saveEnabled,
                saveLayer,
                ScalarDeclarationSourceKind.ProfileBinding,
                stableContext,
                debugContext,
                source,
                out declaration,
                out failureReason);
        }

        public static bool TryCreateScalarDeclarations(
            IEnumerable<IProfileValueBinding> bindings,
            ScalarOwnerIdentity owner,
            string profileTypeName,
            SourceLocationIR source,
            out ScalarDeclarationInput[] declarations,
            out string failureReason)
        {
            if (bindings == null)
            {
                declarations = Array.Empty<ScalarDeclarationInput>();
                failureReason = string.Empty;
                return true;
            }

            List<ScalarDeclarationInput> results = new List<ScalarDeclarationInput>();
            HashSet<ScalarBindingEndpoint> seenEndpoints = new HashSet<ScalarBindingEndpoint>();

            foreach (IProfileValueBinding binding in bindings)
            {
                if (binding == null || !HasRequestedScalarBinding(binding))
                    continue;

                if (!binding.ScalarKey.IsVerified)
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    failureReason = "Scalar-bound profile binding type '" + binding.GetType().FullName + "' requires a verified scalar key for M9.2 declaration projection.";
                    return false;
                }

                if (!(binding is IScalarDeclarationAuthoring scalarAuthoring))
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    failureReason = "Scalar-bound profile binding type '" + binding.GetType().FullName + "' must implement IScalarDeclarationAuthoring for M9.2 declaration projection.";
                    return false;
                }

                if (!scalarAuthoring.TryCreateScalarDeclaration(owner, profileTypeName, source, out ScalarDeclarationInput declaration, out failureReason))
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    return false;
                }

                if (!seenEndpoints.Add(declaration.Endpoint))
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    failureReason = "Duplicate scalar declaration endpoint detected for owner '" + owner + "' and key id '" + declaration.Endpoint.KeyId.Value + "'.";
                    return false;
                }

                results.Add(declaration);
            }

            declarations = results.ToArray();
            failureReason = string.Empty;
            return true;
        }

        public static bool HasRequestedScalarBinding(IProfileValueBinding binding)
        {
            if (binding == null)
                return false;

            ScalarKey scalarKey = binding.ScalarKey;
            return scalarKey.Id != 0 || !string.IsNullOrWhiteSpace(scalarKey.Name);
        }

        static bool TryMapApplyPolicy(
            ScalarBindPolicy applyPolicy,
            out ScalarDeclarationApplyPolicy declarationApplyPolicy,
            out string failureReason)
        {
            switch (applyPolicy)
            {
                case ScalarBindPolicy.UpdateBaseline:
                    declarationApplyPolicy = ScalarDeclarationApplyPolicy.UpdateBaseline;
                    failureReason = string.Empty;
                    return true;

                case ScalarBindPolicy.ReplaceRuntime:
                    declarationApplyPolicy = ScalarDeclarationApplyPolicy.ReplaceRuntime;
                    failureReason = string.Empty;
                    return true;

                case ScalarBindPolicy.SkipIfExists:
                    declarationApplyPolicy = ScalarDeclarationApplyPolicy.SkipIfExists;
                    failureReason = string.Empty;
                    return true;

                default:
                    declarationApplyPolicy = default;
                    failureReason = "Scalar declaration projection requires a defined apply policy.";
                    return false;
            }
        }
    }
}

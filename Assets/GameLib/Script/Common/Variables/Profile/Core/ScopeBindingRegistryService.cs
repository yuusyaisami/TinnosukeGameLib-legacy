// Game.Profile.ScopeBindingRegistryService.cs
//
// Profile Registry Service - Profile 定義の型ベース登録とバインディング適用
// IProfileSaveProvider を実装し、SaveManager への SaveEntry 供給を担当

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Pool;
using Game;
using Game.Common;
using Game.Kernel.IR;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Profile 定義を型ベースで管理するレジストリサービス。
    /// 登録時に IProfileValueBinding を自動的に Blackboard/Scalar に適用する。
    /// </summary>
    public sealed class ScopeBindingRegistryService : IScopeBindingRegistry
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // 重要: BaseNail のように同一 ProfileType(CustomProfileDefinition) を複数登録するケースがある。
        // 以前の Dictionary<Type, IProfileRuntime> のみ実装だと後勝ち上書きになり、
        // Reapply/Clear-Reset 後に先行定義（例: LevelValue baseline=1）が消えて RichText が Null になった。
        // _profiles は再適用用に全件保持し、_latestProfileByType は型解決 API 互換のため最新定義だけ保持する。
        readonly List<IProfileRuntime> _profiles = new();
        readonly Dictionary<Type, IProfileRuntime> _latestProfileByType = new();
        readonly IVarStore _blackboard;
        readonly IBaseScalarService _scalar;
        readonly IScopeNode _scope;
        string _scopeIdentity;

        int _version;

        // ================================================================
        // Constructor
        // ================================================================

        /// <summary>
        /// ScopeBindingRegistryService を作成。
        /// </summary>
        /// <param name="blackboard">Blackboard vocabulary に対応する value seam（null 可）</param>
        /// <param name="scalar">Scalar サービス（null 可）</param>
        /// <param name="scopeIdentity">Scope の安定 ID。Save 対象にする場合は必須。</param>
        public ScopeBindingRegistryService(
            IVarStore blackboard = null,
            IBaseScalarService scalar = null,
            string scopeIdentity = null,
            IScopeNode scope = null
            )
        {
            _blackboard = blackboard;
            _scalar = scalar;
            _scopeIdentity = scopeIdentity ?? string.Empty;
            _scope = scope;
        }

        // ================================================================
        // Properties
        // ================================================================

        /// <summary>Scope の安定 ID</summary>
        public string ScopeIdentity => _scopeIdentity;

        /// <summary>Save 対象として有効か（ScopeIdentity が設定されているか）</summary>
        public bool IsSaveEnabled => !string.IsNullOrEmpty(_scopeIdentity);

        /// <summary>
        /// ScopeIdentity を設定（後から設定可能）
        /// </summary>
        public void SetScopeIdentity(string scopeIdentity)
        {
            _scopeIdentity = scopeIdentity ?? string.Empty;
        }

        // ================================================================
        // IScopeBindingRegistry — Definition-centric API
        // ================================================================

        /// <inheritdoc/>
        public int Version => _version;

        /// <inheritdoc/>
        public void SetProfileDefinition(IProfileDefinition profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var type = profile.ProfileType ?? profile.GetType();
            var runtime = new ProfileDefinitionRuntime(profile, ++_version);
            // 再適用時に全定義を流し直すため、型重複でも捨てずに積む。
            _profiles.Add(runtime);
            // 型解決は従来通り「その型の最新定義」を返す。
            _latestProfileByType[type] = runtime;

            if (_blackboard != null || _scalar != null)
            {
                ApplyBindingsInternal(runtime, profile, _blackboard, _scalar);
            }
        }

        /// <inheritdoc/>
        public bool HasDefinition(Type profileType)
        {
            if (profileType == null)
                throw new ArgumentNullException(nameof(profileType));

            return _latestProfileByType.ContainsKey(profileType);
        }

        /// <inheritdoc/>
        public bool TryResolveDefinition(Type profileType, out IProfileDefinition profile)
        {
            if (_latestProfileByType.TryGetValue(profileType, out var runtime))
            {
                profile = runtime.Profile;
                return profile != null;
            }

            profile = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryResolveDefinition<T>(out T profile) where T : class, IProfileDefinition
        {
            if (_latestProfileByType.TryGetValue(typeof(T), out var runtime) && runtime.Profile is T typed)
            {
                profile = typed;
                return true;
            }

            profile = null;
            return false;
        }

        // ================================================================
        // Extended API
        // ================================================================

        /// <summary>
        /// 型を指定せずに Profile Runtime を取得。
        /// </summary>
        public bool TryGetRuntime(Type type, out IProfileRuntime runtime)
        {
            return _latestProfileByType.TryGetValue(type, out runtime);
        }

        /// <summary>
        /// 登録されている全 Profile を列挙。
        /// </summary>
        public IEnumerable<IProfileRuntime> EnumerateProfiles()
        {
            return _profiles;
        }

        /// <summary>
        /// 登録されている Profile の数。
        /// </summary>
        public int ProfileCount => _profiles.Count;

        /// <summary>
        /// 登録済み Profile のバインディングを全て再適用する。
        /// </summary>
        public void ReapplyAllBindings()
        {
            if (_blackboard == null && _scalar == null)
                return;

            for (var i = 0; i < _profiles.Count; i++)
            {
                var runtime = _profiles[i];
                var definition = runtime?.Profile;
                if (definition != null)
                    ApplyBindingsInternal(runtime, definition, _blackboard, _scalar);
            }
        }

        /// <summary>
        /// 登録済み Profile を全てクリアする。
        /// RuntimeLifetimeScope の Pool 再利用時のリセットに使用する。
        /// </summary>
        public void ClearAllProfiles(bool resetVersion = true)
        {
            _profiles.Clear();
            _latestProfileByType.Clear();
            if (resetVersion)
                _version = 0;
        }

        /// <summary>
        /// 外部からバインディングを適用する。
        /// ScopeBindingRegistryMB など、遅延インジェクションを行う場合に使用。
        /// </summary>
        public void ApplyBindings(IProfileRuntime runtime, IProfileDefinition profile,
            IVarStore blackboard, IBaseScalarService scalar)
        {
            if (runtime == null || profile == null)
                return;

            ApplyBindingsInternal(runtime, profile, blackboard, scalar);
        }



        // ================================================================
        // Internal
        // ================================================================

        /// <summary>
        /// IProfileRuntime からバインディングを適用する（非ジェネリック版）
        /// </summary>
        void ApplyBindingsInternal(IProfileRuntime runtime, IProfileDefinition profile,
            IVarStore blackboard, IBaseScalarService scalar)
        {
            var bindings = ListPool<IProfileValueBinding>.Get();
            try
            {
                profile.CollectBindings(bindings);

                // SaveEntry をクリア（IProfileRuntime 経由）
                runtime.ClearSaveEntries();

                var profileTypeName = runtime.ProfileType.Name;
                bool bindingsAppliedSuccessfully = true;

                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];

                    if (!binding.HasAnyBinding)
                        continue;

                    // Blackboard vocabulary に対応する value seam への書き込み
                    if (blackboard != null && binding.BlackboardKey != 0)
                    {
                        binding.WriteToBlackboard(blackboard);
                    }

                    // SaveEntry を収集（ScopeIdentity が設定されている場合のみ）
                    if (IsSaveEnabled)
                    {
                        var saveEntries = ListPool<BindingSaveEntry>.Get();
                        try
                        {
                            binding.CollectSaveEntries(saveEntries, _scopeIdentity, profileTypeName);
                            foreach (var entry in saveEntries)
                            {
                                if (entry.IsValid)
                                {
                                    runtime.AddSaveEntry(entry);
                                }
                            }
                        }
                        finally
                        {
                            ListPool<BindingSaveEntry>.Release(saveEntries);
                        }
                    }
                    else
                    {
                        // If a binding requests save but ScopeIdentity isn't set, warn to help debugging.
                        if (binding.BlackboardSaveEnabled || binding.ScalarSaveEnabled)
                        {
                            Debug.LogWarning($"[ScopeBindingRegistryService] Profile '{profileTypeName}' has bindings requesting Save, but this registry's ScopeIdentity is empty. Save entries will not be collected unless ScopeIdentity is set (LTS identity) or saving in runtime scope is enabled.");
                        }
                    }
                }

                if (HasScalarBindingRequests(bindings))
                {
                    if (scalar == null)
                    {
                        bindingsAppliedSuccessfully = false;
                        var scopeLabel = DescribeScope(_scope);
                        Debug.LogError($"[ScopeBindingRegistryService] Scalar binding requires IBaseScalarService, but no scalar service exists in this LTS. scope={scopeLabel} profile='{profileTypeName}'");
                    }
                    else if (!TryApplyScalarDeclarations(bindings, profileTypeName, scalar, out var failureReason))
                    {
                        bindingsAppliedSuccessfully = false;
                        var scopeLabel = DescribeScope(_scope);
                        Debug.LogError($"[ScopeBindingRegistryService] Failed to apply scalar declarations. scope={scopeLabel} profile='{profileTypeName}' reason='{failureReason}'");
                    }
                }

                // Runtime の状態を更新
                runtime.SetBindingsApplied(bindingsAppliedSuccessfully);
            }
            finally
            {
                ListPool<IProfileValueBinding>.Release(bindings);
            }
        }

        bool TryApplyScalarDeclarations(
            List<IProfileValueBinding> bindings,
            string profileTypeName,
            IBaseScalarService scalar,
            out string failureReason)
        {
            if (!TryCreateScalarOwnerIdentity(out ScalarOwnerIdentity owner, out failureReason))
                return false;

            SourceLocationIR source = CreateScalarDeclarationSource(profileTypeName);
            if (!ProfileScalarDeclarationProjection.TryCreateScalarDeclarations(bindings, owner, profileTypeName, source, out ScalarDeclarationInput[] declarations, out failureReason))
                return false;

            if (scalar is IScalarRuntimeShell shell)
                return ScalarDeclarationRuntimeBridge.TryApplyDeclarations(shell, declarations, out failureReason);

            return ScalarDeclarationRuntimeBridge.TryApplyDeclarations(scalar, declarations, out failureReason);
        }

        bool TryCreateScalarOwnerIdentity(out ScalarOwnerIdentity owner, out string failureReason)
        {
            owner = default;

            if (_scope == null)
            {
                failureReason = "ScopeBindingRegistryService requires IScopeNode to project scalar declarations.";
                return false;
            }

            if (!TryMapScalarOwnerKind(_scope.Kind, out ScalarOwnerKind ownerKind))
            {
                failureReason = "ScopeBindingRegistryService cannot map scope kind '" + _scope.Kind + "' to a scalar owner kind.";
                return false;
            }

            string ownerIdValue = _scope.Identity?.Id;
            if (string.IsNullOrWhiteSpace(ownerIdValue))
                ownerIdValue = _scopeIdentity;

            if (string.IsNullOrWhiteSpace(ownerIdValue))
            {
                failureReason = "ScopeBindingRegistryService requires a non-empty scope identity to project scalar declarations.";
                return false;
            }

            owner = new ScalarOwnerIdentity(ownerKind, new ScalarOwnerId(ownerIdValue));
            failureReason = string.Empty;
            return true;
        }

        static bool HasScalarBindingRequests(List<IProfileValueBinding> bindings)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (ProfileScalarDeclarationProjection.HasRequestedScalarBinding(bindings[i]))
                    return true;
            }

            return false;
        }

        static bool TryMapScalarOwnerKind(LifetimeScopeKind scopeKind, out ScalarOwnerKind ownerKind)
        {
            switch (scopeKind)
            {
                case LifetimeScopeKind.Project:
                    ownerKind = ScalarOwnerKind.Application;
                    return true;
                case LifetimeScopeKind.Platform:
                    ownerKind = ScalarOwnerKind.Platform;
                    return true;
                case LifetimeScopeKind.Global:
                    ownerKind = ScalarOwnerKind.Global;
                    return true;
                case LifetimeScopeKind.Scene:
                    ownerKind = ScalarOwnerKind.Scene;
                    return true;
                case LifetimeScopeKind.Field:
                    ownerKind = ScalarOwnerKind.Field;
                    return true;
                case LifetimeScopeKind.Entity:
                    ownerKind = ScalarOwnerKind.Entity;
                    return true;
                case LifetimeScopeKind.UI:
                    ownerKind = ScalarOwnerKind.UI;
                    return true;
                case LifetimeScopeKind.UIElement:
                    ownerKind = ScalarOwnerKind.UIElement;
                    return true;
                case LifetimeScopeKind.Runtime:
                    ownerKind = ScalarOwnerKind.Runtime;
                    return true;
                default:
                    ownerKind = default;
                    return false;
            }
        }

        SourceLocationIR CreateScalarDeclarationSource(string profileTypeName)
        {
            string scopeLabel = DescribeScope(_scope);
            return new SourceLocationIR(
                new GeneratedSourceLocation(
                    nameof(ScopeBindingRegistryService),
                    profileTypeName + "@" + scopeLabel,
                    nameof(ApplyBindingsInternal)));
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "<null>";

            var identity = scope.Identity;
            if (identity != null && !string.IsNullOrEmpty(identity.Id))
                return $"{identity.Kind}:{identity.Id}";

            return scope.Kind.ToString();
        }



    }

}

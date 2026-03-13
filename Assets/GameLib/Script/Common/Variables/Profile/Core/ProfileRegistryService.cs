// Game.Profile.ProfileRegistryService.cs
//
// Profile Registry Service - Profile SO の型ベース登録とバインディング適用
// IProfileSaveProvider を実装し、SaveManager への SaveEntry 供給を担当

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
using Game;
using Game.Common;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Profile SO を型ベースで管理するレジストリサービス。
    /// 登録時に IProfileValueBinding を自動的に Blackboard/Scalar に適用する。
    /// Profile registry service. Save persistence responsibilities are removed from this service.
    /// </summary>
    public sealed class ProfileRegistryService : IProfileRegistry
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly object _setterLock = new();
        static readonly Dictionary<Type, Action<ProfileRegistryService, BaseProfileSO>> _settersByType = new();
        static readonly MethodInfo _setProfileGenericMethod = ResolveGenericSetProfileMethod();

        readonly Dictionary<Type, IProfileRuntime> _profiles = new();
        readonly IBlackboardService _blackboard;
        readonly IBaseScalarService _scalar;
        readonly IScopeNode _scope;
        string _scopeIdentity;

        int _version;

        // ================================================================
        // Constructor
        // ================================================================

        /// <summary>
        /// ProfileRegistryService を作成。
        /// </summary>
        /// <param name="blackboard">Blackboard サービス（null 可）</param>
        /// <param name="scalar">Scalar サービス（null 可）</param>
        /// <param name="scopeIdentity">Scope の安定 ID（LTSIdentityMB.id）。Save 対象にする場合は必須。</param>
        public ProfileRegistryService(
            IBlackboardService blackboard = null,
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
        // IProfileRegistry
        // ================================================================

        /// <inheritdoc/>
        public int Version => _version;

        /// <inheritdoc/>
        public void SetProfileDefinition(IProfileDefinition profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (profile is BaseProfileSO profileSo)
            {
                SetProfileSO(profileSo);
                return;
            }

            var type = profile.ProfileType ?? profile.GetType();
            var runtime = new ProfileDefinitionRuntime(profile, ++_version);
            _profiles[type] = runtime;

            if (_blackboard != null || _scalar != null)
            {
                ApplyBindingsInternal(runtime, profile, _blackboard, _scalar);
            }
        }

        /// <summary>
        /// ScriptableObject を型ベースで登録する（非ジェネリック版）。
        /// Pool / Template など、型が動的なケース向け。
        /// </summary>
        public void SetProfileSO(BaseProfileSO profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var type = profile.GetType();
            var setter = GetOrCreateSetter(type);
            setter(this, profile);
        }

        /// <inheritdoc/>
        public void SetProfileSO<T>(T profile) where T : BaseProfileSO
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var type = typeof(T);
            var runtime = new ProfileRuntime<T>(profile, ++_version);

            _profiles[type] = runtime;

            // BaseProfileSO の場合はバインディングと SaveEntry 収集を実行
            // 注意: コンストラクタで blackboard/scalar が設定されていない場合、バインディングは適用されない
            if ((_blackboard != null || _scalar != null))
            {
                ApplyBindingsAndCollectSaveEntries(profile, runtime, _blackboard, _scalar);
            }
        }

        /// <inheritdoc/>
        public bool TryResolve<T>(out T profile) where T : BaseProfileSO
        {
            if (_profiles.TryGetValue(typeof(T), out var runtime))
            {
                profile = runtime.Profile as T;
                return profile != null;
            }

            profile = default;
            return false;
        }

        /// <inheritdoc/>
        public bool HasProfile<T>() where T : BaseProfileSO
        {
            return _profiles.ContainsKey(typeof(T));
        }

        /// <inheritdoc/>
        public T Resolve<T>() where T : BaseProfileSO
        {
            return TryResolve<T>(out var profile) ? profile : null;
        }

        /// <inheritdoc/>
        public bool TryResolveDefinition(Type profileType, out IProfileDefinition profile)
        {
            if (_profiles.TryGetValue(profileType, out var runtime))
            {
                profile = runtime.Profile;
                return profile != null;
            }

            profile = null;
            return false;
        }

        // ================================================================
        // Extended API
        // ================================================================

        /// <summary>
        /// Profile Runtime 情報を取得。
        /// </summary>
        public bool TryGetRuntime<T>(out ProfileRuntime<T> runtime) where T : BaseProfileSO
        {
            if (_profiles.TryGetValue(typeof(T), out var r) && r is ProfileRuntime<T> typed)
            {
                runtime = typed;
                return true;
            }

            runtime = null;
            return false;
        }

        /// <summary>
        /// 型を指定せずに Profile Runtime を取得。
        /// </summary>
        public bool TryGetRuntime(Type type, out IProfileRuntime runtime)
        {
            return _profiles.TryGetValue(type, out runtime);
        }

        /// <summary>
        /// 登録されている全 Profile を列挙。
        /// </summary>
        public IEnumerable<IProfileRuntime> EnumerateProfiles()
        {
            return _profiles.Values;
        }

        /// <summary>
        /// 登録されている Profile の数。
        /// </summary>
        public int ProfileCount => _profiles.Count;

        /// <summary>
        /// 登録済み Profile（BaseProfileSO）のバインディングを全て再適用する。
        /// </summary>
        public void ReapplyAllBindings()
        {
            if (_blackboard == null && _scalar == null)
                return;

            foreach (var runtime in _profiles.Values)
            {
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
            if (resetVersion)
                _version = 0;
        }

        /// <summary>
        /// 指定した Profile のバインディングを再適用。
        /// </summary>
        public void ReapplyBindings<T>() where T : BaseProfileSO
        {
            if (!TryGetRuntime<T>(out var runtime))
                return;

            ApplyBindingsAndCollectSaveEntries(runtime.TypedProfile, runtime, _blackboard, _scalar);
        }

        /// <summary>
        /// 外部からバインディングを適用する。
        /// ProfileRegistryMB など、遅延インジェクションを行う場合に使用。
        /// </summary>
        /// <param name="runtime">適用対象の ProfileRuntime</param>
        /// <param name="profile">BaseProfileSO</param>
        /// <param name="blackboard">Blackboard サービス</param>
        /// <param name="scalar">Scalar サービス</param>
        public void ApplyBindings(IProfileRuntime runtime, IProfileDefinition profile,
            IBlackboardService blackboard, IBaseScalarService scalar)
        {
            if (runtime == null || profile == null)
                return;

            // IProfileRuntime から ProfileRuntime<T> を取得する必要がある
            // 内部メソッドを直接呼ぶ
            ApplyBindingsInternal(runtime, profile, blackboard, scalar);
        }



        // ================================================================
        // Internal
        // ================================================================

        /// <summary>
        /// IProfileRuntime からバインディングを適用する（非ジェネリック版）
        /// </summary>
        void ApplyBindingsInternal(IProfileRuntime runtime, IProfileDefinition profile,
            IBlackboardService blackboard, IBaseScalarService scalar)
        {
            var bindings = ListPool<IProfileValueBinding>.Get();
            try
            {
                profile.CollectBindings(bindings);

                // SaveEntry をクリア（IProfileRuntime 経由）
                runtime.ClearSaveEntries();

                var profileTypeName = runtime.ProfileType.Name;

                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];

                    if (!binding.HasAnyBinding)
                        continue;

                    // Blackboard への書き込み
                    if (blackboard != null && binding.BlackboardKey != 0)
                    {
                        binding.WriteToBlackboard(blackboard);
                    }

                    // Scalar への書き込み
                    if (scalar != null && binding.ScalarKey.Id != 0)
                    {
                        binding.WriteToScalar(scalar);
                    }

                    // SaveEntry を収集（ScopeIdentity が設定されている場合のみ）
                    if (IsSaveEnabled)
                    {
                        var saveEntries = ListPool<ProfileSaveEntry>.Get();
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
                            ListPool<ProfileSaveEntry>.Release(saveEntries);
                        }
                    }
                    else
                    {
                        // If a binding requests save but ScopeIdentity isn't set, warn to help debugging.
                        if (binding.BlackboardSaveEnabled || binding.ScalarSaveEnabled)
                        {
                            Debug.LogWarning($"[ProfileRegistryService] Profile '{profileTypeName}' has bindings requesting Save, but this registry's ScopeIdentity is empty. Save entries will not be collected unless ScopeIdentity is set (LTS identity) or saving in runtime scope is enabled.");
                        }
                    }
                }

                // Runtime の状態を更新
                runtime.SetBindingsApplied(true);
            }
            finally
            {
                ListPool<IProfileValueBinding>.Release(bindings);
            }
        }

        void ApplyBindingsAndCollectSaveEntries<T>(BaseProfileSO profile, ProfileRuntime<T> runtime,
            IBlackboardService blackboard, IBaseScalarService scalar) where T : BaseProfileSO
        {
            var bindings = ListPool<IProfileValueBinding>.Get();
            try
            {
                profile.CollectBindings(bindings);

                // SaveEntry をクリア
                runtime.ClearSaveEntries();

                var profileTypeName = typeof(T).Name;

                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];

                    if (!binding.HasAnyBinding)
                        continue;

                    // Blackboard への書き込み
                    if (blackboard != null && binding.BlackboardKey != 0)
                    {
                        binding.WriteToBlackboard(blackboard);
                    }

                    // Scalar への書き込み
                    if (scalar != null && binding.ScalarKey.Id != 0)
                    {
                        binding.WriteToScalar(scalar);
                    }

                    // SaveEntry を収集（ScopeIdentity が設定されている場合のみ）
                    if (IsSaveEnabled)
                    {
                        var saveEntries = ListPool<ProfileSaveEntry>.Get();
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
                            ListPool<ProfileSaveEntry>.Release(saveEntries);
                        }
                    }
                    else
                    {
                        // If a binding requests save but ScopeIdentity isn't set, warn to help debugging.
                        if (binding.BlackboardSaveEnabled || binding.ScalarSaveEnabled)
                        {
                            Debug.LogWarning($"[ProfileRegistryService] Profile '{profileTypeName}' has bindings requesting Save, but this registry's ScopeIdentity is empty. Save entries will not be collected unless ScopeIdentity is set (LTS identity) or saving in runtime scope is enabled.");
                        }
                    }
                }

                // Runtime の状態を更新
                runtime.IsBindingsApplied = true;
            }
            finally
            {
                ListPool<IProfileValueBinding>.Release(bindings);
            }
        }

        static Action<ProfileRegistryService, BaseProfileSO> GetOrCreateSetter(Type profileType)
        {
            if (profileType == null)
                throw new ArgumentNullException(nameof(profileType));

            lock (_setterLock)
            {
                if (_settersByType.TryGetValue(profileType, out var cached))
                    return cached;

                var closed = _setProfileGenericMethod.MakeGenericMethod(profileType);
                Action<ProfileRegistryService, BaseProfileSO> setter =
                    (svc, so) => closed.Invoke(svc, new object[] { so });

                _settersByType[profileType] = setter;
                return setter;
            }
        }

        static MethodInfo ResolveGenericSetProfileMethod()
        {
            // public void SetProfileSO<T>(T profile) where T : BaseProfileSO
            // Note: There is also a non-generic overload, so resolve by IsGenericMethodDefinition.
            var methods = typeof(ProfileRegistryService).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != nameof(SetProfileSO))
                    continue;
                if (!m.IsGenericMethodDefinition)
                    continue;

                var ps = m.GetParameters();
                if (ps.Length != 1)
                    continue;

                // Parameter should be the generic T (not the non-generic overload).
                if (!ps[0].ParameterType.IsGenericParameter)
                    continue;

                return m;
            }

            throw new InvalidOperationException("Cannot resolve ProfileRegistryService.SetProfileSO<T>(T) method.");
        }


    }

}

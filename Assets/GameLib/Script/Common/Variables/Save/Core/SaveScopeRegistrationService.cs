#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using VContainer;

namespace Game.Save
{
    public sealed class SaveScopeRegistrationService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly ProfileRegistryService? _profiles;
        readonly IBlackboardService? _blackboard;
        readonly IBaseScalarService? _scalars;
        readonly IScopeNode _owner;

        IDisposable? _handle;

        public SaveScopeRegistrationService(
            ProfileRegistryService? profiles,
            IBlackboardService? blackboard,
            IBaseScalarService? scalars,
            IScopeNode owner)
        {
            _profiles = profiles;
            _blackboard = blackboard;
            _scalars = scalars;
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (scope == null || scope != _owner)
                return;

            if (scope.Kind == LifetimeScopeKind.Runtime)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;
            if (!resolver.TryResolve<ISaveManager>(out var save) || save == null)
                return;

            EnsureProfileScopeIdentity(scope);

            // ProfileRegistry がある場合のみ登録
            // ProfileRegistry がないスコープは Save 対象ではないため登録しない
            if (_profiles == null || string.IsNullOrEmpty(_profiles.ScopeIdentity))
            {
                Debug.LogWarning("[SaveScopeRegistrationService] Skip registration: ProfileRegistry or ScopeIdentity is missing.");
                return;
            }

            var id = _profiles.ScopeIdentity;
            if (string.IsNullOrEmpty(id) || !SaveKeys.TryValidateSegment(id, out _))
            {
                Debug.LogWarning($"[SaveScopeRegistrationService] Skip registration: invalid ScopeIdentity '{id}'.");
                return;
            }

            var scopeKey = new ScopeKey(scope.Kind, id);
            var planSource = new ProfileRegistryPlanSource(_profiles, scopeKey);
            var reg = new SaveScopeRegistration(scopeKey, planSource, _profiles, _blackboard, _scalars);

            _handle?.Dispose();
            _handle = save.RegisterScope(in reg);

            // Profile bindings write initial values to Blackboard/Scalar.
            // Ensure that work is complete before applying persisted payloads.
            _profiles.ReapplyAllBindings();

            // Registration 完了直後に永続レイヤーの Load を実行
            LoadInitialState(save, scopeKey);
        }

        static readonly SaveLayer[] PersistentLayers = new[]
        {
            SaveLayer.Global,
            SaveLayer.SystemSetting,
            SaveLayer.Profile,
            SaveLayer.GameLogic
        };

        void LoadInitialState(ISaveManager save, ScopeKey scopeKey)
        {
            foreach (var layer in PersistentLayers)
            {
                var ctx = new SaveContext(save.ActiveProfileId, layer, scopeKey);
                var result = save.Load(in ctx);

                if (!result.IsSuccess && !result.IsNoData)
                {
                    Debug.LogWarning($"[SaveScopeRegistrationService] Initial load failed | Scope={scopeKey} | Layer={layer} | Error={result.Error} | Message={result.Message}");
                }
            }
        }

        void EnsureProfileScopeIdentity(IScopeNode scope)
        {
            if (_profiles == null || !string.IsNullOrEmpty(_profiles.ScopeIdentity))
                return;

            var id = scope.Identity?.Id ?? string.Empty;
            if (string.IsNullOrEmpty(id) || !SaveKeys.TryValidateSegment(id, out _))
                return;

            _profiles.SetScopeIdentity(id);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _handle?.Dispose();
            _handle = null;
        }
    }

    /// <summary>
    /// Minimal ISavePlanSource that returns no plan.
    /// Used for scopes without ProfileRegistry.
    /// </summary>
    public sealed class EmptySavePlanSource : ISavePlanSource
    {
        readonly ScopeKey _scopeKey;

        public ScopeKey ScopeKey => _scopeKey;
        public int Version => 0;

        public EmptySavePlanSource(ScopeKey scopeKey)
        {
            _scopeKey = scopeKey;
        }

        public void CollectEntries(SaveLayer layer, List<SaveEntry> dest)
        {
            // No entries - this scope has no save plan
        }
    }
}

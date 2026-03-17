using System;
using System.Collections.Generic;
using Game;
using Game.Save;
using VContainer;
using UnityEngine;

namespace Game.Profile
{
    /// <summary>
    /// ScopeBindingRegistryService の「どの Profile を登録するか」「Pool の Acquire/Release でどう振る舞うか」
    /// という運用ロジックを集約するサービス。
    /// </summary>
    public sealed class ScopeBindingRegistryInstallService :
        IScopeBindingRegistryConfigurator,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        public readonly struct Options
        {
            public readonly IProfileDefinition[] InspectorProfiles;
            public readonly bool ResetOnAcquire;
            public readonly bool ClearOnRelease;

            public Options(
                IProfileDefinition[] inspectorProfiles,
                bool resetOnAcquire,
                bool clearOnRelease)
            {
                InspectorProfiles = inspectorProfiles ?? Array.Empty<IProfileDefinition>();
                ResetOnAcquire = resetOnAcquire;
                ClearOnRelease = clearOnRelease;
            }
        }

        readonly ScopeBindingRegistryService _registry;
        readonly IScopeNode _scope;
        readonly Options _options;

        readonly List<IProfileDefinition> _externalDefinitions = new();

        public ScopeBindingRegistryInstallService(ScopeBindingRegistryService registry, IScopeNode scope, Options options)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _options = options;
        }

        public void InstallInitialIfNeeded()
        {
            if (_scope.Kind == LifetimeScopeKind.Runtime)
                return;

            RegisterSelectedProfiles();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // Ensure registry scope identity is set if available when the scope is acquired.
            // This covers cases where identity wasn't available at initial install time but appears on Acquire.
            if (string.IsNullOrEmpty(_registry.ScopeIdentity) && scope?.Identity != null)
            {
                var id = scope.Identity.Id ?? string.Empty;
                if (!string.IsNullOrEmpty(id))
                {
                    _registry.SetScopeIdentity(id);
                    //Debug.Log($"[ScopeBindingRegistryInstallService] Set ProfileRegistry ScopeIdentity to '{id}' on acquire.");
                    // Reapply bindings so SaveEntries are collected using the new scope id.
                    _registry.ReapplyAllBindings();
                }
            }

            if (!isReset || !_options.ResetOnAcquire)
                return;

            _registry.ClearAllProfiles(resetVersion: true);
            RegisterSelectedProfiles();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!isReset || !_options.ClearOnRelease)
                return;

            _registry.ClearAllProfiles(resetVersion: true);
        }

        public void ClearExternalProfiles(bool applyImmediately = false)
        {
            _externalDefinitions.Clear();
            if (applyImmediately)
                ApplyProfilesImmediately();
        }

        public void SetExternalProfileDefinitions(IReadOnlyList<IProfileDefinition> profiles, bool applyImmediately = false)
        {
            _externalDefinitions.Clear();
            AddDefinitionsToExternalList(profiles);

            if (applyImmediately)
                ApplyProfilesImmediately();
        }

        public void AddExternalProfileDefinition(IProfileDefinition profile, bool applyImmediately = false)
        {
            if (profile != null && !_externalDefinitions.Contains(profile))
                _externalDefinitions.Add(profile);

            if (applyImmediately)
                ApplyProfilesImmediately();
        }

        void ApplyProfilesImmediately()
        {
            _registry.ClearAllProfiles(resetVersion: true);
            RegisterSelectedProfiles();
        }

        void RegisterSelectedProfiles()
        {
            if (_externalDefinitions.Count > 0)
            {
                for (int i = 0; i < _externalDefinitions.Count; i++)
                {
                    var p = _externalDefinitions[i];
                    if (p != null)
                        _registry.SetProfileDefinition(p);
                }
                return;
            }

            var list = _options.InspectorProfiles;
            for (int i = 0; i < list.Length; i++)
            {
                var p = list[i];
                if (p != null)
                    _registry.SetProfileDefinition(p);
            }
        }

        void AddDefinitionsToExternalList(IReadOnlyList<IProfileDefinition> profiles)
        {
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                if (p == null)
                    continue;
                if (!_externalDefinitions.Contains(p))
                    _externalDefinitions.Add(p);
            }
        }
    }
}

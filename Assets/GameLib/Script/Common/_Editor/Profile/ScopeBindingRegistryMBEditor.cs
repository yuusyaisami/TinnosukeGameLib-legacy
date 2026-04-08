#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Common;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Profile.Editor
{
    [CustomEditor(typeof(ScopeBindingRegistryMB))]
    public sealed class ScopeBindingRegistryMBEditor : OdinEditor
    {
        const string ProfilesFieldName = "_profiles";

        static readonly FieldInfo? ProfilesFieldInfo = typeof(ScopeBindingRegistryMB)
            .GetField(ProfilesFieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        string _searchText = string.Empty;
        bool _showFullProfilesList = true;
        readonly List<int> _matchedIndices = new();
        readonly List<string> _matchedLabels = new();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tree = Tree;
            tree.UpdateTree();

            for (var i = 0; i < tree.RootProperty.Children.Count; i++)
            {
                var child = tree.RootProperty.Children[i];
                if (child == null)
                    continue;

                if (child.Name == ProfilesFieldName)
                {
                    DrawProfilesSection(child);
                    continue;
                }

                child.Draw();
            }

            tree.ApplyChanges();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawProfilesSection(InspectorProperty profilesProperty)
        {
            if (profilesProperty == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Profiles Search", EditorStyles.boldLabel);
                DrawSearchToolbar();
                RebuildMatches();

                var totalCount = GetProfilesCount(profilesProperty);
                EditorGUILayout.LabelField($"Matched {_matchedIndices.Count} / {totalCount}", EditorStyles.miniLabel);

                var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
                if (hasQuery && _matchedIndices.Count == 0)
                    EditorGUILayout.HelpBox("No profiles matched the current query.", MessageType.Info);

                if (hasQuery)
                {
                    _showFullProfilesList = EditorGUILayout.ToggleLeft("Show Full Profiles List", _showFullProfilesList);
                    if (!_showFullProfilesList)
                        DrawFilteredEntries(profilesProperty);
                }
                else
                {
                    _showFullProfilesList = true;
                }
            }

            if (_showFullProfilesList || string.IsNullOrWhiteSpace(_searchText))
            {
                profilesProperty.Draw();
            }
        }

        void DrawSearchToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Query", GUILayout.Width(44f));

                var next = EditorGUILayout.TextField(_searchText ?? string.Empty);
                if (!string.Equals(next, _searchText, StringComparison.Ordinal))
                    _searchText = next;

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_searchText)))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(56f)))
                        _searchText = string.Empty;
                }
            }
        }

        void DrawFilteredEntries(InspectorProperty profilesProperty)
        {
            if (_matchedIndices.Count <= 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Filtered Results", EditorStyles.boldLabel);

            for (var i = 0; i < _matchedIndices.Count; i++)
            {
                var index = _matchedIndices[i];
                if (index < 0 || index >= profilesProperty.Children.Count)
                    continue;

                var element = profilesProperty.Children[index];
                if (element == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var header = i < _matchedLabels.Count ? _matchedLabels[i] : $"[{index}]";
                    EditorGUILayout.LabelField(header, EditorStyles.miniBoldLabel);
                    element.Draw();
                }
            }
        }

        int GetProfilesCount(InspectorProperty profilesProperty)
        {
            var profiles = TryGetProfiles();
            if (profiles != null)
                return profiles.Count;

            return profilesProperty?.Children?.Count ?? 0;
        }

        void RebuildMatches()
        {
            _matchedIndices.Clear();
            _matchedLabels.Clear();

            var profiles = TryGetProfiles();
            if (profiles == null)
                return;

            var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
            var query = _searchText?.Trim() ?? string.Empty;

            for (var i = 0; i < profiles.Count; i++)
            {
                var value = profiles[i];
                var label = BuildProfileLabel(value, i);
                if (!hasQuery || IsMatch(value, label, query))
                {
                    _matchedIndices.Add(i);
                    _matchedLabels.Add(label);
                }
            }
        }

        List<DynamicValue<BaseProfileData>>? TryGetProfiles()
        {
            if (target is not ScopeBindingRegistryMB registry || registry == null)
                return null;

            if (targets == null || targets.Length != 1)
                return null;

            if (ProfilesFieldInfo == null)
                return null;

            return ProfilesFieldInfo.GetValue(registry) as List<DynamicValue<BaseProfileData>>;
        }

        static bool IsMatch(DynamicValue<BaseProfileData> value, string summaryLabel, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            if (ContainsIgnoreCase(summaryLabel, query))
                return true;

            if (ContainsIgnoreCase(value.SourceTypeName, query))
                return true;

            if (ContainsIgnoreCase(value.SourceDebugData, query))
                return true;

            if (!TryResolveProfileDefinition(value, out var profile) || profile == null)
                return false;

            if (ContainsIgnoreCase(GetProfileDisplayName(profile), query))
                return true;

            if (ContainsIgnoreCase(profile.GetType().Name, query))
                return true;

            foreach (var binding in profile.EnumerateBindings())
            {
                if (binding == null)
                    continue;

                if (ContainsIgnoreCase(BuildBindingToken(binding), query))
                    return true;
            }

            return false;
        }

        static bool ContainsIgnoreCase(string? value, string query)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string BuildProfileLabel(DynamicValue<BaseProfileData> value, int index)
        {
            var sourceType = value.SourceTypeName;
            if (!value.HasSource)
                return $"[{index}] None";

            if (TryResolveProfileDefinition(value, out var profile) && profile != null)
            {
                var profileName = GetProfileDisplayName(profile);
                var profileType = profile.GetType().Name;
                var bindingCount = profile.GetBindingCount();
                var preview = BuildBindingPreview(profile, 2);

                if (string.IsNullOrEmpty(preview))
                    return $"[{index}] {sourceType} | {profileName} ({profileType}) | Bindings:{bindingCount}";

                return $"[{index}] {sourceType} | {profileName} ({profileType}) | Bindings:{bindingCount} | {preview}";
            }

            var debugData = value.SourceDebugData;
            if (string.IsNullOrEmpty(debugData))
                debugData = "(unresolved)";

            return $"[{index}] {sourceType} | {Trim(debugData, 80)}";
        }

        static bool TryResolveProfileDefinition(DynamicValue<BaseProfileData> value, out BaseProfileData? profile)
        {
            profile = null;

            if (!value.HasSource)
                return false;

            var sourceType = value.SourceTypeName;
            if (!string.Equals(sourceType, "Literal", StringComparison.Ordinal)
                && !string.Equals(sourceType, "Asset", StringComparison.Ordinal))
            {
                return false;
            }

            return value.TryGet(null, out profile) && profile != null;
        }

        static string GetProfileDisplayName(BaseProfileData profile)
        {
            if (profile is CustomProfileDefinition custom && !string.IsNullOrEmpty(custom.ProfileName))
                return custom.ProfileName;

            var text = profile.ToString();
            if (!string.IsNullOrEmpty(text)
                && !string.Equals(text, profile.GetType().Name, StringComparison.Ordinal)
                && !string.Equals(text, profile.GetType().FullName, StringComparison.Ordinal))
            {
                return text;
            }

            return profile.GetType().Name;
        }

        static string BuildBindingPreview(BaseProfileData profile, int maxCount)
        {
            var parts = new List<string>(maxCount);
            var total = 0;

            foreach (var binding in profile.EnumerateBindings())
            {
                if (binding == null)
                    continue;

                total++;
                if (parts.Count < maxCount)
                    parts.Add(BuildBindingToken(binding));
            }

            if (parts.Count == 0)
                return string.Empty;

            if (total > parts.Count)
                parts.Add("...");

            return string.Join(", ", parts);
        }

        static string BuildBindingToken(IProfileValueBinding binding)
        {
            if (binding == null)
                return "null";

            var typeName = binding.GetType().Name;
            var scalarLabel = binding.ScalarKey.Id != 0 ? GetLeafLabel(binding.ScalarKey.Name) : string.Empty;
            var blackboardLabel = binding.BlackboardKey != 0 ? GetBlackboardLabel(binding.BlackboardKey) : string.Empty;

            if (!string.IsNullOrEmpty(scalarLabel) && !string.IsNullOrEmpty(blackboardLabel))
                return $"{typeName}:{scalarLabel}/{blackboardLabel}";

            if (!string.IsNullOrEmpty(scalarLabel))
                return $"{typeName}:{scalarLabel}";

            if (!string.IsNullOrEmpty(blackboardLabel))
                return $"{typeName}:{blackboardLabel}";

            return typeName;
        }

        static string GetBlackboardLabel(int varId)
        {
            if (VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
                return GetLeafLabel(stableKey);

            return $"varId:{varId}";
        }

        static string GetLeafLabel(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return "Unbound";

            var lastDot = key.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < key.Length)
                return key.Substring(lastDot + 1);

            var lastSlash = key.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < key.Length)
                return key.Substring(lastSlash + 1);

            return key;
        }

        static string Trim(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var singleLine = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine.Substring(0, maxLength - 3) + "...";
        }
    }
}
#endif

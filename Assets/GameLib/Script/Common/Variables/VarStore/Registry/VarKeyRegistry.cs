#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Registry;
using UnityEngine;

namespace Game.VarStoreKeys
{
    /// <summary>
    /// VarStore 用のキー（stableKey→varId）を管理する階層レジストリ。
    ///
    /// 重要:
    /// - stableKey は新規ノードではノード名（Name）で初期生成されるが、以後は自動更新しない
    /// - stableKey の変更は alias 方式（旧 stableKey を aliases に残す）
    /// - 削除しても varId/キーは tombstone として予約し、再利用しない
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/Variables/Var Key Registry")]
    public sealed class VarKeyRegistry : HierarchyRegistryBase<VarKeyNode>, IVarKeyRegistry
    {
        readonly struct BuiltInVarKey
        {
            public readonly int VarId;
            public readonly string StableKey;

            public BuiltInVarKey(int varId, string stableKey)
            {
                VarId = varId;
                StableKey = stableKey ?? string.Empty;
            }
        }

        static readonly BuiltInVarKey[] s_builtInKeys =
        {
            new(Game.Vars.Generated.VarIds.GameLib.Base.PointerRelation.isSelf, "GameLib.Base.PointerRelation.isSelf"),
            new(Game.Vars.Generated.VarIds.GameLib.Base.PointerRelation.isSelfOrDescendant, "GameLib.Base.PointerRelation.isSelfOrDescendant"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.HoldTime, "HoldTime"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.HoldProgress, "HoldProgress"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.State, "UIButton.ShortLong.State"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.ShortProgress, "UIButton.ShortLong.ShortProgress"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.LongProgress, "UIButton.ShortLong.LongProgress"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.IsLong, "UIButton.ShortLong.IsLong"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.IsLongMax, "UIButton.ShortLong.IsLongMax"),
            new(Game.Vars.Generated.VarIds.GameLib.UI.Button.ShortLong.LongMaxTime, "UIButton.ShortLong.LongMaxTime"),
        };

        [Serializable]
        sealed class Tombstone
        {
            [SerializeField] int varId;
            [SerializeField] string stableKey = string.Empty;
            [SerializeField] List<string> aliases = new();

            public int VarId => varId;
            public string StableKey => stableKey;
            public IReadOnlyList<string> Aliases => aliases;

            public Tombstone(int varId, string stableKey, List<string> aliases)
            {
                this.varId = varId;
                this.stableKey = stableKey ?? string.Empty;
                this.aliases = aliases != null ? new List<string>(aliases) : new List<string>();
            }
        }

        [SerializeField] int nextVarId = 1;
        [SerializeField] List<Tombstone> tombstones = new();

        readonly Dictionary<string, int> _keyToId = new(StringComparer.Ordinal);
        readonly Dictionary<int, string> _idToKey = new();
        bool _built;
        bool _isBuilding;
#if UNITY_EDITOR
        const bool EnableEditorDiagnostics = false;
#endif

        public override string GetKeyString(VarKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            // 後方互換:
            // 過去のデータ（または外部インポート）で StableKey が未設定のケースがあり得る。
            // その場合は Name を暫定キーとして返す（ただし Name は rename により変わり得るため、最終的には StableKey を必ず設定する）。
            return !string.IsNullOrEmpty(node.StableKey) ? node.StableKey : (node.Name ?? string.Empty);
        }

        public bool TryResolve(string stableKeyOrAlias, out int varId)
        {
            EnsureLookup();
            if (string.IsNullOrEmpty(stableKeyOrAlias))
            {
                varId = 0;
                return false;
            }
            return _keyToId.TryGetValue(stableKeyOrAlias, out varId) && varId > 0;
        }

        /// <summary>
        /// Number of registered stable keys and aliases available in this registry (editor/runtime lookup).
        /// </summary>
        public int RegisteredKeyCount
        {
            get { EnsureLookup(); return _keyToId.Count; }
        }

        public bool TryGetStableKey(int varId, out string stableKey)
        {
            EnsureLookup();
            if (varId <= 0 || !_idToKey.TryGetValue(varId, out stableKey))
            {
                stableKey = string.Empty;
                return false;
            }
            return !string.IsNullOrEmpty(stableKey);
        }

        protected override void InitializeLeafNode(VarKeyNode node)
        {
            base.InitializeLeafNode(node);

            if (node == null)
                return;

            if (node.VarId <= 0)
            {
                node.VarId = AllocateNewVarId();
            }

            if (string.IsNullOrEmpty(node.StableKey))
            {
                // 初期生成: フォルダでないノードはノード名そのものを stableKey とする（基本同期されます）。
                // 以後 rename/move があっても stableKey は自動更新しない（資産互換優先）。
                node.StableKey = node.Name ?? string.Empty;
            }

            _built = false;
        }

        public void EnsureLookupRebuild() => _built = false;

        /// <summary>
        /// Returns true while EnsureLookup is actively building the internal lookup tables.
        /// Used for diagnostics to avoid false-negative empty checks during build.
        /// </summary>
        public bool IsLookupBuilding => _isBuilding;

        protected override bool OnDeleteNode(VarKeyNode node)
        {
            if (node == null)
                return true;

            if (node.IsFolder)
                return true;

            // tombstone に退避して予約（欠番/キー再利用禁止）
            if (node.VarId > 0 && !string.IsNullOrEmpty(node.StableKey))
            {
                tombstones ??= new List<Tombstone>();
                tombstones.Add(new Tombstone(node.VarId, node.StableKey, node.Aliases));
            }

            _built = false;
            return true; // remove from nodes
        }

        int AllocateNewVarId()
        {
            int maxId = 0;
            foreach (var n in nodes)
            {
                if (n != null && !n.IsFolder && n.VarId > maxId)
                    maxId = n.VarId;
            }
            if (tombstones != null)
            {
                for (int i = 0; i < tombstones.Count; i++)
                {
                    var t = tombstones[i];
                    if (t != null && t.VarId > maxId)
                        maxId = t.VarId;
                }
            }

            if (nextVarId <= maxId)
                nextVarId = maxId + 1;

            return nextVarId++;
        }

        void EnsureLookup()
        {
            if (_built)
                return;

            if (_isBuilding)
                return; // Guard against reentrancy: if we're already building, don't start again.

            _isBuilding = true;

            _keyToId.Clear();
            _idToKey.Clear();

            BuildFromNodes();
            BuildFromBuiltInKeys();
            BuildFromTombstones();

            _built = true;
            _isBuilding = false;

        }

        void BuildFromNodes()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsFolder)
                    continue;

                var stableKey = node.StableKey;
                if (string.IsNullOrEmpty(stableKey))
                    stableKey = node.Name;

                if (node.VarId <= 0 || string.IsNullOrEmpty(stableKey))
                    continue;

                var effectiveStableKey = AddStableKeyWithPathFallback(node, stableKey, node.VarId);
                if (string.IsNullOrEmpty(effectiveStableKey))
                    continue;

                var aliases = node.Aliases;
                if (aliases != null)
                {
                    for (int a = 0; a < aliases.Count; a++)
                    {
                        var alias = aliases[a];
                        if (string.IsNullOrEmpty(alias))
                            continue;
                        AddAliasKey(alias, node.VarId);
                    }
                }

                if (!_idToKey.ContainsKey(node.VarId))
                    _idToKey.Add(node.VarId, effectiveStableKey);
            }
        }

        void BuildFromTombstones()
        {
            if (tombstones == null)
                return;

            for (int i = 0; i < tombstones.Count; i++)
            {
                var t = tombstones[i];
                if (t == null)
                    continue;

                if (t.VarId <= 0 || string.IsNullOrEmpty(t.StableKey))
                    continue;

                AddKey(t.StableKey, t.VarId, isReserved: true);

                var aliases = t.Aliases;
                if (aliases == null)
                    continue;
                for (int a = 0; a < aliases.Count; a++)
                {
                    var alias = aliases[a];
                    if (string.IsNullOrEmpty(alias))
                        continue;
                    AddKey(alias, t.VarId, isReserved: true);
                }
            }
        }

        void BuildFromBuiltInKeys()
        {
            for (int i = 0; i < s_builtInKeys.Length; i++)
            {
                var builtIn = s_builtInKeys[i];
                if (builtIn.VarId <= 0 || string.IsNullOrEmpty(builtIn.StableKey))
                    continue;

                if (!_keyToId.ContainsKey(builtIn.StableKey))
                {
                    _keyToId.Add(builtIn.StableKey, builtIn.VarId);
                }

                if (!_idToKey.ContainsKey(builtIn.VarId))
                {
                    _idToKey.Add(builtIn.VarId, builtIn.StableKey);
                }
            }
        }

        void AddKey(string key, int varId, bool isReserved)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (_keyToId.TryGetValue(key, out var existing))
            {
                if (existing == varId)
                    return;

                if (!isReserved)
                    Debug.LogError($"[VarKeyRegistry] Duplicate stableKey/alias: '{key}' ({existing} vs {varId})");
                return;
            }

            _keyToId.Add(key, isReserved ? 0 : varId);
        }

        string AddStableKeyWithPathFallback(VarKeyNode node, string stableKey, int varId)
        {
            if (string.IsNullOrEmpty(stableKey))
                return string.Empty;

            if (!_keyToId.TryGetValue(stableKey, out var existing))
            {
                _keyToId.Add(stableKey, varId);
                return stableKey;
            }

            if (existing == varId)
                return stableKey;

            var pathKey = BuildPathStableKey(node);
            if (!string.IsNullOrEmpty(pathKey))
            {
                if (!_keyToId.TryGetValue(pathKey, out existing))
                {
                    _keyToId.Add(pathKey, varId);
#if UNITY_EDITOR
                    // Persist as full path key to avoid recurring collisions by leaf-name keys.
                    if (!string.Equals(node.StableKey, pathKey, StringComparison.Ordinal))
                    {
                        if (!string.IsNullOrEmpty(stableKey))
                            AddAliasIfMissing(node, stableKey);
                        node.StableKey = pathKey;
                        MarkDirty();
                    }
#endif
                    return pathKey;
                }

                if (existing == varId)
                    return pathKey;
            }

            Debug.LogError($"[VarKeyRegistry] Duplicate stableKey: '{stableKey}' ({existing} vs {varId})");
            return string.Empty;
        }

        void AddAliasKey(string alias, int varId)
        {
            if (string.IsNullOrEmpty(alias))
                return;

            if (_keyToId.TryGetValue(alias, out var existing))
            {
                if (existing == varId)
                    return;
                // Alias collision is non-fatal. Keep first owner.
                return;
            }

            _keyToId.Add(alias, varId);
        }

        string BuildPathStableKey(VarKeyNode node)
        {
            if (node == null)
                return string.Empty;

            var path = GetDisplayPath(node.Id);
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('/', '.');
        }

#if UNITY_EDITOR
        static void AddAliasIfMissing(VarKeyNode node, string alias)
        {
            if (node == null || string.IsNullOrEmpty(alias))
                return;

            var aliases = node.Aliases;
            if (aliases == null)
                return;

            for (int i = 0; i < aliases.Count; i++)
            {
                if (string.Equals(aliases[i], alias, StringComparison.Ordinal))
                    return;
            }

            aliases.Add(alias);
        }
#endif

        void OnEnable()
        {
            // When the asset is deserialized/loaded we want to ensure lookup is built immediately.
            // This avoids cases where the asset is present but the internal lookup dictionaries
            // are not yet populated when other systems call into VarIdResolver.
            _built = false;
            try
            {
                EnsureLookup();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

#if UNITY_EDITOR
        [Header("CSV Import/Export")]
        [Tooltip("CSV フォーマット: Type,Path,StableKey,Aliases,Description")]
        [SerializeField] TextAsset? csvFile;

        protected override void OnCsvRowImport(VarKeyNode node, CsvImportRowData rowData, bool isNew)
        {
            base.OnCsvRowImport(node, rowData, isNew);

            if (node == null || node.IsFolder)
                return;

            var cols = rowData.RawColumns;

            // Column 2: StableKey
            if (cols.Length > 2 && !string.IsNullOrWhiteSpace(cols[2]))
            {
                var newStable = cols[2].Trim();
                if (!string.IsNullOrEmpty(newStable) && newStable != node.StableKey)
                {
                    if (!string.IsNullOrEmpty(node.StableKey))
                        node.Aliases.Add(node.StableKey);
                    node.StableKey = newStable;
                }
            }

            // Column 3: Aliases (split by '|')
            if (cols.Length > 3 && !string.IsNullOrWhiteSpace(cols[3]))
            {
                node.Aliases.Clear();
                var raw = cols[3].Trim();
                var parts = raw.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i]?.Trim();
                    if (!string.IsNullOrEmpty(p) && p != node.StableKey)
                        node.Aliases.Add(p);
                }
            }

            // Column 4: Description
            if (cols.Length > 4 && !string.IsNullOrWhiteSpace(cols[4]))
            {
                node.Description = cols[4].Trim();
            }

            _built = false;
        }

        [ContextMenu("Import from CSV")]
        public void ImportFromCsvFile()
        {
            if (csvFile == null)
            {
                Debug.LogWarning("[VarKeyRegistry] CSV file is not assigned.");
                return;
            }

            ImportFromCsv(csvFile.text, hasHeader: true);
            Debug.Log($"[VarKeyRegistry] Imported CSV: {csvFile.name}");
        }

        public string ExportToCsv(bool includeHeader = true)
        {
            var sb = new System.Text.StringBuilder();
            if (includeHeader)
                sb.AppendLine("Type,Path,StableKey,VarId,Aliases,Description");

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null)
                    continue;

                var type = n.IsFolder ? "Folder" : "Leaf";
                var path = GetDisplayPath(n.Id);
                var stable = n.IsFolder ? "" : (n.StableKey ?? "");
                var id = n.IsFolder ? "" : n.VarId.ToString();
                var aliases = n.IsFolder ? "" : string.Join("|", n.Aliases ?? new List<string>());
                var desc = n.Description ?? "";

                sb.Append(EscapeCsv(type)).Append(',')
                  .Append(EscapeCsv(path)).Append(',')
                  .Append(EscapeCsv(stable)).Append(',')
                  .Append(EscapeCsv(id)).Append(',')
                  .Append(EscapeCsv(aliases)).Append(',')
                  .AppendLine(EscapeCsv(desc));
            }

            return sb.ToString();
        }

        static string EscapeCsv(string s)
        {
            s ??= string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
#endif
    }
}

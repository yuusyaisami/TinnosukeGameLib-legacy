#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed class SaveManagerDebugView
    {
        [FoldoutGroup("Bindings")]
        [ShowInInspector, ReadOnly]
        public bool IsBound => _save != null;

        [FoldoutGroup("Bindings")]
        [ShowInInspector, ReadOnly]
        public string BindStatus { get; private set; } = "(unbound)";

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public int ActiveProfileId { get; private set; }

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public string PlatformCaps { get; private set; } = "(unknown)";

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public int PlanCacheCount { get; private set; }

        [FoldoutGroup("Registrations")]
        [ShowInInspector, ReadOnly]
        public List<SaveScopeRegistrationInfo> Registrations { get; } = new();

        [FoldoutGroup("History")]
        [ShowInInspector, ReadOnly]
        public List<SaveHistoryRow> History { get; } = new();

        [FoldoutGroup("Persisted Files")]
        [ShowInInspector, ReadOnly]
        public List<PersistedFileRow> PersistedFiles { get; } = new();

        [FoldoutGroup("Build")]
        [LabelText("Delete All Save Data Before Build")]
        public bool DeleteAllSaveDataBeforeBuild;

        [FoldoutGroup("Payload Preview")]
        [LabelText("Profile Id")]
        public int previewProfileId;

        [FoldoutGroup("Payload Preview")]
        [LabelText("Scope Kind")]
        public Game.LifetimeScopeKind previewScopeKind;

        [FoldoutGroup("Payload Preview")]
        [LabelText("Scope Id")]
        public string previewScopeId = string.Empty;

        [FoldoutGroup("Payload Preview")]
        [LabelText("Layer")]
        public SaveLayer previewLayer;

        [FoldoutGroup("Payload Preview")]
        [ShowInInspector, ReadOnly]
        public string PreviewStatus { get; private set; } = "(idle)";

        [FoldoutGroup("Payload Preview")]
        [ShowInInspector, ReadOnly]
        public int PreviewBlackboardCount { get; private set; }

        [FoldoutGroup("Payload Preview")]
        [ShowInInspector, ReadOnly]
        public int PreviewScalarCount { get; private set; }

        [FoldoutGroup("Payload Preview")]
        [ShowInInspector, ReadOnly]
        public int PreviewBytesLength { get; private set; }

        [NonSerialized] ISaveManager? _save;
        [NonSerialized] ISaveManagerDebug? _debug;
        [NonSerialized] ISaveStore? _store;
        [NonSerialized] ISavePlatform? _platform;
        [NonSerialized] ISaveSerializer? _serializer;

#if UNITY_EDITOR
        [NonSerialized] MonoBehaviour? _ownerForEditor;
        static double _lastRepaintTime;
        const double RepaintIntervalSeconds = 0.2;
#endif

        public void Initialize(ISaveManager save, ISaveStore store, ISavePlatform platform, ISaveSerializer serializer)
        {
            _save = save;
            _debug = save as ISaveManagerDebug;
            _store = store;
            _platform = platform;
            _serializer = serializer;

            BindStatus = _debug != null
                ? "Bound (with debug API)"
                : "Bound (no debug API)";
        }

        public void Initialize(ISaveManager save, ISaveStore store, ISavePlatform platform, ISaveSerializer serializer, MonoBehaviour owner)
        {
            Initialize(save, store, platform, serializer);
#if UNITY_EDITOR
            _ownerForEditor = owner;
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            _ownerForEditor = null;
#endif
        }

#if UNITY_EDITOR
        void OnEditorUpdate()
        {
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintIntervalSeconds)
                return;
            _lastRepaintTime = now;

            if (_save == null || _ownerForEditor == null)
                return;

            try { UnityEditor.EditorUtility.SetDirty(_ownerForEditor); }
            catch { }

            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
            catch { }
        }
#endif

        [Button(ButtonSizes.Medium)]
        public void RefreshNow()
        {
            PersistedFiles.Clear();
            Registrations.Clear();
            History.Clear();

            if (_save == null || _store == null || _platform == null)
            {
                BindStatus = "(unbound)";
                ActiveProfileId = 0;
                PlatformCaps = "(unknown)";
                PlanCacheCount = 0;
                PreviewStatus = "(idle)";
                return;
            }

            ActiveProfileId = _save.ActiveProfileId;
            var caps = _save.PlatformCaps;
            var max = caps.MaxBytesHint.HasValue ? caps.MaxBytesHint.Value.ToString() : "(none)";
            PlatformCaps = $"WebGL={caps.IsWebGL} MaxBytes={max} Export={caps.SupportsDownloadExport}";

            if (_debug != null)
            {
                PlanCacheCount = _debug.PlanCacheCount;
                _debug.GetRegistrations(Registrations);

                _tmpOps.Clear();
                _debug.GetRecentOperations(_tmpOps);
                for (int i = 0; i < _tmpOps.Count; i++)
                    History.Add(new SaveHistoryRow(_tmpOps[i]));
            }
            else
            {
                PlanCacheCount = 0;
            }

            RefreshPersistedFiles();
            BindStatus = _debug != null ? "Bound (with debug API)" : "Bound (no debug API)";
        }

        [Button(ButtonSizes.Medium)]
        public void LoadPreviewPayload()
        {
            PreviewBlackboardCount = 0;
            PreviewScalarCount = 0;
            PreviewBytesLength = 0;

            if (_store == null || _serializer == null)
            {
                PreviewStatus = "(unbound)";
                return;
            }

            var scopeKey = new ScopeKey(previewScopeKind, previewScopeId);
            var ctx = new SaveContext(previewProfileId, previewLayer, scopeKey);
            if (!ctx.TryValidate(out var ctxErr))
            {
                PreviewStatus = $"Context invalid: {ctxErr.Error} {ctxErr.Message}";
                return;
            }

            if (!SaveKeys.TryBuildPayloadKey(previewProfileId, scopeKey, previewLayer, out var key, out var keyErr))
            {
                PreviewStatus = $"Key build failed: {keyErr.Error} {keyErr.Message}";
                return;
            }

            var load = _store.Load(key);
            if (load.Status == SaveStoreLoadStatus.NotFound)
            {
                PreviewStatus = "NotFound";
                return;
            }

            if (load.Status != SaveStoreLoadStatus.Success || load.Bytes == null)
            {
                PreviewStatus = $"Load failed: {load.Message}";
                return;
            }

            PreviewBytesLength = load.Bytes.Length;
            var de = _serializer.TryDeserialize(load.Bytes, out SavePayload payload);
            if (de.Status != SaveSerializerStatus.Success)
            {
                PreviewStatus = $"Deserialize failed: {de.Message}";
                return;
            }

            PreviewBlackboardCount = payload.Blackboard != null ? payload.Blackboard.Length : 0;
            PreviewScalarCount = payload.Scalars != null ? payload.Scalars.Length : 0;
            PreviewStatus = "Success";
        }

        public static bool TryDeletePersistedDataFromEditor(out string status)
        {
#if UNITY_EDITOR
            status = "Deleted persisted save data";
            try
            {
                var root = Path.Combine(Application.persistentDataPath, "SaveV2");
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (Exception ex)
            {
                status = ex.Message;
                return false;
            }

            try
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                status = ex.Message;
                return false;
            }

            return true;
#else
            status = "Editor only";
            return false;
#endif
        }

        void RefreshPersistedFiles()
        {
            PersistedFiles.Clear();

            var root = Path.Combine(Application.persistentDataPath, "SaveV2");
            if (!Directory.Exists(root))
            {
                PersistedFiles.Add(new PersistedFileRow("(missing)", 0, "(none)"));
                return;
            }

            try
            {
                var files = Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                {
                    var fi = new FileInfo(files[i]);
                    var rel = files[i].Replace(root, "").TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    PersistedFiles.Add(new PersistedFileRow(rel, fi.Length, fi.LastWriteTimeUtc.ToString("u")));
                }

                if (files.Length == 0)
                    PersistedFiles.Add(new PersistedFileRow("(empty)", 0, "(none)"));
            }
            catch (Exception ex)
            {
                PersistedFiles.Add(new PersistedFileRow($"(error) {ex.Message}", 0, "(none)"));
            }
        }

        [NonSerialized] readonly List<SaveOperationRecord> _tmpOps = new();

        [Serializable]
        public readonly struct PersistedFileRow
        {
            [ShowInInspector, ReadOnly] public readonly string RelativePath;
            [ShowInInspector, ReadOnly] public readonly long Bytes;
            [ShowInInspector, ReadOnly] public readonly string LastWriteUtc;

            public PersistedFileRow(string relativePath, long bytes, string lastWriteUtc)
            {
                RelativePath = relativePath;
                Bytes = bytes;
                LastWriteUtc = lastWriteUtc;
            }
        }

        [Serializable]
        public readonly struct SaveHistoryRow
        {
            [ShowInInspector, ReadOnly] public readonly string Utc;
            [ShowInInspector, ReadOnly] public readonly SaveOperationKind Kind;
            [ShowInInspector, ReadOnly] public readonly string Context;
            [ShowInInspector, ReadOnly] public readonly string Result;
            [ShowInInspector, ReadOnly] public readonly string Key;
            [ShowInInspector, ReadOnly] public readonly int BytesLength;

            public SaveHistoryRow(in SaveOperationRecord r)
            {
                Utc = r.UtcTime.ToString("u");
                Kind = r.Kind;
                Context = r.Context.ToString();
                Result = r.Result.ToString();
                Key = r.Key;
                BytesLength = r.BytesLength;
            }
        }
    }
}

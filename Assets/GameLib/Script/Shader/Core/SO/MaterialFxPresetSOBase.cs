#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx プリセットの基底クラス。
    /// CustomEntries と AutoEntries を合成した Entries を提供。
    /// </summary>
    public abstract class MaterialFxPresetSOBase : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        // Custom Entries（開発者が自由に編集）
        // ─────────────────────────────────────────────────────────────────────

        [TitleGroup("Custom Entries")]
        [InfoBox("開発者が自由に編集できるエントリ。AutoEntries より後に適用されます。")]
        [Tooltip("カスタムエントリ。AutoEntries を上書きできます。")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> CustomEntries = new();

        // ─────────────────────────────────────────────────────────────────────
        // Auto Entries（派生クラスで自動生成）
        // ─────────────────────────────────────────────────────────────────────

        [TitleGroup("Auto Entries (Read Only)")]
        [InfoBox("フィールドから自動生成されたエントリ。直接編集不可。")]
        [SerializeField, ReadOnly]
        [ListDrawerSettings(ShowFoldout = true, IsReadOnly = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        protected List<MaterialFxPresetEntry> AutoEntries = new();

        // ─────────────────────────────────────────────────────────────────────
        // Combined Entries（外部向け）
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AutoEntries + CustomEntries を合成したエントリリスト。
        /// CustomEntries が後なので、同じ Key があれば Custom が優先される。
        /// </summary>
        public IReadOnlyList<MaterialFxPresetEntry> Entries
        {
            get
            {
                RefreshEntriesIfNeeded();
                return _combinedEntries;
            }
        }

        [NonSerialized]
        List<MaterialFxPresetEntry> _combinedEntries = new();

        [NonSerialized]
        bool _entriesDirty = true;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// エントリを強制更新。
        /// </summary>
        [TitleGroup("Actions")]
        [Button("Refresh Entries", ButtonSizes.Medium)]
        public void RefreshEntries()
        {
            OnRefreshAutoEntries();
            CombineEntries();
            _entriesDirty = false;
        }

        /// <summary>
        /// エントリを汚れた状態にマーク（次回アクセス時に更新）。
        /// </summary>
        public void MarkEntriesDirty()
        {
            _entriesDirty = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Protected API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AutoEntries を更新するための派生クラス実装。
        /// </summary>
        protected abstract void OnRefreshAutoEntries();

        /// <summary>
        /// AutoEntry を設定または更新。
        /// </summary>
        protected void SetAutoEntry(string key, MaterialFxSerializedValue value, MaterialFxBlendMode blendMode = MaterialFxBlendMode.Override)
        {
            for (int i = 0; i < AutoEntries.Count; i++)
            {
                if (AutoEntries[i].Key == key)
                {
                    var entry = AutoEntries[i];
                    entry.Value = value;
                    entry.BlendMode = blendMode;
                    AutoEntries[i] = entry;
                    return;
                }
            }

            AutoEntries.Add(new MaterialFxPresetEntry
            {
                Key = key,
                Value = value,
                BlendMode = blendMode
            });
        }

        /// <summary>
        /// AutoEntry を削除。
        /// </summary>
        protected void RemoveAutoEntry(string key)
        {
            for (int i = AutoEntries.Count - 1; i >= 0; i--)
            {
                if (AutoEntries[i].Key == key)
                {
                    AutoEntries.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// AutoEntries をクリア。
        /// </summary>
        protected void ClearAutoEntries()
        {
            AutoEntries.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────────────────────────────

        void RefreshEntriesIfNeeded()
        {
            if (_entriesDirty)
            {
                RefreshEntries();
            }
        }

        void CombineEntries()
        {
            _combinedEntries.Clear();

            // AutoEntries を先に追加
            foreach (var entry in AutoEntries)
            {
                _combinedEntries.Add(entry);
            }

            // CustomEntries を追加（同じ Key があれば上書き）
            foreach (var customEntry in CustomEntries)
            {
                bool found = false;
                for (int i = 0; i < _combinedEntries.Count; i++)
                {
                    if (_combinedEntries[i].Key == customEntry.Key)
                    {
                        _combinedEntries[i] = customEntry;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _combinedEntries.Add(customEntry);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Callbacks
        // ─────────────────────────────────────────────────────────────────────

        protected virtual void OnEnable()
        {
            _entriesDirty = true;
        }

        protected virtual void OnValidate()
        {
            _entriesDirty = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper Methods for Value Creation
        // ─────────────────────────────────────────────────────────────────────

        protected static MaterialFxSerializedValue MakeFloat(float v) => new() { Type = ValueKind.Float, Float = v };
        protected static MaterialFxSerializedValue MakeInt(int v) => new() { Type = ValueKind.Int, Int = v };
        protected static MaterialFxSerializedValue MakeBool(bool v) => new() { Type = ValueKind.Bool, Int = v ? 1 : 0 };
        protected static MaterialFxSerializedValue MakeFloat2(Vector2 v) => new() { Type = ValueKind.Float2, Float2 = v };
        protected static MaterialFxSerializedValue MakeFloat3(Vector3 v) => new() { Type = ValueKind.Float3, Float3 = v };
        protected static MaterialFxSerializedValue MakeFloat4(Vector4 v) => new() { Type = ValueKind.Float4, Float4 = v };
        protected static MaterialFxSerializedValue MakeColor(Color v) => new() { Type = ValueKind.Color, Color = v };
        protected static MaterialFxSerializedValue MakeTexture(Texture? v) => new() { Type = ValueKind.Texture, Texture = v };
    }
}

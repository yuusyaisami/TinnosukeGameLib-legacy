#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx 繝励Μ繧ｻ繝・ヨ縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// CustomEntries 縺ｨ AutoEntries 繧貞粋謌舌＠縺・Entries 繧呈署萓帙・
    /// </summary>
    public abstract class MaterialFxPresetSOBase : ScriptableObject
    {
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Custom Entries・磯幕逋ｺ閠・′閾ｪ逕ｱ縺ｫ邱ｨ髮・ｼ・
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [TitleGroup("Custom Entries")]
        [InfoBox("Inspector info.")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> CustomEntries = new();

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Auto Entries・域ｴｾ逕溘け繝ｩ繧ｹ縺ｧ閾ｪ蜍慕函謌撰ｼ・
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [TitleGroup("Auto Entries (Read Only)")]
        [InfoBox("Inspector info.")]
        [SerializeField, ReadOnly]
        [ListDrawerSettings(ShowFoldout = true, IsReadOnly = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        protected List<MaterialFxPresetEntry> AutoEntries = new();

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Combined Entries・亥､夜Κ蜷代￠・・
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        /// <summary>
        /// AutoEntries + CustomEntries 繧貞粋謌舌＠縺溘お繝ｳ繝医Μ繝ｪ繧ｹ繝医・
        /// CustomEntries 縺悟ｾ後↑縺ｮ縺ｧ縲∝酔縺・Key 縺後≠繧後・ Custom 縺悟━蜈医＆繧後ｋ縲・
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

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Public API
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        /// <summary>
        /// 繧ｨ繝ｳ繝医Μ繧貞ｼｷ蛻ｶ譖ｴ譁ｰ縲・
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
        /// 繧ｨ繝ｳ繝医Μ繧呈ｱ壹ｌ縺溽憾諷九↓繝槭・繧ｯ・域ｬ｡蝗槭い繧ｯ繧ｻ繧ｹ譎ゅ↓譖ｴ譁ｰ・峨・
        /// </summary>
        public void MarkEntriesDirty()
        {
            _entriesDirty = true;
        }

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Protected API
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        /// <summary>
        /// AutoEntries 繧呈峩譁ｰ縺吶ｋ縺溘ａ縺ｮ豢ｾ逕溘け繝ｩ繧ｹ螳溯｣・・
        /// </summary>
        protected abstract void OnRefreshAutoEntries();

        /// <summary>
        /// AutoEntry 繧定ｨｭ螳壹∪縺溘・譖ｴ譁ｰ縲・
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
        /// AutoEntry 繧貞炎髯､縲・
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
        /// AutoEntries 繧偵け繝ｪ繧｢縲・
        /// </summary>
        protected void ClearAutoEntries()
        {
            AutoEntries.Clear();
        }

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Internal
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

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

            // AutoEntries 繧貞・縺ｫ霑ｽ蜉
            foreach (var entry in AutoEntries)
            {
                _combinedEntries.Add(entry);
            }

            // CustomEntries 繧定ｿｽ蜉・亥酔縺・Key 縺後≠繧後・荳頑嶌縺搾ｼ・
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

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Unity Callbacks
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        protected virtual void OnEnable()
        {
            _entriesDirty = true;
        }

        protected virtual void OnValidate()
        {
            _entriesDirty = true;
        }

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        // Helper Methods for Value Creation
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        protected static MaterialFxSerializedValue MakeFloat(float v) => new() { Type = ValueKind.Float, Float = DynamicValueExtensions.FromLiteral(v) };
        protected static MaterialFxSerializedValue MakeInt(int v) => new() { Type = ValueKind.Int, Int = v };
        protected static MaterialFxSerializedValue MakeBool(bool v) => new() { Type = ValueKind.Bool, Int = v ? 1 : 0 };
        protected static MaterialFxSerializedValue MakeFloat2(Vector2 v) => new() { Type = ValueKind.Float2, Float2 = v };
        protected static MaterialFxSerializedValue MakeFloat3(Vector3 v) => new() { Type = ValueKind.Float3, Float3 = v };
        protected static MaterialFxSerializedValue MakeFloat4(Vector4 v) => new() { Type = ValueKind.Float4, Float4 = v };
        protected static MaterialFxSerializedValue MakeColor(Color v) => new() { Type = ValueKind.Color, Color = v };
        protected static MaterialFxSerializedValue MakeTexture(Texture? v) => new() { Type = ValueKind.Texture, Texture = v };
    }
}

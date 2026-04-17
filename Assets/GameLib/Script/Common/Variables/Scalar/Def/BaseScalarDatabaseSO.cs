using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

namespace Game.Scalar
{
    /// <summary>
    /// Scalarに登録するエントリデータ。
    /// </summary>
    [Serializable]
    public struct ScalarDatabaseEntry
    {
        public ScalarKey Key;
        public float BaseValue;
        public bool UseEffectMod;
        public bool UseRoundMod;
        public int RoundDigits;
        public bool UseClampMod;
        public ScalarClamp Clamp;
        public bool SaveEnabled;
        public SaveLayer SaveLayer;
    }

    /// <summary>
    /// Scalarシステムに登録するデータ群を定義する基底ScriptableObject。
    /// 派生クラスで固定のエントリを追加することも可能。
    /// </summary>
    public class BaseScalarDatabaseSO : ScriptableObject
    {
        [SerializeField] protected List<ScalarDatabaseEntry> entries = new();

        /// <summary>
        /// 登録するエントリを取得する。派生クラスでオーバーライド可能。
        /// </summary>
        public virtual IEnumerable<ScalarDatabaseEntry> GetEntries()
        {
            return entries;
        }

        public bool TryGetEntry(ScalarKey key, out ScalarDatabaseEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key.Id == key.Id)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IEnumerable<ScalarDatabaseEntry> EnumerateByLayer(SaveLayer layer)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].SaveLayer == layer)
                    yield return entries[i];
            }
        }

        public IEnumerable<ScalarDatabaseEntry> EnumerateSaveTargets(SaveLayer layer)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.SaveEnabled && e.SaveLayer == layer)
                    yield return e;
            }
        }
    }
}

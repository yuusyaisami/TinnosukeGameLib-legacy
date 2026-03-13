#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// WebGL向けの簡易永続ストア。
    /// PlayerPrefsにBase64で保存する（小容量向け）。
    /// </summary>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        const string Prefix = "SaveV2_";

        public bool KeyExists(string key)
        {
            var ppKey = ToPlayerPrefsKey(key);
            return PlayerPrefs.HasKey(ppKey);
        }

        public void DeleteKey(string key)
        {
            var ppKey = ToPlayerPrefsKey(key);
            if (PlayerPrefs.HasKey(ppKey))
                PlayerPrefs.DeleteKey(ppKey);
        }

        public SaveStoreDeleteAllResult DeleteAll()
        {
            try
            {
                // PlayerPrefs has no key enumeration API, so the WebGL backend must clear the whole store.
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                return new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.Success);
            }
            catch (Exception ex)
            {
                return new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.IOError, ex.Message);
            }
        }

        public SaveStoreSaveResult Save(string key, byte[] bytes)
        {
            if (string.IsNullOrEmpty(key))
                return new SaveStoreSaveResult(SaveStoreSaveStatus.IOError, "Key is empty.");
            if (bytes == null)
                return new SaveStoreSaveResult(SaveStoreSaveStatus.IOError, "Bytes is null.");

            try
            {
                var ppKey = ToPlayerPrefsKey(key);
                var b64 = Convert.ToBase64String(bytes);
                PlayerPrefs.SetString(ppKey, b64);
                // WebGLでも確実に永続化させる
                PlayerPrefs.Save();

#if UNITY_WEBGL || DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[PlayerPrefsSaveStore] Save ok: {ppKey} bytes={bytes.Length}");
#endif
                return new SaveStoreSaveResult(SaveStoreSaveStatus.Success);
            }
            catch (Exception ex)
            {
                return new SaveStoreSaveResult(SaveStoreSaveStatus.IOError, ex.Message);
            }
        }

        public SaveStoreLoadResult Load(string key)
        {
            if (string.IsNullOrEmpty(key))
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, "Key is empty.");

            try
            {
                var ppKey = ToPlayerPrefsKey(key);
                if (!PlayerPrefs.HasKey(ppKey))
                    return new SaveStoreLoadResult(SaveStoreLoadStatus.NotFound, null);

                var b64 = PlayerPrefs.GetString(ppKey, string.Empty);
                if (string.IsNullOrEmpty(b64))
                    return new SaveStoreLoadResult(SaveStoreLoadStatus.NotFound, null);

                var bytes = Convert.FromBase64String(b64);

#if UNITY_WEBGL || DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[PlayerPrefsSaveStore] Load ok: {ppKey} bytes={bytes.Length}");
#endif
                return new SaveStoreLoadResult(SaveStoreLoadStatus.Success, bytes);
            }
            catch (FormatException fe)
            {
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, fe.Message);
            }
            catch (Exception ex)
            {
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, ex.Message);
            }
        }

        static string ToPlayerPrefsKey(string key)
        {
            // PlayerPrefsキーの長さや文字種を安定化する
            return Prefix + Sha1Hex(key);
        }

        static string Sha1Hex(string s)
        {
            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var hash = sha1.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}

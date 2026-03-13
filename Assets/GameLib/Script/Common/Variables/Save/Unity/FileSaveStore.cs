#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Save
{
    public sealed class FileSaveStore : ISaveStore
    {
        readonly string _root;

        public FileSaveStore(string rootDirectory)
        {
            _root = string.IsNullOrEmpty(rootDirectory)
                ? Application.persistentDataPath
                : rootDirectory;
        }

        public bool KeyExists(string key)
        {
            var path = KeyToPath(key);
            return File.Exists(path);
        }

        public void DeleteKey(string key)
        {
            var path = KeyToPath(key);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Deliberately swallow: Delete is best-effort; failures will surface on Flush/next Save.
            }
        }

        public SaveStoreDeleteAllResult DeleteAll()
        {
            var root = Path.Combine(_root, "SaveV2");
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);

                return new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.Success);
            }
            catch (IOException io)
            {
                return new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.IOError, io.Message);
            }
            catch (UnauthorizedAccessException ua)
            {
                return new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.IOError, ua.Message);
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

            var path = KeyToPath(key);
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, bytes);

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tmp, path);
                return new SaveStoreSaveResult(SaveStoreSaveStatus.Success);
            }
            catch (IOException io)
            {
                return new SaveStoreSaveResult(SaveStoreSaveStatus.IOError, io.Message);
            }
            catch (UnauthorizedAccessException ua)
            {
                return new SaveStoreSaveResult(SaveStoreSaveStatus.IOError, ua.Message);
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

            var path = KeyToPath(key);
            try
            {
                if (!File.Exists(path))
                    return new SaveStoreLoadResult(SaveStoreLoadStatus.NotFound, null);

                var bytes = File.ReadAllBytes(path);
                return new SaveStoreLoadResult(SaveStoreLoadStatus.Success, bytes);
            }
            catch (IOException io)
            {
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, io.Message);
            }
            catch (UnauthorizedAccessException ua)
            {
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, ua.Message);
            }
            catch (Exception ex)
            {
                return new SaveStoreLoadResult(SaveStoreLoadStatus.IOError, null, ex.Message);
            }
        }

        string KeyToPath(string key)
        {
            // Keep directory structure for readability, but also hash to protect against overly long paths.
            var safeRelative = key.Replace('\\', '/').TrimStart('/');
            var rel = safeRelative.Replace('/', Path.DirectorySeparatorChar);

            // Windows MAX_PATH avoidance: keep a short hashed leaf.
            var leaf = Sha1Hex(key) + ".bin";
            var dir = Path.Combine(_root, "SaveV2", rel);
            return Path.Combine(dir, leaf);
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

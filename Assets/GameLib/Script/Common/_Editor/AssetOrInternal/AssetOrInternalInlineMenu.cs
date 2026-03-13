// Assets/Game/Editor/AssetOrInternalInlineMenu.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace Game.Entity
{
    public static class AssetOrInternalInlineMenu
    {
        /// リスト要素の右端に「…」ボタンを出す（Odin の OnBeginListElementGUI から呼ぶ）
        public static void DrawForListElement<T>(ScriptableObject root, IList<T> list, int index, Type[] allowedTypes = null) where T : ScriptableObject
        {
            var rect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight);
            rect.x = rect.xMax - 22; rect.width = 20;
            if (GUI.Button(rect, EditorGUIUtility.IconContent("_Popup"), EditorStyles.miniButton))
            {
                ShowMenuFor(root, list, index, allowedTypes);
            }
        }

        /// 単体参照用（通常のフィールド横にインラインで置きたい時）
        public static void DrawForSingle<T>(ScriptableObject root, Func<T> get, Action<T> set, Type[] allowedTypes = null) where T : ScriptableObject
        {
            var rect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight);
            rect.x = rect.xMax - 22; rect.width = 20;
            if (GUI.Button(rect, EditorGUIUtility.IconContent("_Popup"), EditorStyles.miniButton))
            {
                ShowMenuFor(root, get(), set, allowedTypes);
            }
        }

        static void ShowMenuFor<T>(ScriptableObject root, IList<T> list, int index, Type[] allowedTypes) where T : ScriptableObject
        {
            EnsureRootAssetSaved(root);
            var cur = list[index] as ScriptableObject;
            var menu = BuildMenu(root, typeof(T), cur, allowedTypes,
                onSet: o => { list[index] = (T)o; EditorUtility.SetDirty(root); },
                onClear: () => { list[index] = null; EditorUtility.SetDirty(root); });
            menu.ShowAsContext();
        }

        static void ShowMenuFor<T>(ScriptableObject root, T cur, Action<T> setter, Type[] allowedTypes) where T : ScriptableObject
        {
            EnsureRootAssetSaved(root);
            var menu = BuildMenu(root, typeof(T), cur, allowedTypes,
                onSet: o => { setter((T)o); EditorUtility.SetDirty(root); },
                onClear: () => { setter(null); EditorUtility.SetDirty(root); });
            menu.ShowAsContext();
        }

        static GenericMenu BuildMenu(ScriptableObject root, Type baseType, ScriptableObject cur, Type[] allowedTypes,
            Action<UnityEngine.Object> onSet, System.Action onClear)
        {
            var m = new GenericMenu();
            var baseLabel = ObjectNames.NicifyVariableName(baseType.Name);

            m.AddDisabledItem(new GUIContent($"{baseLabel}" + (cur ? $" : {cur.name}" : " : (None)")));
            if (cur) m.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(cur));
            else m.AddDisabledItem(new GUIContent("Ping"));

            // New Internal…（基底/派生）
            var entries = BuildCreationEntries(baseType, allowedTypes)
                .OrderBy(e => e.CategoryPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Order)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Type.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var entry in entries)
            {
                m.AddItem(new GUIContent(entry.FullPath), false, () =>
                {
                    var obj = ScriptableObject.CreateInstance(entry.Type);
                    obj.name = entry.Type.Name;
                    var rootPath = AssetDatabase.GetAssetPath(root);
                    AssetDatabase.AddObjectToAsset(obj, root);
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        AssetDatabase.ImportAsset(rootPath);
                    }
                    AssetDatabase.SaveAssets();
                    onSet(obj);
                    EditorGUIUtility.PingObject(obj);
                });
            }

            // Extract
            if (IsSubAssetOfRoot(cur, root))
            {
                m.AddItem(new GUIContent("Extract…"), false, () =>
                {
                    var save = EditorUtility.SaveFilePanelInProject("Extract As Asset", cur.name, "asset", "Save location");
                    if (string.IsNullOrEmpty(save)) return;
                    var clone = UnityEngine.Object.Instantiate(cur);
                    clone.name = cur.name;
                    AssetDatabase.CreateAsset(clone, save);
                    AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                    onSet(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(save));
                });
            }
            else m.AddDisabledItem(new GUIContent("Extract…"));

            // Delete Internal
            if (IsSubAssetOfRoot(cur, root))
            {
                m.AddItem(new GUIContent("Delete Internal"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Internal Asset", $"Delete '{cur.name}' ?", "Delete", "Cancel"))
                    {
                        UnityEngine.Object.DestroyImmediate(cur, true);
                        onClear();
                        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                    }
                });
            }
            else m.AddDisabledItem(new GUIContent("Delete Internal"));

            // Clear
            if (cur) m.AddItem(new GUIContent("Clear"), false, () => onClear());
            else m.AddDisabledItem(new GUIContent("Clear"));

            // Clean Unused
            if (root) m.AddItem(new GUIContent("Clean Unused SubAssets"), false, () => CleanUnused(root));
            else m.AddDisabledItem(new GUIContent("Clean Unused SubAssets"));

            return m;
        }

        static bool IsSubAssetOfRoot(UnityEngine.Object obj, ScriptableObject root)
        {
            if (!obj || !root) return false;
            var a = AssetDatabase.GetAssetPath(obj);
            var b = AssetDatabase.GetAssetPath(root);
            return !string.IsNullOrEmpty(a) && a == b && obj != root;
        }

        static void EnsureRootAssetSaved(ScriptableObject root)
        {
            var path = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(path))
            {
                var save = EditorUtility.SaveFilePanelInProject("Save Root Asset", root.name, "asset", "Save the root asset.");
                if (!string.IsNullOrEmpty(save))
                {
                    AssetDatabase.CreateAsset(root, save);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        static void CleanUnused(ScriptableObject root)
        {
            var path = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(path)) return;

            var used = new HashSet<UnityEngine.Object>();
            var so = new SerializedObject(root);
            var it = so.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = true;
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var o = it.objectReferenceValue;
                    if (o) used.Add(o);
                }
            }

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            int removed = 0;
            foreach (var a in all)
            {
                if (!a || a == root) continue;
                if (!used.Contains(a))
                {
                    UnityEngine.Object.DestroyImmediate(a, true);
                    removed++;
                }
            }
            if (removed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[AssetOrInternal] Cleaned {removed} unused sub-assets from '{root.name}'");
            }
        }

        private readonly struct CreationEntry
        {
            public CreationEntry(Type type, string categoryPath, string displayName, int order)
            {
                Type = type;
                CategoryPath = categoryPath;
                DisplayName = displayName;
                Order = order;
            }

            public Type Type { get; }
            public string CategoryPath { get; }
            public string DisplayName { get; }
            public int Order { get; }
            public string FullPath => string.IsNullOrEmpty(CategoryPath)
                ? $"New Internal…/{DisplayName}"
                : $"New Internal…/{CategoryPath}/{DisplayName}";
        }

        private static IEnumerable<CreationEntry> BuildCreationEntries(Type baseType, Type[] allowedTypes)
        {
            // Generic: use CreateAssetMenuAttribute for menu metadata. Removed AICommand-specific handling.
            var types = TypeCache.GetTypesDerivedFrom(baseType)
                                 .Concat(new[] { baseType })
                                 .Where(t => t != null
                                             && typeof(ScriptableObject).IsAssignableFrom(t)
                                             && !t.IsAbstract
                                             && !t.IsGenericType
                                             && t.GetCustomAttribute<ObsoleteAttribute>(inherit: true) == null)
                                 .Distinct();

            foreach (var type in types)
            {
                if (!IsAllowed(type, allowedTypes))
                    continue;

                string category = null;
                string display = ObjectNames.NicifyVariableName(type.Name);
                int order = 0;

                {
                    var createMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                    if (createMenu != null && !string.IsNullOrWhiteSpace(createMenu.menuName))
                    {
                        ParseCreateMenuName(createMenu.menuName, out category, out display);
                        order = createMenu.order;
                    }
                }

                yield return new CreationEntry(type, category, display, order);
            }
        }

        private static string SanitizeMenuPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "General";
            var trimmed = value.Replace('\\', '/').Trim('/');
            return string.IsNullOrEmpty(trimmed) ? "General" : trimmed;
        }

        private static string CombinePathSegments(string prefix, string path)
        {
            if (string.IsNullOrEmpty(prefix)) return path;
            if (string.IsNullOrEmpty(path)) return prefix;
            return $"{prefix}/{path.TrimStart('/')}";
        }

        private static void ParseCreateMenuName(string menuName, out string category, out string display)
        {
            var trimmed = menuName?.Replace('\\', '/').Trim('/') ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                category = null;
                display = "New Asset";
                return;
            }

            var slash = trimmed.LastIndexOf('/');
            if (slash >= 0)
            {
                category = trimmed.Substring(0, slash);
                display = trimmed.Substring(slash + 1);
            }
            else
            {
                category = null;
                display = trimmed;
            }
        }

        private static bool IsAllowed(Type candidate, Type[] allowedTypes)
        {
            if (candidate == null) return false;
            if (!typeof(ScriptableObject).IsAssignableFrom(candidate)) return false;
            if (allowedTypes == null || allowedTypes.Length == 0) return true;
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                var allowed = allowedTypes[i];
                if (allowed == null) continue;
                if (allowed.IsAssignableFrom(candidate))
                    return true;
            }
            return false;
        }
    }
}
#endif

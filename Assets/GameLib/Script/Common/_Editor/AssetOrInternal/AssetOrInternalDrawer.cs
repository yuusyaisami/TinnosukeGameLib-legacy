// Assets/Game/Editor/AssetOrInternalDrawer.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

namespace Game.Entity
{
    /// <summary>
    /// - 本体のObjectFieldは既定描画（CallNextDrawer）→ Odinのペンアイコンが残る
    /// - 右端に「⋯」ボタン → クリックでメニュー表示
    /// - ScriptableObject サブアセットの生成／抽出／削除／クリーンアップをサポート
    /// </summary>
    public sealed class AssetOrInternalDrawer<T> : OdinAttributeDrawer<AssetOrInternalAttribute, T>
        where T : ScriptableObject
    {
        // 型不一致時にログを出すかどうか（大量フィールド対策でデフォルト false）
        const bool LogInvalidType = false;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            SirenixEditorGUI.BeginHorizontalPropertyLayout(label);

            // 左：ObjectField（標準描画）
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            this.CallNextDrawer(null); // ラベルは既に描画済みなので null
            GUILayout.EndVertical();

            GUILayout.Space(2);

            // 右：メニュー呼び出しボタン
            var btnStyle = EditorStyles.miniButton;
            var popupIcon = EditorGUIUtility.IconContent("_Popup");
            var h = Mathf.Max(18, (int)EditorGUIUtility.singleLineHeight);
            if (GUILayout.Button(popupIcon, btnStyle, GUILayout.Width(20), GUILayout.Height(h)))
            {
                var entry = Property.ValueEntry as IPropertyValueEntry<T>;
                var root = GetRootObject(Property) as ScriptableObject;

                ShowMenu(entry, root);
            }

            EnforceAllowedType();

            SirenixEditorGUI.EndHorizontalPropertyLayout();
        }

        /// <summary>
        /// 属性の AllowedTypes に一致しない型が入っていた場合、静かに null にする。
        /// （Undo対応あり／ログはオプション）
        /// </summary>
        void EnforceAllowedType()
        {
            if (!(Property.ValueEntry is IPropertyValueEntry<T> entry))
                return;

            if (Attribute.AllowedTypes == null || Attribute.AllowedTypes.Length == 0)
                return;

            var value = entry.SmartValue;
            if (value == null)
                return;

            if (!IsAllowed(Attribute, value.GetType()))
            {
                var root = GetRootObject(Property);
                if (root != null)
                {
                    Undo.RecordObject(root, "[AssetOrInternal] Clear invalid reference");
                }

                if (LogInvalidType)
                {
                    //Debug.LogWarning(
                    //    $"[AssetOrInternal] '{value.name}' is not an allowed type for this field. Clearing reference.",
                    //    value);
                }

                entry.SmartValue = null;
                entry.ApplyChanges();
            }
        }

        static UnityEngine.Object GetRootObject(InspectorProperty prop)
        {
            // Odin のシリアライズルート（通常は編集中のSO）
            var root = prop.SerializationRoot?.ValueEntry?.WeakSmartValue as UnityEngine.Object;
            if (!root)
                root = prop.Tree.UnitySerializedObject?.targetObject;
            return root;
        }

        void ShowMenu(IPropertyValueEntry<T> entry, ScriptableObject root)
        {
            var cur = entry?.SmartValue;
            var menu = new GenericMenu();

            // ヘッダ
            menu.AddDisabledItem(new GUIContent(typeof(T).Name + (cur ? $" : {cur.name}" : " : (None)")));

            // Ping
            if (cur)
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(cur));
            else
                menu.AddDisabledItem(new GUIContent("Ping"));

            // New Internal …（基底/派生クラス一覧）
            if (!root)
            {
                menu.AddDisabledItem(new GUIContent("New Internal…"));
            }
            else
            {
                EnsureAssetSaved(root);
                var entries = BuildCreationEntries(typeof(T), Attribute)
                    .OrderBy(e => e.CategoryPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.Order)
                    .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.Type.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var entryInfo in entries)
                {
                    menu.AddItem(new GUIContent(entryInfo.FullPath), false, () =>
                    {
                        // Undoグループ開始
                        Undo.IncrementCurrentGroup();
                        var group = Undo.GetCurrentGroup();

                        Undo.RecordObject(root, $"Create Internal {entryInfo.Type.Name}");

                        var obj = ScriptableObject.CreateInstance(entryInfo.Type);
                        obj.name = entryInfo.Type.Name;

                        // 生成された sub-asset 自体も Undo 管理
                        Undo.RegisterCreatedObjectUndo(obj, $"Create Internal {entryInfo.Type.Name}");

                        var rootPath = AssetDatabase.GetAssetPath(root);
                        AssetDatabase.AddObjectToAsset(obj, root);
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            AssetDatabase.ImportAsset(rootPath);
                        }

                        AssetDatabase.SaveAssets();
                        entry.SmartValue = (T)obj;
                        entry.ApplyChanges();
                        EditorGUIUtility.PingObject(obj);

                        Undo.CollapseUndoOperations(group);
                    });
                }
            }

            // Extract（内部SO→外部化）
            if (IsSubAssetOfRoot(cur, root))
            {
                menu.AddItem(new GUIContent("Extract…"), false, () =>
                {
                    if (!root)
                        return;

                    var rootPath = AssetDatabase.GetAssetPath(root);
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        Debug.LogWarning("[AssetOrInternal] Cannot extract: root asset has no valid path.", root);
                        return;
                    }

                    // ルートSOと同じフォルダに、自動で .asset を作成
                    var dir = Path.GetDirectoryName(rootPath);
                    var fileName = string.IsNullOrWhiteSpace(cur.name) ? cur.GetType().Name : cur.name;
                    var newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, fileName + ".asset"));

                    Undo.IncrementCurrentGroup();
                    var group = Undo.GetCurrentGroup();

                    // 新規アセットとしてクローンを作成
                    var clone = UnityEngine.Object.Instantiate(cur);
                    clone.name = fileName;
                    Undo.RegisterCreatedObjectUndo(clone, "Extract Internal Asset");

                    AssetDatabase.CreateAsset(clone, newPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Undo.RecordObject(root, "Extract Internal Asset");

                    // フィールドの参照先を、新しく作った外部アセットに差し替え
                    entry.SmartValue = AssetDatabase.LoadAssetAtPath<T>(newPath);
                    entry.ApplyChanges();
                    EditorGUIUtility.PingObject(entry.SmartValue);

                    Undo.CollapseUndoOperations(group);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Extract…"));
            }

            // Delete（内部SOのみ）
            if (IsSubAssetOfRoot(cur, root))
            {
                menu.AddItem(new GUIContent("Delete Internal"), false, () =>
                {
                    if (!EditorUtility.DisplayDialog(
                            "Delete Internal Asset",
                            $"Delete '{cur.name}' ?",
                            "Delete",
                            "Cancel"))
                        return;

                    Undo.IncrementCurrentGroup();
                    var group = Undo.GetCurrentGroup();

                    if (root)
                    {
                        Undo.RecordObject(root, "Delete Internal Asset");
                    }

                    // Undo対応のDestroy
                    Undo.DestroyObjectImmediate(cur);

                    entry.SmartValue = null;
                    entry.ApplyChanges();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Undo.CollapseUndoOperations(group);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Delete Internal"));
            }

            // Clear（参照を空に）
            if (cur)
            {
                menu.AddItem(new GUIContent("Clear"), false, () =>
                {
                    var rootObj = GetRootObject(Property);
                    if (rootObj != null)
                    {
                        Undo.RecordObject(rootObj, "Clear AssetOrInternal reference");
                    }

                    entry.SmartValue = null;
                    entry.ApplyChanges();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clear"));
            }

            // Clean Unused（未参照 sub-asset を一括削除）
            if (root)
            {
                menu.AddItem(new GUIContent("Clean Unused SubAssets"), false, () => CleanUnused(root));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clean Unused SubAssets"));
            }

            menu.ShowAsContext();
        }

        static void EnsureAssetSaved(ScriptableObject root)
        {
            var path = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(path))
            {
                var save = EditorUtility.SaveFilePanelInProject(
                    "Save Root Asset",
                    root.name,
                    "asset",
                    "Save the root asset.");
                if (!string.IsNullOrEmpty(save))
                {
                    AssetDatabase.CreateAsset(root, save);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        static bool IsSubAssetOfRoot(UnityEngine.Object obj, ScriptableObject root)
        {
            if (!obj || !root)
                return false;

            var a = AssetDatabase.GetAssetPath(obj);
            var b = AssetDatabase.GetAssetPath(root);
            return !string.IsNullOrEmpty(a) && a == b && obj != root;
        }

        static void CleanUnused(ScriptableObject root)
        {
            var path = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(path))
                return;

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            Undo.RecordObject(root, "Clean Unused SubAssets");

            // root内で参照されている sub-asset を収集
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
                    if (o)
                        used.Add(o);
                }
            }

            // 未使用 sub-asset を削除（Undo対応）
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            int removed = 0;
            foreach (var a in all)
            {
                if (!a || a == root)
                    continue;
                if (!used.Contains(a))
                {
                    Undo.DestroyObjectImmediate(a);
                    removed++;
                }
            }

            if (removed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[AssetOrInternal] Cleaned {removed} unused sub-assets from '{root.name}'");
            }

            Undo.CollapseUndoOperations(group);
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

        /// <summary>
        /// New Internal… メニューに表示する派生型一覧を構築。
        /// AICommandSO 依存は完全に排除し、CreateAssetMenuAttribute だけを見る汎用実装。
        /// </summary>
        private static IEnumerable<CreationEntry> BuildCreationEntries(
            Type baseType,
            AssetOrInternalAttribute attribute)
        {
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
                if (!IsAllowed(attribute, type))
                    continue;

                string category = null;
                string display = ObjectNames.NicifyVariableName(type.Name);
                int order = 0;

                var createMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                if (createMenu != null && !string.IsNullOrWhiteSpace(createMenu.menuName))
                {
                    ParseCreateMenuName(createMenu.menuName, out category, out display);
                    order = createMenu.order;
                }

                yield return new CreationEntry(type, category, display, order);
            }
        }

        static string SanitizeMenuPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "General";
            var trimmed = value.Replace('\\', '/').Trim('/');
            return string.IsNullOrEmpty(trimmed) ? "General" : trimmed;
        }

        static string CombinePathSegments(string prefix, string path)
        {
            if (string.IsNullOrEmpty(prefix))
                return path;
            if (string.IsNullOrEmpty(path))
                return prefix;
            return $"{prefix}/{path.TrimStart('/')}";
        }

        static void ParseCreateMenuName(string menuName, out string category, out string display)
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

        static bool IsAllowed(AssetOrInternalAttribute attribute, Type candidate)
        {
            if (candidate == null)
                return false;
            if (!typeof(ScriptableObject).IsAssignableFrom(candidate))
                return false;

            if (attribute == null || attribute.AllowedTypes == null || attribute.AllowedTypes.Length == 0)
                return true;

            for (int i = 0; i < attribute.AllowedTypes.Length; i++)
            {
                var allowed = attribute.AllowedTypes[i];
                if (allowed == null)
                    continue;
                if (allowed.IsAssignableFrom(candidate))
                    return true;
            }

            return false;
        }
    }
}
#endif

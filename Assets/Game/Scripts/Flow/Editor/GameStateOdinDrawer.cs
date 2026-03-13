#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using System;
namespace Game.Actions.Editor
{
    /// <summary>
    /// GameState を階層メニューで選択できる Odin Drawer。
    /// </summary>
    public sealed class GameStateOdinDrawer : OdinValueDrawer<GameState>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var registry = GameStateRegistryLocator.GetOrCreate();
            if (registry == null)
            {
                CallNextDrawer(label);
                return;
            }

            var current = ValueEntry.SmartValue;
            var currentId = (int)current;

            var entries = new List<(string menuPath, int id)>();
            var idToPath = new Dictionary<int, string>();

            foreach (var node in registry.Nodes)
            {
                if (node == null || node.IsFolder)
                    continue;

                var menuPath = registry.GetDisplayPath(node.Id);
                if (string.IsNullOrEmpty(menuPath))
                    continue;

                entries.Add((menuPath, node.StateId));
                if (!idToPath.ContainsKey(node.StateId))
                    idToPath.Add(node.StateId, menuPath);
            }

            entries.Sort((a, b) => string.Compare(a.menuPath, b.menuPath, StringComparison.Ordinal));

            EditorGUILayout.BeginHorizontal();
            {
                if (label != null && !string.IsNullOrEmpty(label.text))
                    GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

                var displayPath = idToPath.TryGetValue(currentId, out var path) ? path : current.ToString();
                var displayText = string.IsNullOrEmpty(displayPath) ? "<None>" : displayPath.Replace("/", " › ");

                if (GUILayout.Button(displayText, EditorStyles.popup, GUILayout.MinWidth(100f)))
                {
                    var menu = new GenericMenu();
                    foreach (var (menuPath, id) in entries)
                    {
                        var isOn = id == currentId;
                        var captured = id;
                        menu.AddItem(new GUIContent(menuPath), isOn, () => { ValueEntry.SmartValue = (GameState)captured; });
                    }

                    menu.ShowAsContext();
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("d_FilterByLabel"), GUILayout.Width(22f)))
                {
                    GameStateExplorerWindow.Open();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif

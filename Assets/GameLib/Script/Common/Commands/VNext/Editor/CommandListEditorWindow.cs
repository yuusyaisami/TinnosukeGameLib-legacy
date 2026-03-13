#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Commands.VNext.Editor
{
    /// <summary>
    /// CommandListDataを別ウィンドウで編集するためのEditorWindow。
    /// Inspectorよりも広い画面でコマンドリストを編集できます。
    /// </summary>
    public sealed class CommandListEditorWindow : EditorWindow
    {
        const string WindowTitle = "Command List Editor";
        const float MinWidth = 400f;
        const float MinHeight = 300f;
        const float FooterHeight = 28f;

        [SerializeField]
        UnityEngine.Object? _ownerObject;

        [SerializeField]
        string _fieldPath = string.Empty;

        [SerializeField]
        string _functionName = string.Empty;

        [SerializeField]
        CommandListData? _targetData;

        PropertyTree? _propertyTree;
        InspectorProperty? _commandListProperty;

        Vector2 _scrollPosition;
        static Type[]? _commandSourceTypes;

        bool _needsRefresh;
        bool _rootExpandedInitialized;

        /// <summary>
        /// CommandListDataを別ウィンドウで開く
        /// </summary>
        public static CommandListEditorWindow Open(
            string fieldPath,
            CommandListData targetData,
            UnityEngine.Object ownerObject)
        {
            var window = CreateInstance<CommandListEditorWindow>();
            window.minSize = new Vector2(MinWidth, MinHeight);
            window.Initialize(fieldPath, targetData, ownerObject);
            window.Show();
            return window;
        }

        void Initialize(string fieldPath, CommandListData targetData, UnityEngine.Object ownerObject)
        {
            _ownerObject = ownerObject;
            _fieldPath = fieldPath;
            _functionName = targetData.FunctionName;
            _targetData = targetData;

            CreatePropertyTree();
            UpdateWindowTitle();
        }

        void CreatePropertyTree()
        {
            DisposePropertyTree();
            if (_ownerObject == null || !_ownerObject)
                return;

            var serializedObject = new SerializedObject(_ownerObject);
            _propertyTree = PropertyTree.Create(serializedObject);
            _propertyTree.UpdateTree();
            _commandListProperty = ResolveCommandListProperty(_propertyTree, _fieldPath, _targetData);
            _rootExpandedInitialized = false;
        }

        void DisposePropertyTree()
        {
            _commandListProperty = null;
            _rootExpandedInitialized = false;

            if (_propertyTree != null)
            {
                _propertyTree.Dispose();
                _propertyTree = null;
            }
        }

        void UpdateWindowTitle()
        {
            var ownerName = _ownerObject != null ? _ownerObject.name : "Unknown";
            var funcName = string.IsNullOrEmpty(_functionName) ? "Unnamed" : _functionName;
            titleContent = new GUIContent($"{WindowTitle} - {funcName} ({ownerName})");
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            if (_propertyTree == null && _ownerObject != null)
                CreatePropertyTree();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            DisposePropertyTree();
        }

        void OnDestroy()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            DisposePropertyTree();
        }

        void OnBeforeAssemblyReload()
        {
            DisposePropertyTree();
        }

        void OnUndoRedo()
        {
            _needsRefresh = true;
            Repaint();
        }

        void OnGUI()
        {
            if (_needsRefresh)
            {
                _needsRefresh = false;
                CreatePropertyTree();
            }

            if (_ownerObject == null)
            {
                EditorGUILayout.HelpBox("No target object. Please reopen the window.", MessageType.Info);
                return;
            }

            if (!_ownerObject)
            {
                EditorGUILayout.HelpBox("Target object has been destroyed.", MessageType.Warning);
                if (GUILayout.Button("Close"))
                    Close();
                return;
            }

            if (_propertyTree == null)
                CreatePropertyTree();

            DrawToolbar();
            DrawCommandList();
            DrawFooter();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var funcLabel = string.IsNullOrEmpty(_functionName) ? "<Unnamed>" : _functionName;
            EditorGUILayout.LabelField($"Function: {funcLabel}", EditorStyles.boldLabel, GUILayout.Width(200));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select Owner", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                if (_ownerObject != null)
                {
                    Selection.activeObject = _ownerObject;
                    EditorGUIUtility.PingObject(_ownerObject);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawCommandList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_propertyTree != null)
            {
                _propertyTree.UpdateTree();
                _propertyTree.BeginDraw(true);
                try
                {
                    _commandListProperty = ResolveCommandListProperty(_propertyTree, _fieldPath, _targetData);
                    if (_commandListProperty != null)
                    {
                        EnsureRootExpanded(_commandListProperty);
                        DrawChildSafely(_commandListProperty);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"CommandListData property not found. path='{_fieldPath}'", MessageType.Warning);
                    }
                }
                finally
                {
                    _propertyTree.EndDraw();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("PropertyTree is not initialized.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        static void DrawChildSafely(InspectorProperty child)
        {
            if (child == null)
                return;

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            try
            {
                child.Draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
            }
        }

        static InspectorProperty? ResolveCommandListProperty(PropertyTree tree, string fieldPath, CommandListData? expectedData)
        {
            if (tree == null)
                return null;

            if (!string.IsNullOrEmpty(fieldPath))
            {
                var byUnityPath = tree.GetPropertyAtUnityPath(fieldPath);
                if (byUnityPath != null)
                    return byUnityPath;

                var byOdinPath = tree.GetPropertyAtPath(fieldPath);
                if (byOdinPath != null)
                    return byOdinPath;
            }

            if (expectedData != null)
            {
                foreach (var property in tree.EnumerateTree(includeChildren: true, onlyVisible: false))
                {
                    var value = property?.ValueEntry?.WeakSmartValue as CommandListData;
                    if (value == null)
                        continue;
                    if (ReferenceEquals(value, expectedData))
                        return property;
                }
            }

            for (int i = 0; i < tree.RootProperty.Children.Count; i++)
            {
                var child = tree.RootProperty.Children[i];
                var valueType = child?.ValueEntry?.TypeOfValue;
                if (valueType == null)
                    continue;
                if (!typeof(CommandListData).IsAssignableFrom(valueType))
                    continue;
                return child;
            }

            return null;
        }

        void EnsureRootExpanded(InspectorProperty commandListProperty)
        {
            if (_rootExpandedInitialized)
                return;

            commandListProperty.State.Expanded = true;
            _rootExpandedInitialized = true;
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("+ Add Command", GUILayout.Height(FooterHeight - 4)))
                ShowAddCommandMenu();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(FooterHeight - 4)))
            {
                CreatePropertyTree();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        void ShowAddCommandMenu()
        {
            var types = GetCommandSourceTypes();
            var menu = new GenericMenu();

            foreach (var type in types)
            {
                var t = type;
                menu.AddItem(new GUIContent(t.Name), false, () => AddCommand(t));
            }

            menu.ShowAsContext();
        }

        void AddCommand(Type commandSourceType)
        {
            if (_ownerObject == null)
                return;

            if (commandSourceType.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning($"[CommandListEditorWindow] Type {commandSourceType.Name} has no default constructor.");
                return;
            }

            var targetData = _commandListProperty?.ValueEntry?.WeakSmartValue as CommandListData ?? _targetData;
            if (targetData == null)
                return;

            Undo.RecordObject(_ownerObject, "Add Command");

            var instance = (ICommandSource)Activator.CreateInstance(commandSourceType);
            targetData.Add(instance);

            EditorUtility.SetDirty(_ownerObject);
            CreatePropertyTree();
            Repaint();
        }

        static IEnumerable<Type> GetCommandSourceTypes()
        {
            if (_commandSourceTypes != null)
                return _commandSourceTypes;

            var types = TypeCache.GetTypesDerivedFrom<ICommandSource>();
            var list = new List<Type>();

            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                list.Add(t);
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _commandSourceTypes = list.ToArray();
            return _commandSourceTypes;
        }
    }
}
#endif

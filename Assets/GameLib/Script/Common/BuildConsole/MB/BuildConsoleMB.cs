#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using VContainer;
using VContainer.Unity;

namespace Game.BuildConsole
{
    [DisallowMultipleComponent]
    public sealed class BuildConsoleMB : MonoBehaviour, IScopeInstaller
    {
        static readonly Color32 WindowBackgroundColor = new(10, 13, 18, 245);
        static readonly Color32 PanelBackgroundColor = new(18, 24, 34, 255);
        static readonly Color32 CardBackgroundColor = new(24, 31, 42, 255);
        static readonly Color32 SelectedCardBackgroundColor = new(34, 39, 47, 255);
        static readonly Color32 DetailBackgroundColor = new(12, 17, 24, 255);
        static readonly Color32 ButtonBackgroundColor = new(39, 48, 63, 255);
        static readonly Color32 ButtonHoverBackgroundColor = new(54, 66, 86, 255);
        static readonly Color32 ButtonActiveBackgroundColor = new(70, 84, 108, 255);
        static readonly Color32 DangerButtonBackgroundColor = new(93, 47, 55, 255);
        static readonly Color32 DangerButtonHoverBackgroundColor = new(118, 59, 69, 255);
        static readonly Color32 DangerButtonActiveBackgroundColor = new(143, 73, 86, 255);
        static readonly Color32 SearchBackgroundColor = new(8, 11, 17, 255);
        static readonly Color32 WhiteColor = new(255, 255, 255, 255);
        static readonly Color32 PrimaryTextColor = new(240, 244, 250, 255);
        static readonly Color32 SecondaryTextColor = new(136, 148, 168, 255);

        [FoldoutGroup("Runtime")]
        [LabelText("Enable In Editor")]
        [SerializeField] bool enableInEditor = true;

        [FoldoutGroup("Runtime")]
        [LabelText("Capture Unity Logs")]
        [SerializeField] bool captureUnityLogs = true;

        [FoldoutGroup("Runtime")]
        [LabelText("Capture Stack Trace")]
        [SerializeField] bool captureStackTrace = true;

        [FoldoutGroup("Runtime")]
        [LabelText("Visible On Start")]
        [SerializeField] bool visibleOnStart;

        [FoldoutGroup("Runtime")]
        [LabelText("Max Entries")]
        [MinValue(16)]
        [SerializeField] int maxEntries = 512;

        [FoldoutGroup("Runtime")]
        [LabelText("Preview Character Limit")]
        [MinValue(32)]
        [SerializeField] int previewCharacterLimit = 220;

        [FoldoutGroup("Layout")]
        [LabelText("Window Width")]
        [MinValue(320f)]
        [SerializeField] float windowWidth = 1040f;

        [FoldoutGroup("Layout")]
        [LabelText("Window Height")]
        [MinValue(240f)]
        [SerializeField] float windowHeight = 680f;

        [FoldoutGroup("Layout")]
        [LabelText("Window Margin")]
        [MinValue(0f)]
        [SerializeField] float windowMargin = 16f;

        [FoldoutGroup("Layout")]
        [LabelText("Header Font Size")]
        [MinValue(10)]
        [SerializeField] int headerFontSize = 14;

        [FoldoutGroup("Layout")]
        [LabelText("Row Font Size")]
        [MinValue(10)]
        [SerializeField] int rowFontSize = 12;

        [FoldoutGroup("Layout")]
        [LabelText("Detail Font Size")]
        [MinValue(10)]
        [SerializeField] int detailFontSize = 11;

        IBuildConsole? _console;
        Rect _windowRect;
        Vector2 _scroll;
        string _search = string.Empty;
        readonly HashSet<int> _expandedEntries = new();
        readonly HashSet<int> _selectedEntries = new();
        int _selectionAnchorSequence = -1;
        int _lastCopyFrame = -1;

        GUIStyle? _windowStyle;
        GUIStyle? _headerPanelStyle;
        GUIStyle? _titleLabelStyle;
        GUIStyle? _subtitleLabelStyle;
        GUIStyle? _toolbarLabelStyle;
        GUIStyle? _searchTextFieldStyle;
        GUIStyle? _toolbarButtonStyle;
        GUIStyle? _dangerButtonStyle;
        GUIStyle? _entryCardStyle;
        GUIStyle? _selectedEntryCardStyle;
        GUIStyle? _expandButtonStyle;
        GUIStyle? _timeLabelStyle;
        GUIStyle? _previewLabelStyle;
        GUIStyle? _badgeStyle;
        GUIStyle? _detailPanelStyle;
        GUIStyle? _detailSectionLabelStyle;
        GUIStyle? _detailTextStyle;
        GUIStyle? _emptyStateStyle;

        Texture2D? _windowBackgroundTexture;
        Texture2D? _panelBackgroundTexture;
        Texture2D? _cardBackgroundTexture;
        Texture2D? _selectedCardBackgroundTexture;
        Texture2D? _detailBackgroundTexture;
        Texture2D? _buttonBackgroundTexture;
        Texture2D? _buttonHoverBackgroundTexture;
        Texture2D? _buttonActiveBackgroundTexture;
        Texture2D? _dangerButtonBackgroundTexture;
        Texture2D? _dangerButtonHoverBackgroundTexture;
        Texture2D? _dangerButtonActiveBackgroundTexture;
        Texture2D? _searchBackgroundTexture;
        Texture2D? _badgeBackgroundTexture;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;

            builder.RegisterInstance(new BuildConsoleOptions
            {
                EnableInEditor = enableInEditor,
                CaptureUnityLogs = captureUnityLogs,
                CaptureStackTrace = captureStackTrace,
                VisibleOnStart = visibleOnStart,
                MaxEntries = Mathf.Max(16, maxEntries),
                PreviewCharacterLimit = Mathf.Max(32, previewCharacterLimit),
                WindowWidth = Mathf.Max(320f, windowWidth),
                WindowHeight = Mathf.Max(240f, windowHeight),
                Margin = Mathf.Max(0f, windowMargin),
                HeaderFontSize = Mathf.Max(10, headerFontSize),
                RowFontSize = Mathf.Max(10, rowFontSize),
                DetailFontSize = Mathf.Max(10, detailFontSize),
            });

            builder.Register<BuildConsoleService>(RuntimeLifetime.Singleton)
                .As<IBuildConsole>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>()
                .As<IDisposable>();

            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IBuildConsole>(out var console) && console != null)
                {
                    _console = console;
                }
            });
        }

        void OnDestroy()
        {
            DestroyTexture(ref _windowBackgroundTexture);
            DestroyTexture(ref _panelBackgroundTexture);
            DestroyTexture(ref _cardBackgroundTexture);
            DestroyTexture(ref _selectedCardBackgroundTexture);
            DestroyTexture(ref _detailBackgroundTexture);
            DestroyTexture(ref _buttonBackgroundTexture);
            DestroyTexture(ref _buttonHoverBackgroundTexture);
            DestroyTexture(ref _buttonActiveBackgroundTexture);
            DestroyTexture(ref _dangerButtonBackgroundTexture);
            DestroyTexture(ref _dangerButtonHoverBackgroundTexture);
            DestroyTexture(ref _dangerButtonActiveBackgroundTexture);
            DestroyTexture(ref _searchBackgroundTexture);
            DestroyTexture(ref _badgeBackgroundTexture);
        }

        void OnGUI()
        {
            if (_console == null || !_console.IsVisible)
            {
                return;
            }

            if (Application.isEditor && !enableInEditor)
            {
                return;
            }

            EnsureWindowRect();
            EnsureStyles(_console.Options);
            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, string.Empty, _windowStyle!);
        }

        void DrawWindow(int windowId)
        {
            if (_console == null)
            {
                return;
            }

            var entries = _console.Entries;
            PruneSelection(entries);
            HandleKeyboardShortcuts(entries);
            HandleCopyShortcut(entries);
            var filteredCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (MatchesSearch(entries[i]))
                {
                    filteredCount++;
                }
            }

            GUILayout.BeginVertical();
            DrawHeader(entries.Count, filteredCount);
            GUILayout.Space(10f);
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);

            if (filteredCount == 0)
            {
                DrawEmptyState(entries.Count == 0
                    ? "No logs captured yet."
                    : "No entries matched the current search.");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!MatchesSearch(entry))
                    {
                        continue;
                    }

                    DrawEntry(entries, entry);
                    GUILayout.Space(6f);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 48f));
        }

        void DrawHeader(int totalCount, int filteredCount)
        {
            GUILayout.BeginVertical(_headerPanelStyle!);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Console", _titleLabelStyle!);
            GUILayout.FlexibleSpace();
            DrawBadge("F2", new Color32(71, 127, 255, 255), minWidth: 42f, maxWidth: 42f);
            GUILayout.Space(6f);
            GUILayout.Label("Toggle", _subtitleLabelStyle!, GUILayout.Width(46f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Entries {filteredCount}/{totalCount}", _subtitleLabelStyle!);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Selected {_selectedEntries.Count}", _subtitleLabelStyle!);
            GUILayout.Space(12f);
            GUILayout.Label(captureUnityLogs ? "Unity capture on" : "Unity capture off", _subtitleLabelStyle!);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _toolbarLabelStyle!, GUILayout.Width(52f));
            _search = GUILayout.TextField(_search, _searchTextFieldStyle!, GUILayout.Height(28f), GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Clear", _toolbarButtonStyle!, GUILayout.Width(66f), GUILayout.Height(28f)))
            {
                _search = string.Empty;
            }

            if (GUILayout.Button("Copy", _toolbarButtonStyle!, GUILayout.Width(66f), GUILayout.Height(28f)))
            {
                _ = CopySelectedEntriesToClipboard(_console?.Entries);
            }

            if (GUILayout.Button("Clear Logs", _dangerButtonStyle!, GUILayout.Width(92f), GUILayout.Height(28f)))
            {
                _console?.Clear();
                _expandedEntries.Clear();
                _selectedEntries.Clear();
                _selectionAnchorSequence = -1;
            }

            if (GUILayout.Button("Collapse All", _toolbarButtonStyle!, GUILayout.Width(104f), GUILayout.Height(28f)))
            {
                _expandedEntries.Clear();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void DrawEntry(IReadOnlyList<BuildConsoleEntry> entries, BuildConsoleEntry entry)
        {
            var expanded = _expandedEntries.Contains(entry.Sequence);
            const float rowHeight = 26f;
            var detailHeight = expanded ? GetExpandedEntryHeight(entry) : 0f;
            var totalHeight = rowHeight + (expanded ? 8f + detailHeight : 0f);
            var outerRect = GUILayoutUtility.GetRect(0f, totalHeight, GUILayout.ExpandWidth(true));
            var selected = _selectedEntries.Contains(entry.Sequence);

            GUI.Box(outerRect, GUIContent.none, selected ? _selectedEntryCardStyle! : _entryCardStyle!);

            var rowRect = new Rect(outerRect.x + 10f, outerRect.y + 3f, outerRect.width - 20f, rowHeight);
            const float expandSize = 24f;
            var expandRect = new Rect(rowRect.xMax - expandSize, rowRect.y + (rowRect.height - expandSize) * 0.5f, expandSize, expandSize);
            var barsRect = new Rect(rowRect.x, rowRect.y + 1f, 14f, rowRect.height - 2f);
            var timeRect = new Rect(barsRect.xMax + 8f, rowRect.y - 1f, 66f, rowRect.height);
            var previewRect = new Rect(timeRect.xMax + 8f, rowRect.y - 1f, expandRect.x - timeRect.xMax - 14f, rowRect.height);

            DrawIndicatorBars(barsRect, entry);
            GUI.Label(timeRect, $"{entry.RealtimeSeconds:0000.00}", _timeLabelStyle!);
            GUI.Label(previewRect, BuildPreviewRichText(entry), _previewLabelStyle!);

            if (GUI.Button(expandRect, expanded ? "-" : "+", _expandButtonStyle!))
            {
                ToggleExpanded(entry.Sequence);
            }

            HandleEntryMouse(entries, entry, rowRect, expandRect);

            if (!expanded)
            {
                return;
            }

            var detailRect = new Rect(outerRect.x + 10f, rowRect.yMax + 6f, outerRect.width - 20f, detailHeight);
            DrawExpandedEntry(entry, detailRect);
        }

        void DrawExpandedEntry(BuildConsoleEntry entry, Rect detailRect)
        {
            GUI.Box(detailRect, GUIContent.none, _detailPanelStyle!);

            const float inset = 8f;
            var innerX = detailRect.x + inset;
            var innerY = detailRect.y + inset;
            var innerWidth = detailRect.width - inset * 2f;
            var messageText = BuildDetailMessage(entry);
            var messageHeight = _detailTextStyle!.CalcHeight(new GUIContent(messageText), innerWidth);
            var messageRect = new Rect(innerX, innerY, innerWidth, messageHeight);
            GUI.Label(messageRect, messageText, _detailTextStyle!);

            if (string.IsNullOrEmpty(entry.StackTrace))
            {
                return;
            }

            innerY = messageRect.yMax + 8f;
            var stackLabelHeight = _detailSectionLabelStyle!.CalcHeight(new GUIContent("Stack Trace"), innerWidth);
            var stackLabelRect = new Rect(innerX, innerY, innerWidth, stackLabelHeight);
            GUI.Label(stackLabelRect, "Stack Trace", _detailSectionLabelStyle!);

            innerY = stackLabelRect.yMax + 2f;
            var stackHeight = _detailTextStyle.CalcHeight(new GUIContent(entry.StackTrace), innerWidth);
            var stackRect = new Rect(innerX, innerY, innerWidth, stackHeight);
            GUI.Label(stackRect, entry.StackTrace, _detailTextStyle!);
        }

        void DrawEmptyState(string message)
        {
            GUILayout.BeginVertical(_entryCardStyle!);
            GUILayout.Space(12f);
            GUILayout.Label(message, _emptyStateStyle!);
            GUILayout.Space(12f);
            GUILayout.EndVertical();
        }

        void DrawBadge(string label, Color backgroundColor, float minWidth, float maxWidth)
        {
            var previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            GUILayout.Box(
                label,
                _badgeStyle!,
                GUILayout.Width(GetBadgeWidth(label, minWidth, maxWidth)),
                GUILayout.Height(22f));
            GUI.backgroundColor = previousBackgroundColor;
        }

        void ToggleExpanded(int sequence)
        {
            if (!_expandedEntries.Add(sequence))
            {
                _expandedEntries.Remove(sequence);
            }
        }

        void HandleEntryMouse(IReadOnlyList<BuildConsoleEntry> entries, BuildConsoleEntry entry, Rect rowRect, Rect expandRect)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {
                return;
            }

            if (!rowRect.Contains(currentEvent.mousePosition) || expandRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            SelectEntry(entries, entry.Sequence, currentEvent.shift, currentEvent.control || currentEvent.command);
            GUI.FocusControl(null);

            if (currentEvent.clickCount >= 2)
            {
                ToggleExpanded(entry.Sequence);
            }

            currentEvent.Use();
        }

        void HandleKeyboardShortcuts(IReadOnlyList<BuildConsoleEntry> entries)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            var isCopy = (currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.C;
            if (!isCopy)
            {
                return;
            }

            if (CopySelectedEntriesToClipboard(entries))
            {
                currentEvent.Use();
            }
        }

        void HandleCopyShortcut(IReadOnlyList<BuildConsoleEntry> entries)
        {
            if (!IsCopyPressed() || _lastCopyFrame == Time.frameCount)
            {
                return;
            }

            if (CopySelectedEntriesToClipboard(entries))
            {
                _lastCopyFrame = Time.frameCount;
            }
        }

        void SelectEntry(IReadOnlyList<BuildConsoleEntry> entries, int sequence, bool shiftPressed, bool ctrlPressed)
        {
            if (ctrlPressed)
            {
                if (!_selectedEntries.Add(sequence))
                {
                    _selectedEntries.Remove(sequence);
                }

                _selectionAnchorSequence = sequence;
                return;
            }

            if (!shiftPressed || _selectionAnchorSequence < 0)
            {
                _selectedEntries.Clear();
                _selectedEntries.Add(sequence);
                _selectionAnchorSequence = sequence;
                return;
            }

            var anchorIndex = FindEntryIndex(entries, _selectionAnchorSequence);
            var currentIndex = FindEntryIndex(entries, sequence);
            if (anchorIndex < 0 || currentIndex < 0)
            {
                _selectedEntries.Clear();
                _selectedEntries.Add(sequence);
                _selectionAnchorSequence = sequence;
                return;
            }

            if (anchorIndex > currentIndex)
            {
                (anchorIndex, currentIndex) = (currentIndex, anchorIndex);
            }

            _selectedEntries.Clear();
            for (int i = anchorIndex; i <= currentIndex; i++)
            {
                _selectedEntries.Add(entries[i].Sequence);
            }
        }

        void PruneSelection(IReadOnlyList<BuildConsoleEntry> entries)
        {
            if (_selectedEntries.Count > 0)
            {
                List<int>? staleSelections = null;
                foreach (var sequence in _selectedEntries)
                {
                    if (FindEntryIndex(entries, sequence) >= 0)
                    {
                        continue;
                    }

                    staleSelections ??= new List<int>();
                    staleSelections.Add(sequence);
                }

                if (staleSelections != null)
                {
                    for (int i = 0; i < staleSelections.Count; i++)
                    {
                        _selectedEntries.Remove(staleSelections[i]);
                    }
                }
            }

            if (_selectionAnchorSequence >= 0 && FindEntryIndex(entries, _selectionAnchorSequence) < 0)
            {
                _selectionAnchorSequence = -1;
            }
        }

        bool CopySelectedEntriesToClipboard(IReadOnlyList<BuildConsoleEntry>? entries)
        {
            if (entries == null || _selectedEntries.Count == 0)
            {
                return false;
            }

            var builder = new StringBuilder();
            var copiedCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!_selectedEntries.Contains(entry.Sequence))
                {
                    continue;
                }

                if (copiedCount > 0)
                {
                    builder.Append('\n');
                    builder.Append('\n');
                }

                builder.Append(BuildDetailMessage(entry));
                if (!string.IsNullOrEmpty(entry.StackTrace))
                {
                    builder.Append('\n');
                    builder.Append(entry.StackTrace);
                }

                copiedCount++;
            }

            if (copiedCount == 0)
            {
                return false;
            }

            CopyTextToClipboard(builder.ToString());
            return true;
        }

        static void CopyTextToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = text;

            var textEditor = new TextEditor
            {
                text = text,
            };
            textEditor.SelectAll();
            textEditor.Copy();
        }

        static bool IsCopyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var isModifierPressed = Keyboard.current.leftCtrlKey.isPressed ||
                                        Keyboard.current.rightCtrlKey.isPressed ||
                                        Keyboard.current.leftCommandKey.isPressed ||
                                        Keyboard.current.rightCommandKey.isPressed;
                if (isModifierPressed && Keyboard.current.cKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            {
                return true;
            }
#endif
            return false;
        }

        float GetExpandedEntryHeight(BuildConsoleEntry entry)
        {
            var detailWidth = Mathf.Max(180f, _windowRect.width - 68f);
            var messageHeight = _detailTextStyle!.CalcHeight(new GUIContent(BuildDetailMessage(entry)), detailWidth);
            var totalHeight = 16f + messageHeight + 8f;

            if (string.IsNullOrEmpty(entry.StackTrace))
            {
                return totalHeight;
            }

            var stackLabelHeight = _detailSectionLabelStyle!.CalcHeight(new GUIContent("Stack Trace"), detailWidth);
            var stackHeight = _detailTextStyle.CalcHeight(new GUIContent(entry.StackTrace), detailWidth);
            return totalHeight + 8f + stackLabelHeight + 2f + stackHeight;
        }

        void DrawIndicatorBars(Rect rect, BuildConsoleEntry entry)
        {
            var sourceRect = new Rect(rect.x, rect.y, 5f, rect.height);
            var logRect = new Rect(rect.x + 8f, rect.y, 5f, rect.height);
            DrawSolidRect(sourceRect, GetSourceBadgeColor(entry.Source));
            DrawSolidRect(logRect, GetLogBadgeColor(entry.LogType));
        }

        static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }

        static int FindEntryIndex(IReadOnlyList<BuildConsoleEntry> entries, int sequence)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Sequence == sequence)
                {
                    return i;
                }
            }

            return -1;
        }

        bool MatchesSearch(BuildConsoleEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            var needle = _search.Trim();
            return ContainsIgnoreCase(entry.SourceLabel, needle) ||
                   ContainsIgnoreCase(entry.ScopePrefix, needle) ||
                   ContainsIgnoreCase(entry.LogType.ToString(), needle) ||
                   ContainsIgnoreCase(entry.Preview, needle) ||
                   ContainsIgnoreCase(entry.Message, needle) ||
                   ContainsIgnoreCase(entry.StackTrace, needle);
        }

        void EnsureWindowRect()
        {
            if (_windowRect.width > 0f && _windowRect.height > 0f)
            {
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
                return;
            }

            var options = _console?.Options;
            var margin = options?.Margin ?? Mathf.Max(0f, windowMargin);
            var width = Mathf.Min(Screen.width - margin * 2f, options?.WindowWidth ?? windowWidth);
            var height = Mathf.Min(Screen.height - margin * 2f, options?.WindowHeight ?? windowHeight);
            width = Mathf.Max(320f, width);
            height = Mathf.Max(240f, height);
            _windowRect = new Rect(margin, margin, width, height);
        }

        void EnsureStyles(BuildConsoleOptions options)
        {
            EnsureTextures();

            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.window)
                {
                    padding = new RectOffset(14, 14, 14, 14),
                    border = new RectOffset(2, 2, 2, 2),
                    fontSize = options.HeaderFontSize,
                };
                ApplyBackground(_windowStyle, _windowBackgroundTexture!);
            }

            if (_headerPanelStyle == null)
            {
                _headerPanelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 12, 12),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                ApplyBackground(_headerPanelStyle, _panelBackgroundTexture!);
            }

            if (_titleLabelStyle == null)
            {
                _titleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = options.HeaderFontSize + 4,
                    fontStyle = FontStyle.Bold,
                    richText = false,
                };
                SetAllTextColors(_titleLabelStyle, PrimaryTextColor);
            }

            if (_subtitleLabelStyle == null)
            {
                _subtitleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, options.RowFontSize - 1),
                    richText = false,
                };
                SetAllTextColors(_subtitleLabelStyle, SecondaryTextColor);
            }

            if (_toolbarLabelStyle == null)
            {
                _toolbarLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = options.RowFontSize,
                    alignment = TextAnchor.MiddleLeft,
                };
                SetAllTextColors(_toolbarLabelStyle, SecondaryTextColor);
            }

            if (_searchTextFieldStyle == null)
            {
                _searchTextFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = options.RowFontSize,
                    padding = new RectOffset(10, 10, 6, 6),
                };
                ApplyBackground(_searchTextFieldStyle, _searchBackgroundTexture!);
                SetAllTextColors(_searchTextFieldStyle, PrimaryTextColor);
            }

            if (_toolbarButtonStyle == null)
            {
                _toolbarButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = options.RowFontSize,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(10, 10, 6, 6),
                };
                ApplyButtonBackground(
                    _toolbarButtonStyle,
                    _buttonBackgroundTexture!,
                    _buttonHoverBackgroundTexture!,
                    _buttonActiveBackgroundTexture!);
                SetAllTextColors(_toolbarButtonStyle, PrimaryTextColor);
            }

            if (_dangerButtonStyle == null)
            {
                _dangerButtonStyle = new GUIStyle(_toolbarButtonStyle!)
                {
                    fontSize = options.RowFontSize,
                };
                ApplyButtonBackground(
                    _dangerButtonStyle,
                    _dangerButtonBackgroundTexture!,
                    _dangerButtonHoverBackgroundTexture!,
                    _dangerButtonActiveBackgroundTexture!);
                SetAllTextColors(_dangerButtonStyle, PrimaryTextColor);
            }

            if (_entryCardStyle == null)
            {
                _entryCardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 10, 10),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                ApplyBackground(_entryCardStyle, _cardBackgroundTexture!);
            }

            if (_selectedEntryCardStyle == null)
            {
                _selectedEntryCardStyle = new GUIStyle(_entryCardStyle)
                {
                    padding = new RectOffset(12, 12, 10, 10),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                ApplyBackground(_selectedEntryCardStyle, _selectedCardBackgroundTexture!);
            }

            if (_expandButtonStyle == null)
            {
                _expandButtonStyle = new GUIStyle(_toolbarButtonStyle!)
                {
                    fontSize = Mathf.Max(12, options.RowFontSize),
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleCenter,
                    contentOffset = new Vector2(0f, -3f),
                };
            }

            if (_timeLabelStyle == null)
            {
                _timeLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, options.RowFontSize - 1),
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    contentOffset = new Vector2(0f, -2f),
                };
                SetAllTextColors(_timeLabelStyle, SecondaryTextColor);
            }

            if (_previewLabelStyle == null)
            {
                _previewLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = options.RowFontSize,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    richText = true,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    contentOffset = new Vector2(0f, -2f),
                };
                SetAllTextColors(_previewLabelStyle, PrimaryTextColor);
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.Max(10, options.RowFontSize - 1),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(8, 8, 3, 3),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                ApplyBackground(_badgeStyle, _badgeBackgroundTexture!);
                SetAllTextColors(_badgeStyle, WhiteColor);
            }

            if (_detailPanelStyle == null)
            {
                _detailPanelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 12, 12),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                ApplyBackground(_detailPanelStyle, _detailBackgroundTexture!);
            }

            if (_detailSectionLabelStyle == null)
            {
                _detailSectionLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, options.RowFontSize - 1),
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(0, 0, 0, 0),
                    contentOffset = new Vector2(0f, -2f),
                };
                SetAllTextColors(_detailSectionLabelStyle, SecondaryTextColor);
            }

            if (_detailTextStyle == null)
            {
                _detailTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = options.DetailFontSize,
                    wordWrap = true,
                    richText = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.UpperLeft,
                    contentOffset = new Vector2(0f, -2f),
                };
                SetAllTextColors(_detailTextStyle, PrimaryTextColor);
            }

            if (_emptyStateStyle == null)
            {
                _emptyStateStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = options.RowFontSize,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                };
                SetAllTextColors(_emptyStateStyle, SecondaryTextColor);
            }
        }

        void EnsureTextures()
        {
            _windowBackgroundTexture ??= CreateSolidTexture(WindowBackgroundColor);
            _panelBackgroundTexture ??= CreateSolidTexture(PanelBackgroundColor);
            _cardBackgroundTexture ??= CreateSolidTexture(CardBackgroundColor);
            _selectedCardBackgroundTexture ??= CreateSolidTexture(SelectedCardBackgroundColor);
            _detailBackgroundTexture ??= CreateSolidTexture(DetailBackgroundColor);
            _buttonBackgroundTexture ??= CreateSolidTexture(ButtonBackgroundColor);
            _buttonHoverBackgroundTexture ??= CreateSolidTexture(ButtonHoverBackgroundColor);
            _buttonActiveBackgroundTexture ??= CreateSolidTexture(ButtonActiveBackgroundColor);
            _dangerButtonBackgroundTexture ??= CreateSolidTexture(DangerButtonBackgroundColor);
            _dangerButtonHoverBackgroundTexture ??= CreateSolidTexture(DangerButtonHoverBackgroundColor);
            _dangerButtonActiveBackgroundTexture ??= CreateSolidTexture(DangerButtonActiveBackgroundColor);
            _searchBackgroundTexture ??= CreateSolidTexture(SearchBackgroundColor);
            _badgeBackgroundTexture ??= CreateSolidTexture(WhiteColor);
        }

        static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static void DestroyTexture(ref Texture2D? texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            texture = null;
        }

        static void ApplyBackground(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        static void ApplyButtonBackground(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.onFocused.background = hover;
        }

        static void SetAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        static float GetBadgeWidth(string label, float minWidth, float maxWidth)
        {
            var width = 18f + Mathf.Ceil(label.Length * 7.2f);
            width = Mathf.Max(minWidth, width);
            width = Mathf.Min(maxWidth, width);
            return width;
        }

        static bool ContainsIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string BuildPreviewRichText(BuildConsoleEntry entry)
        {
            var preview = EscapeRichText(entry.Preview);
            if (entry.HasScope)
            {
                return $"<color=#8E9AB0>{EscapeRichText(entry.ScopePrefix)}</color> <color=#{GetLogColorHex(entry.LogType)}>{preview}</color>";
            }

            return $"<color=#{GetLogColorHex(entry.LogType)}>{preview}</color>";
        }

        static string BuildDetailMessage(BuildConsoleEntry entry)
        {
            return string.IsNullOrEmpty(entry.ScopePrefix)
                ? entry.Message
                : $"{entry.ScopePrefix} {entry.Message}";
        }

        static string GetLogTypeLabel(LogType logType)
        {
            return logType switch
            {
                LogType.Warning => "Warning",
                LogType.Error => "Error",
                LogType.Assert => "Assert",
                LogType.Exception => "Exception",
                _ => "Log",
            };
        }

        static Color32 GetSourceBadgeColor(BuildConsoleEntrySource source)
        {
            return source switch
            {
                BuildConsoleEntrySource.BuildConsole => new Color32(61, 138, 255, 255),
                BuildConsoleEntrySource.UnityLog => new Color32(101, 114, 133, 255),
                _ => new Color32(92, 102, 120, 255),
            };
        }

        static Color32 GetScopeBadgeColor(LifetimeScopeKind kind)
        {
            return kind switch
            {
                LifetimeScopeKind.Project => new Color32(87, 199, 255, 255),
                LifetimeScopeKind.Platform => new Color32(255, 179, 71, 255),
                LifetimeScopeKind.Global => new Color32(255, 209, 102, 255),
                LifetimeScopeKind.Scene => new Color32(122, 214, 109, 255),
                LifetimeScopeKind.Field => new Color32(126, 224, 212, 255),
                LifetimeScopeKind.Entity => new Color32(255, 140, 130, 255),
                LifetimeScopeKind.UI => new Color32(243, 168, 255, 255),
                LifetimeScopeKind.UIElement => new Color32(214, 168, 255, 255),
                LifetimeScopeKind.Runtime => new Color32(255, 167, 86, 255),
                _ => new Color32(134, 148, 168, 255),
            };
        }

        static Color32 GetLogBadgeColor(LogType logType)
        {
            return logType switch
            {
                LogType.Warning => new Color32(214, 168, 62, 255),
                LogType.Error => new Color32(220, 88, 88, 255),
                LogType.Assert => new Color32(220, 88, 88, 255),
                LogType.Exception => new Color32(205, 71, 110, 255),
                _ => new Color32(51, 160, 122, 255),
            };
        }

        static string GetLogColorHex(LogType logType)
        {
            return logType switch
            {
                LogType.Warning => "FFD166",
                LogType.Error => "FF7B7B",
                LogType.Assert => "FF7B7B",
                LogType.Exception => "FF5C8A",
                _ => "F4F7FB",
            };
        }

        static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}


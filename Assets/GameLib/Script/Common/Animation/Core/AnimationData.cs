using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using VNext = Game.Commands.VNext;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

namespace Game.Animation
{
    public interface IAnimationData
    {
        string AnimationName { get; }
        float DefaultFrameDuration { get; }
        bool AwaitFrameCommands { get; }

        VNext.CommandListData OnStart { get; }
        VNext.CommandListData OnEnd { get; }
        VNext.CommandListData OnCanceled { get; }

        IReadOnlyList<IAnimationFrame> Frames { get; }
    }

    public interface IAnimationFrame
    {
        Sprite Sprite { get; }
        float Duration { get; }
        VNext.CommandListData Commands { get; }
    }

    public enum AnimationDataSourceKind
    {
        Asset,
        Inline,
        RandomAsset,
        RandomInline,
    }

    [Serializable]
    public sealed class AnimationDataSource
    {
        [HorizontalGroup("Header", Width = 80), HideLabel]
        public AnimationDataSourceKind kind = AnimationDataSourceKind.Asset;

        [ShowIf(nameof(IsAsset))]
        [AssetOrInternal]
        [LabelText("Animation")]
        public AnimationData asset;

        [ShowIf(nameof(IsInline))]
        [HideLabel]
        public InlineAnimationData inline = new();

        [ShowIf(nameof(IsRandomAsset))]
        [HideLabel]
        public RandomAssetAnimationData randomAsset = new();

        [ShowIf(nameof(IsRandomInline))]
        [HideLabel]
        public RandomInlineAnimationData randomInline = new();

        bool IsAsset => kind == AnimationDataSourceKind.Asset;
        bool IsInline => kind == AnimationDataSourceKind.Inline;
        bool IsRandomAsset => kind == AnimationDataSourceKind.RandomAsset;
        bool IsRandomInline => kind == AnimationDataSourceKind.RandomInline;

        public bool TryGet(out IAnimationData data)
        {
            switch (kind)
            {
                case AnimationDataSourceKind.Asset:
                    data = asset;
                    return data != null;
                case AnimationDataSourceKind.Inline:
                    data = inline;
                    return InlineAnimationData.IsPlayable(inline);
                case AnimationDataSourceKind.RandomAsset:
                    return RandomAssetAnimationData.TryGet(randomAsset, out data);
                case AnimationDataSourceKind.RandomInline:
                    return RandomInlineAnimationData.TryGet(randomInline, out data);
                default:
                    data = null;
                    return false;
            }
        }

        public static bool TryGet(AnimationDataSource source, out IAnimationData data)
        {
            if (source == null)
            {
                data = null;
                return false;
            }
            return source.TryGet(out data);
        }
    }

    [Serializable]
    public sealed class InlineAnimationData : IAnimationData
    {
        [BoxGroup("Header"), LabelText("Name")]
        public string animationName;

        [BoxGroup("Header"), LabelText("Seconds per Sample"), MinValue(0.001f)]
        public float defaultFrameDuration = 0.1f;

        [BoxGroup("Header"), LabelText("Await Frame Commands")]
        [Tooltip("If true, waits for per-frame Commands before advancing to the next frame.")]
        public bool awaitFrameCommands = false;

        [FoldoutGroup("Hooks"), LabelText("On Start")]
        public VNext.CommandListData onStart = new();

        [FoldoutGroup("Hooks"), LabelText("On End")]
        public VNext.CommandListData onEnd = new();

        [FoldoutGroup("Hooks"), LabelText("On Canceled")]
        public VNext.CommandListData onCanceled = new();

        [TableList(AlwaysExpanded = true, NumberOfItemsPerPage = 12)]
        public List<InlineSpriteAnimationFrame> frames = new();

        [Serializable]
        public sealed class InlineSpriteAnimationFrame : IAnimationFrame
        {
            [TableColumnWidth(80, Resizable = false)]
            [PreviewField(Alignment = ObjectFieldAlignment.Left, Height = 40)]
            [HideLabel] public Sprite sprite;

            [TableColumnWidth(70, Resizable = false)]
            [LabelText("Duration (s)"), MinValue(0)] public float duration = 0.1f;

            [LabelText("Commands")]
            [ListDrawerSettings(DraggableItems = true, ShowPaging = false)]
            public VNext.CommandListData commands = new();

            Sprite IAnimationFrame.Sprite => sprite;
            float IAnimationFrame.Duration => duration;
            VNext.CommandListData IAnimationFrame.Commands => commands;
        }

        string IAnimationData.AnimationName => animationName ?? string.Empty;
        float IAnimationData.DefaultFrameDuration => Mathf.Max(0.001f, defaultFrameDuration);
        bool IAnimationData.AwaitFrameCommands => awaitFrameCommands;
        VNext.CommandListData IAnimationData.OnStart => onStart;
        VNext.CommandListData IAnimationData.OnEnd => onEnd;
        VNext.CommandListData IAnimationData.OnCanceled => onCanceled;
        IReadOnlyList<IAnimationFrame> IAnimationData.Frames => frames;

        public static bool IsPlayable(InlineAnimationData clip)
        {
            return clip != null && clip.frames != null && clip.frames.Count > 0;
        }
    }

    [Serializable]
    public sealed class RandomInlineAnimationData
    {
        [Serializable]
        public sealed class Entry
        {
            [HorizontalGroup("Row", Width = 90f)]
            [LabelText("Weight")]
            [MinValue(0f)]
            public float weight = 1f;

            [HorizontalGroup("Row")]
            [HideLabel]
            public InlineAnimationData inline = new();
        }

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<Entry> entries = new();

        public bool TryGet(out IAnimationData data)
        {
            if (!TryPick(entries, out var picked))
            {
                data = null;
                return false;
            }

            data = picked;
            return true;
        }

        public static bool TryGet(RandomInlineAnimationData source, out IAnimationData data)
        {
            if (source == null)
            {
                data = null;
                return false;
            }

            return source.TryGet(out data);
        }

        static bool TryPick(List<Entry> sourceEntries, out InlineAnimationData picked)
        {
            picked = null;
            if (sourceEntries == null || sourceEntries.Count == 0)
                return false;

            float totalWeight = 0f;
            var fallback = default(InlineAnimationData);
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || !InlineAnimationData.IsPlayable(entry.inline))
                    continue;

                fallback ??= entry.inline;
                if (entry.weight > 0f)
                    totalWeight += entry.weight;
            }

            if (fallback == null)
                return false;

            if (totalWeight <= 0f)
            {
                picked = fallback;
                return true;
            }

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || !InlineAnimationData.IsPlayable(entry.inline) || entry.weight <= 0f)
                    continue;

                acc += entry.weight;
                if (roll <= acc)
                {
                    picked = entry.inline;
                    return true;
                }
            }

            picked = fallback;
            return true;
        }
    }

    [Serializable]
    public sealed class RandomAssetAnimationData
    {
        [Serializable]
        public sealed class Entry
        {
            [HorizontalGroup("Row", Width = 90f)]
            [LabelText("Weight")]
            [MinValue(0f)]
            public float weight = 1f;

            [HorizontalGroup("Row")]
            [HideLabel]
            [AssetOrInternal]
            public AnimationData asset;
        }

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<Entry> entries = new();

        public bool TryGet(out IAnimationData data)
        {
            if (!TryPick(entries, out var picked))
            {
                data = null;
                return false;
            }

            data = picked;
            return true;
        }

        public static bool TryGet(RandomAssetAnimationData source, out IAnimationData data)
        {
            if (source == null)
            {
                data = null;
                return false;
            }

            return source.TryGet(out data);
        }

        static bool TryPick(List<Entry> sourceEntries, out AnimationData picked)
        {
            picked = null;
            if (sourceEntries == null || sourceEntries.Count == 0)
                return false;

            float totalWeight = 0f;
            AnimationData fallback = null;
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || !IsPlayable(entry.asset))
                    continue;

                fallback ??= entry.asset;
                if (entry.weight > 0f)
                    totalWeight += entry.weight;
            }

            if (fallback == null)
                return false;

            if (totalWeight <= 0f)
            {
                picked = fallback;
                return true;
            }

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || !IsPlayable(entry.asset) || entry.weight <= 0f)
                    continue;

                acc += entry.weight;
                if (roll <= acc)
                {
                    picked = entry.asset;
                    return true;
                }
            }

            picked = fallback;
            return true;
        }

        static bool IsPlayable(AnimationData asset)
        {
            var frames = asset?.frames;
            return frames != null && frames.Count > 0;
        }
    }

    /// <summary>
    /// スプライトアニメーション用データ。フレーム列とフック用コマンドを保持し、簡易プレビューやスプライトシートインポートも備える。
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationData", menuName = "Game/Animation/AnimationData")]
    public sealed class AnimationData : SerializedScriptableObject, IAnimationData
    {
        [BoxGroup("Header"), LabelText("Name")] public string animationName;
        [BoxGroup("Header"), LabelText("Seconds per Sample"), MinValue(0.001f)] public float defaultFrameDuration = 0.1f;
        [BoxGroup("Header"), LabelText("Await Frame Commands")]
        [Tooltip("If true, waits for per-frame Commands before advancing to the next frame.")]
        public bool awaitFrameCommands = false;

        // アニメ開始/終了/キャンセル時に発火するコマンド
        [FoldoutGroup("Hooks")]
        [LabelText("On Start")]
        public VNext.CommandListData onStart = new();

        [FoldoutGroup("Hooks")]
        [LabelText("On End")]
        public VNext.CommandListData onEnd = new();

        [FoldoutGroup("Hooks")]
        [LabelText("On Canceled")]
        public VNext.CommandListData onCanceled = new();

        // フレームデータ本体
        [TableList(AlwaysExpanded = true, NumberOfItemsPerPage = 12)]
        public List<SpriteAnimationFrame> frames = new();

        [Serializable]
        public sealed class SpriteAnimationFrame : IAnimationFrame
        {
            [TableColumnWidth(80, Resizable = false)]
            [PreviewField(Alignment = ObjectFieldAlignment.Left, Height = 40)]
            [HideLabel] public Sprite sprite;

            [TableColumnWidth(70, Resizable = false)]
            [LabelText("Duration (s)"), MinValue(0)] public float duration = 0.1f;

            // フレーム再生時に発火するコマンド
            [LabelText("Commands")]
            [ListDrawerSettings(DraggableItems = true, ShowPaging = false)]
            public VNext.CommandListData commands = new();

            Sprite IAnimationFrame.Sprite => sprite;
            float IAnimationFrame.Duration => duration;
            VNext.CommandListData IAnimationFrame.Commands => commands;
        }

        string IAnimationData.AnimationName => animationName ?? string.Empty;
        float IAnimationData.DefaultFrameDuration => Mathf.Max(0.001f, defaultFrameDuration);
        bool IAnimationData.AwaitFrameCommands => awaitFrameCommands;
        VNext.CommandListData IAnimationData.OnStart => onStart;
        VNext.CommandListData IAnimationData.OnEnd => onEnd;
        VNext.CommandListData IAnimationData.OnCanceled => onCanceled;
        IReadOnlyList<IAnimationFrame> IAnimationData.Frames => frames;

#if UNITY_EDITOR
        [TitleGroup("Name")]
        [HorizontalGroup("Name/Buttons")]
        [Button("Asset名に反映", ButtonSizes.Medium)]
        void __ApplyDisplayNameToAsset()
        {
            if (string.IsNullOrEmpty(animationName)) return;
            var path = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) return;

            Undo.RecordObject(this, "Rename AnimationData Asset");
            name = animationName;
            EditorUtility.SetDirty(this);
            AssetDatabase.ImportAsset(path);
        }

        private void OnValidate()
        {
            // 破損防止のため null を除去
            frames?.RemoveAll(f => f == null);
            onStart ??= new VNext.CommandListData();
            onEnd ??= new VNext.CommandListData();
            onCanceled ??= new VNext.CommandListData();
            if (frames != null)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i] != null)
                        frames[i].commands ??= new VNext.CommandListData();
                }
            }
        }

        [FoldoutGroup("Tools"), Button(ButtonSizes.Medium), GUIColor(1f, 0.6f, 0.4f)]
        void CleanNullFrames()
        {
            int before = frames?.Count ?? 0;
            frames?.RemoveAll(f => f == null);
            int after = frames?.Count ?? 0;
            EditorUtility.SetDirty(this);
            Debug.Log($"[AnimationData] Cleaned frames {before}->{after} in '{name}'.");
        }

        // ====== 簡易プレビュー ======
        [FoldoutGroup("Preview"), ShowInInspector, ReadOnly] private int _visibleIndex;
        [FoldoutGroup("Preview"), ShowInInspector, ReadOnly] private bool _playing;
        [FoldoutGroup("Preview"), ShowInInspector, PreviewField(Alignment = ObjectFieldAlignment.Center, Height = 90)]
        [HideLabel]
        private Sprite _previewSprite => (frames != null && frames.Count > 0 && _visibleIndex >= 0 && _visibleIndex < frames.Count)
            ? frames[_visibleIndex].sprite : null;

        [FoldoutGroup("Preview"), Button(ButtonSizes.Medium), GUIColor(0.6f, 1f, 0.6f)]
        void PlayPreview()
        {
            if (_playing || frames == null || frames.Count == 0) return;
            _playing = true;
            _visibleIndex = 0;
            _nextTime = EditorApplication.timeSinceStartup + Mathf.Max(0.001f, defaultFrameDuration);
            EditorApplication.update -= TickPreview;
            EditorApplication.update += TickPreview;
        }

        [FoldoutGroup("Preview"), Button(ButtonSizes.Medium), GUIColor(1f, 0.5f, 0.5f)]
        void StopPreview()
        {
            if (!_playing) return;
            _playing = false;
            EditorApplication.update -= TickPreview;
        }

        double _nextTime;
        void TickPreview()
        {
            if (!_playing || frames == null || frames.Count == 0) { StopPreview(); return; }
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextTime) return;

            _visibleIndex++;
            if (_visibleIndex >= frames.Count)
            {
                _visibleIndex = frames.Count - 1;
                StopPreview();
            }
            _nextTime = now + Mathf.Max(0.001f, defaultFrameDuration);
            EditorUtility.SetDirty(this);
        }

        void OnDisable() => StopPreview();

        // ====== SpriteSheet からフレームを生成 ======
        [FoldoutGroup("Import"), LabelText("Source Texture")]
        [AssetSelector(Paths = "Assets"), InlineButton(nameof(ImportFromTexture), "Import")]
        public Texture2D importSourceTexture;

        [FoldoutGroup("Import"), LabelText("Default Time"), MinValue(0)]
        public float importDefaultDuration = 0f;

        [FoldoutGroup("Import"), LabelText("Append (末尾追加)")]
        public bool importAppend = false;

        [FoldoutGroup("Import"), LabelText("Sort Top→Bottom, Left→Right")]
        public bool importSortGrid = true;

        void ImportFromTexture()
        {
            if (!importSourceTexture) return;

            var path = AssetDatabase.GetAssetPath(importSourceTexture);
            if (string.IsNullOrEmpty(path)) return;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                       .OfType<Sprite>()
                                       .ToList();

            if (sprites.Count == 0)
            {
                Debug.LogWarning("Spriteが見つかりません。SpriteMode=Multiple？");
                return;
            }

            if (importSortGrid)
                sprites = sprites.OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToList();

            Undo.RecordObject(this, "Import Animation Frames");
            if (!importAppend) frames.Clear();

            foreach (var s in sprites)
            {
                frames.Add(new SpriteAnimationFrame
                {
                    sprite = s,
                    duration = Mathf.Max(0f, importDefaultDuration),
                    commands = new VNext.CommandListData()
                });
            }

            _visibleIndex = 0;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"Imported {sprites.Count} sprites into '{name}'.");
        }
#endif
    }
}

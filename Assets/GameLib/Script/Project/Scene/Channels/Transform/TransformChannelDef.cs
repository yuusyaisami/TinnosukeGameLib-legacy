using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    // ================================================================
    // InitialTransformSettings - 初期 Transform 設定
    // ================================================================
    //
    // Prefab 生成時に DI よりも先に Transform を設定するための構造体。
    // Awake のタイミングで即座に適用され、1 frame の遅延を防ぐ。
    //
    // ================================================================

    /// <summary>
    /// Transform の初期設定。
    /// </summary>
    [Serializable]
    public sealed class InitialTransformSettings
    {
        [FoldoutGroup("Initial Transform")]
        [LabelText("Apply on Awake")]
        [Tooltip("Awake 時に Transform を即座に適用するか")]
        public bool applyOnAwake;

        [FoldoutGroup("Initial Transform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Local Position")]
        public bool applyLocalPosition;

        [FoldoutGroup("Initial Transform")]
        [ShowIf("@applyOnAwake && applyLocalPosition")]
        [LabelText("Position")]
        public Vector3 localPosition;

        [FoldoutGroup("Initial Transform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Local Rotation")]
        public bool applyLocalRotation;

        [FoldoutGroup("Initial Transform")]
        [ShowIf("@applyOnAwake && applyLocalRotation")]
        [LabelText("Rotation (Euler)")]
        public Vector3 localRotation;

        [FoldoutGroup("Initial Transform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Local Scale")]
        public bool applyLocalScale;

        [FoldoutGroup("Initial Transform")]
        [ShowIf("@applyOnAwake && applyLocalScale")]
        [LabelText("Scale")]
        public Vector3 localScale = Vector3.one;
    }

    /// <summary>
    /// RectTransform の初期設定（Transform 設定に加えて）。
    /// </summary>
    [Serializable]
    public sealed class InitialRectTransformSettings
    {
        [FoldoutGroup("Initial RectTransform")]
        [LabelText("Apply on Awake")]
        [Tooltip("Awake 時に RectTransform を即座に適用するか")]
        public bool applyOnAwake;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Anchored Position")]
        public bool applyAnchoredPosition;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf("@applyOnAwake && applyAnchoredPosition")]
        [LabelText("Position")]
        public Vector2 anchoredPosition;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Size Delta")]
        public bool applySizeDelta;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf("@applyOnAwake && applySizeDelta")]
        [LabelText("Size")]
        public Vector2 sizeDelta;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Pivot")]
        public bool applyPivot;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf("@applyOnAwake && applyPivot")]
        [LabelText("Pivot")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Anchor Min")]
        public bool applyAnchorMin;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf("@applyOnAwake && applyAnchorMin")]
        [LabelText("Anchor Min")]
        public Vector2 anchorMin = new Vector2(0.5f, 0.5f);

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf(nameof(applyOnAwake))]
        [LabelText("Anchor Max")]
        public bool applyAnchorMax;

        [FoldoutGroup("Initial RectTransform")]
        [ShowIf("@applyOnAwake && applyAnchorMax")]
        [LabelText("Anchor Max")]
        public Vector2 anchorMax = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Transform / RectTransform 用チャネル定義。
    /// AnimationTransformHub 用。
    /// </summary>
    [Serializable]
    public sealed class TransformChannelDef : ChannelDefBase, IChannelTransform
    {
        [Header("Target")]
        [SerializeField, ShowIf("isShowTransform")] Transform target;
        [SerializeField, ShowIf("isShowRectTransform")] RectTransform rectTransform;

        [Header("Presets")]
        [SerializeField] bool playOnSpawnPreset;
        [SerializeField, ShowIf(nameof(isShowPlayOnSpawnPreset)), MinValue(0f)]
        [LabelText("Play On Spawn Delay Seconds")]
        [Tooltip("スポーン時再生までの待機秒数。0 で即時再生。")]
        float playOnSpawnDelaySeconds;
        [SerializeField, ShowIf("isShowPlayOnSpawnPreset"), InlineProperty, HideLabel]
        TransformAnimationPreset transformPreset = new();

        [Header("Initial Settings (Applied on Awake)")]
        [SerializeField, InlineProperty, HideLabel]
        InitialTransformSettings initialTransform = new();

        [SerializeField, InlineProperty, HideLabel, ShowIf(nameof(IsRectTransform))]
        InitialRectTransformSettings initialRectTransform = new();

        public Transform Transform => target;
        public RectTransform RectTransform => rectTransform;

        public Transform TargetTransformOrRectTransform
        {
            get
            {
                if (rectTransform != null) return rectTransform;
                if (target != null) return target;
                throw new InvalidOperationException("Both Transform and RectTransform are null.");
            }
        }

        public bool PlayOnSpawnPreset => playOnSpawnPreset;
        public float PlayOnSpawnDelaySeconds => playOnSpawnDelaySeconds;
        public ITransformAnimationPreset TransformPreset => transformPreset;

        public InitialTransformSettings InitialTransform => initialTransform;
        public InitialRectTransformSettings InitialRectTransform => initialRectTransform;

        public bool IsRectTransform => rectTransform != null || (target != null && target is RectTransform);

        private bool isShowTransform() => rectTransform == null || target != null;
        private bool isShowRectTransform() => target == null || rectTransform != null;
        private bool isShowPlayOnSpawnPreset() => playOnSpawnPreset;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (!target && !rectTransform && owner)
            {
                target = owner.transform;
            }
        }

        /// <summary>
        /// 初期 Transform 設定を即座に適用する。
        /// Awake のタイミングで呼び出される。
        /// </summary>
        public void ApplyInitialTransform()
        {
            var t = rectTransform != null ? (Transform)rectTransform : target;
            if (t == null) return;

            // Transform 共通設定
            if (initialTransform != null && initialTransform.applyOnAwake)
            {
                if (initialTransform.applyLocalPosition)
                    t.localPosition = initialTransform.localPosition;

                if (initialTransform.applyLocalRotation)
                    t.localRotation = Quaternion.Euler(initialTransform.localRotation);

                if (initialTransform.applyLocalScale)
                    t.localScale = initialTransform.localScale;
            }

            // RectTransform 専用設定
            var rect = t as RectTransform;
            if (rect == null) rect = rectTransform;
            if (rect == null) return;

            if (initialRectTransform != null && initialRectTransform.applyOnAwake)
            {
                // Pivot を先に設定（位置に影響するため）
                if (initialRectTransform.applyPivot)
                    SetPivotWithPositionPreserved(rect, initialRectTransform.pivot);

                if (initialRectTransform.applyAnchorMin)
                    rect.anchorMin = initialRectTransform.anchorMin;

                if (initialRectTransform.applyAnchorMax)
                    rect.anchorMax = initialRectTransform.anchorMax;

                if (initialRectTransform.applyAnchoredPosition)
                    rect.anchoredPosition = initialRectTransform.anchoredPosition;

                if (initialRectTransform.applySizeDelta)
                    rect.sizeDelta = initialRectTransform.sizeDelta;
            }
        }

        /// <summary>
        /// Pivot を変更しても表示位置が変わらないように anchoredPosition を補正して設定する。
        /// </summary>
        static void SetPivotWithPositionPreserved(RectTransform rect, Vector2 newPivot)
        {
            var oldPivot = rect.pivot;
            var size = rect.rect.size;

            var deltaPos = new Vector2(
                (newPivot.x - oldPivot.x) * size.x,
                (newPivot.y - oldPivot.y) * size.y
            );

            rect.pivot = newPivot;
            rect.anchoredPosition += deltaPos;
        }
    }
}

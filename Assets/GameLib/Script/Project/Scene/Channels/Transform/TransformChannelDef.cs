using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    // ================================================================
    // InitialTransformSettings - 蛻晄悄 Transform 險ｭ螳・
    // ================================================================
    //
    // Prefab 逕滓・譎ゅ↓ DI 繧医ｊ繧ょ・縺ｫ Transform 繧定ｨｭ螳壹☆繧九◆繧√・讒矩菴薙・
    // Awake 縺ｮ繧ｿ繧､繝溘Φ繧ｰ縺ｧ蜊ｳ蠎ｧ縺ｫ驕ｩ逕ｨ縺輔ｌ縲・ frame 縺ｮ驕・ｻｶ繧帝亟縺舌・
    //
    // ================================================================

    /// <summary>
    /// Transform 縺ｮ蛻晄悄險ｭ螳壹・
    /// </summary>
    [Serializable]
    public sealed class InitialTransformSettings
    {
        [FoldoutGroup("Initial Transform")]
        [LabelText("Apply on Awake")]
        [Tooltip("Inspector setting.")]
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
    /// RectTransform 縺ｮ蛻晄悄險ｭ螳夲ｼ・ransform 險ｭ螳壹↓蜉縺医※・峨・
    /// </summary>
    [Serializable]
    public sealed class InitialRectTransformSettings
    {
        [FoldoutGroup("Initial RectTransform")]
        [LabelText("Apply on Awake")]
        [Tooltip("Inspector setting.")]
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
    /// Transform / RectTransform 逕ｨ繝√Ε繝阪Ν螳夂ｾｩ縲・
    /// AnimationTransformHub 逕ｨ縲・
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
        [Tooltip("Inspector setting.")]
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
        /// 蛻晄悄 Transform 險ｭ螳壹ｒ蜊ｳ蠎ｧ縺ｫ驕ｩ逕ｨ縺吶ｋ縲・
        /// Awake 縺ｮ繧ｿ繧､繝溘Φ繧ｰ縺ｧ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// </summary>
        public void ApplyInitialTransform()
        {
            var t = rectTransform != null ? (Transform)rectTransform : target;
            if (t == null) return;

            // Transform 蜈ｱ騾夊ｨｭ螳・
            if (initialTransform != null && initialTransform.applyOnAwake)
            {
                if (initialTransform.applyLocalPosition)
                    t.localPosition = initialTransform.localPosition;

                if (initialTransform.applyLocalRotation)
                    t.localRotation = Quaternion.Euler(initialTransform.localRotation);

                if (initialTransform.applyLocalScale)
                    t.localScale = initialTransform.localScale;
            }

            // RectTransform 蟆ら畑險ｭ螳・
            var rect = t as RectTransform;
            if (rect == null) rect = rectTransform;
            if (rect == null) return;

            if (initialRectTransform != null && initialRectTransform.applyOnAwake)
            {
                // Pivot 繧貞・縺ｫ險ｭ螳夲ｼ井ｽ咲ｽｮ縺ｫ蠖ｱ髻ｿ縺吶ｋ縺溘ａ・・
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
        /// Pivot 繧貞､画峩縺励※繧り｡ｨ遉ｺ菴咲ｽｮ縺悟､峨ｏ繧峨↑縺・ｈ縺・↓ anchoredPosition 繧定｣懈ｭ｣縺励※險ｭ螳壹☆繧九・
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

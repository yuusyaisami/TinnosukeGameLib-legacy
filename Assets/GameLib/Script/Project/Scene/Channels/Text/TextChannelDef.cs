using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using Febucci.TextAnimatorCore.Typing;
using Febucci.TextAnimatorForUnity;
using Sirenix.OdinInspector;
using Game.MaterialFx;
using DG.Tweening;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Channel
{
    [Serializable]
    public sealed class TextChannelDef : ChannelDefBase, IChannelText, IChannelMaterialFx
    {
        [Header("Target")]
        [SerializeField] TMP_Text text;

        [SerializeField, Sirenix.OdinInspector.ListDrawerSettings(ShowPaging = false, ShowFoldout = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> materialFxPresetEntries = new();

        [Header("TextAnimator (Editor Auto Attach)")]
        [SerializeField] bool useRichTextAnimator = true;


        [SerializeField, ShowIf("useRichTextAnimator")] bool useTypewriter = false;

        [Header("Typewriter Event Commands")]
        [SerializeField, ShowIf(nameof(useTypewriter))]
        TypewriterEventCommandBindings typewriterEventCommands = new();

        [Header("Counter")]
        [SerializeField] bool useCounter = false;

        [SerializeField, ShowIf(nameof(useCounter))]
        [LabelText("Counter Ease")]
        Ease counterEase = Ease.OutQuad;

        [SerializeField, ShowIf(nameof(useCounter))]
        [LabelText("Counter Duration (sec)")]
        [Min(0.01f)]
        float counterDurationSeconds = 0.5f;

        [SerializeField, ShowIf(nameof(useCounter))]
        [LabelText("Use Unscaled Time")]
        bool counterUseUnscaledTime = false;

        public TMP_Text Text => text;
        public IReadOnlyList<MaterialFxPresetEntry> MaterialFxPresetEntries => materialFxPresetEntries;

        public bool UseRichTextAnimator => useRichTextAnimator;
        public bool UseTypewriter => useTypewriter;
        public TypewriterEventCommandBindings TypewriterEventCommands => typewriterEventCommands;

        public bool UseCounter => useCounter;
        public Ease CounterEase => counterEase;
        public float CounterDurationSeconds => counterDurationSeconds;
        public bool CounterUseUnscaledTime => counterUseUnscaledTime;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (!text && owner)
                text = owner.GetComponentInChildren<TMP_Text>(true);

            typewriterEventCommands?.BindDebugOwner(owner, nameof(typewriterEventCommands));

#if UNITY_EDITOR
            // ★PlayModeでは絶対に触らない（要求通り Editor 専用）
            if (!Application.isPlaying && text)
            {
                // TextAnimator / Typewriter を Def の指定に従って “Editorで” 自動付与
                if (!text) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var go = text.gameObject;

                if (useRichTextAnimator && !go.TryGetComponent<TextAnimator_TMP>(out var taComp))
                    go.AddComponent<TextAnimator_TMP>();
                if (useTypewriter && !go.TryGetComponent<TypewriterComponent>(out var twComp))
                    go.AddComponent<TypewriterComponent>();
            }
#endif
        }
    }
}

#nullable enable

using System;
using Game.Channel;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum DialogSpawnSource
    {
        RuntimeTemplate = 0,
        Prefab = 1,
    }

    public enum DialogPrefabParentMode
    {
        OverlayLayerRoot = 0,
        OwnerTransform = 1,
    }

    [Serializable]
    public sealed class DialogRuntimeOptions
    {
        [LabelText("Focus On Show")]
        public bool FocusOnShow = true;
    }

    [Serializable]
    public struct DialogEventBinding
    {
        [LabelText("Event Key")]
        public string EventKey;

        [LabelText("Commands")]
        public VNext.CommandListData Commands;

        [LabelText("Close After Invoke")]
        public bool CloseAfterInvoke;
    }

    [Serializable]
    public sealed class DialogChannelDef : ChannelDefBase
    {
        [BoxGroup("Spawn")]
        [LabelText("Spawn Source")]
        [EnumToggleButtons]
        public DialogSpawnSource SpawnSource = DialogSpawnSource.Prefab;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UseTemplateSO))]
        [LabelText("Runtime Template")]
        public DialogRuntimeTemplateSO? DialogRuntimeTemplate;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsePrefabSO))]
        [LabelText("Dialog Prefab")]
        public GameObject? DialogPrefab;

        [BoxGroup("Spawn")]
        [LabelText("Layer")]
        public OverlayLayerId Layer = OverlayLayerId.Dialog;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsePrefabSO))]
        [LabelText("Prefab Parent")]
        public DialogPrefabParentMode PrefabParentMode = DialogPrefabParentMode.OverlayLayerRoot;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UseTemplateSO))]
        [LabelText("Allow Pooling")]
        public bool AllowPooling = false;

        [BoxGroup("Runtime")]
        [LabelText("Runtime Options")]
        public DialogRuntimeOptions RuntimeOptions = new();

        [BoxGroup("Bindings")]
        [LabelText("Bindings")]
        public DialogEventBinding[] Bindings = Array.Empty<DialogEventBinding>();

        [BoxGroup("Modal")]
        [LabelText("Push To ModalStack On Show")]
        public bool PushToModalStackOnShow = false;

        [BoxGroup("Modal")]
        [ShowIf(nameof(PushToModalStackOnShow))]
        [LabelText("Modal Options")]
        public ModalOptions ModalOptions = ModalOptions.Default;

        [BoxGroup("Modal")]
        [LabelText("Auto Close On ModalStack Change")]
        public bool AutoCloseOnModalStackChange = false;

        public bool UseTemplateSO => SpawnSource == DialogSpawnSource.RuntimeTemplate;
        public bool UsePrefabSO => SpawnSource == DialogSpawnSource.Prefab;

        public OverlayLayerMask GetValidatedLayerMask()
        {
            var mask = OverlayLayerMask.From(Layer);
            if (!mask.IsSingleBit)
                return OverlayLayerMask.Dialog;
            return mask;
        }

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            RuntimeOptions ??= new DialogRuntimeOptions();

            Bindings ??= Array.Empty<DialogEventBinding>();
            for (int i = 0; i < Bindings.Length; i++)
            {
                var b = Bindings[i];
                b.EventKey ??= string.Empty;
                b.Commands ??= new VNext.CommandListData();
                Bindings[i] = b;
            }

            // Ensure a single-bit overlay layer.
            var bits = (int)Layer;
            var isSingleBit = bits != 0 && (bits & (bits - 1)) == 0;
            if (!isSingleBit)
            {
                Layer = OverlayLayerId.Dialog;
            }

            var hasTemplate = DialogRuntimeTemplate != null;
            var hasPrefab = DialogPrefab != null;

            if (!hasTemplate && !hasPrefab)
            {
                try
                {
                    Debug.LogWarning(
                        $"[{nameof(DialogChannelDef)}] Invalid spawn config: both {nameof(DialogRuntimeTemplate)} and {nameof(DialogPrefab)} are null. " +
                        $"Tag='{Tag}'. SpawnSource cannot be determined; defaulting to {DialogSpawnSource.Prefab}.",
                        owner);
                }
                catch
                {
                }

                // Keep the value deterministic even though the def is invalid (caller should handle null on spawn).
                SpawnSource = DialogSpawnSource.Prefab;
                return;
            }

            // Enforce spawn source requirements.
            if (SpawnSource == DialogSpawnSource.RuntimeTemplate)
            {
                if (!hasTemplate && hasPrefab)
                    SpawnSource = DialogSpawnSource.Prefab;
            }
            else
            {
                if (!hasPrefab && hasTemplate)
                    SpawnSource = DialogSpawnSource.RuntimeTemplate;
            }
        }
    }
}

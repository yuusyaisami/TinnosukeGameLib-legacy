#nullable enable
using System;
using Game.Commands;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum UIControlOperation
    {
        // Modal stack
        ModalPush = 0,
        ModalPop = 1,
        ModalPopTop = 2,
        ModalClearAll = 3,
        ModalSetDefaultRoot = 4,

        // Selection
        Select = 10,
        TrySelect = 11,
        ClearSelection = 12,

        // Element state
        SetActive = 20,
        ToggleActive = 21,
        SetVisible = 22,
        ToggleVisible = 23,

        // Navigation
        SetNavigationSelectable = 30,
        SetNavigationOverride = 31,
        ClearNavigationOverride = 32,
    }

    [Serializable]
    public sealed class UIControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.UIControl;
        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                var thenCount = Then?.Count ?? 0;
                var stackLabel = string.IsNullOrEmpty(StackKey) ? "" : $" Stack={StackKey}";
                return $"{targetLabel} Op={Operation}{stackLabel} Then={thenCount}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [LabelText("Operation")]
        public UIControlOperation Operation;

        [ShowIf("@Operation == UIControlOperation.ModalPush")]
        [LabelText("Modal Options")]
        public ModalOptions ModalOptions = ModalOptions.Default;

        [ShowIf("@Operation == UIControlOperation.ModalPush || Operation == UIControlOperation.ModalPop || Operation == UIControlOperation.ModalPopTop || Operation == UIControlOperation.ModalSetDefaultRoot")]
        [LabelText("Stack Key")]
        public string StackKey = string.Empty;

        [ShowIf("@Operation == UIControlOperation.SetActive")]
        [LabelText("Active")]
        public bool Active = true;

        [ShowIf("@Operation == UIControlOperation.SetVisible")]
        [LabelText("Visible")]
        public bool Visible = true;

        [ShowIf("@Operation == UIControlOperation.SetNavigationSelectable")]
        [LabelText("Navigation Selectable")]
        public bool NavigationSelectable = true;

        [ShowIf("@Operation == UIControlOperation.SetNavigationOverride")]
        [LabelText("Navigation Override")]
        public NavigationOverride? NavigationOverride;

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy;

        [LabelText("Then (With Target As Actor)")]
        public CommandListData Then = new();
    }
}
